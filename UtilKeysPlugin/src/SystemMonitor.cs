namespace Loupedeck.UtilKeysPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Threading;

    // One sampler for every key. It reads once a second (slower readings less often) and raises Changed. It runs only while a key is being
    // drawn: each key calls Use when it draws, and the sampler stops when nothing has drawn for a few seconds.

    public class SystemMonitor : IDisposable
    {
        private const Int32 TemperatureEvery = 2;
        private const Int32 AppEvery = 5;
        private const Int32 LinkEvery = 5;
        private const Int32 DiskEvery = 10;
        private static readonly TimeSpan IdleAfter = TimeSpan.FromSeconds(5);

        private readonly Object _lock = new Object();
        private readonly Dictionary<String, Byte[]> _icons = new Dictionary<String, Byte[]>();
        private Timer _timer;
        private DateTime _lastUse;
        private Int32 _tick;
        private Int32 _busy;
        private (UInt64 Busy, UInt64 Idle) _lastCpu;
        private (Int64 Down, Int64 Up)? _lastBytes;
        private DateTime _lastBytesAt;
        private (Double Read, Double Written)? _lastDisk;
        private DateTime _lastDiskAt;

        public event Action Changed;

        public Double Cpu { get; private set; }

        public History CpuHistory { get; } = new History();

        public UInt64 MemoryUsed { get; private set; }

        public UInt64 MemoryTotal { get; private set; }

        // 1 is normal, 2 is warning, 4 is critical.
        public Int32 PressureLevel { get; private set; }

        // 0 to 100, as in Activity Monitor's pressure graph.
        public Int32 Pressure { get; private set; }

        public Int64 DiskFree { get; private set; }

        public Int64 DiskTotal { get; private set; }

        // Bytes per second.
        public Double Down { get; private set; }

        public Double Up { get; private set; }

        public History DownHistory { get; } = new History();

        public History UpHistory { get; } = new History();

        // Disk bytes per second, all disks together.
        public Double DiskRead { get; private set; }

        public Double DiskWrite { get; private set; }

        public History DiskReadHistory { get; } = new History();

        public History DiskWriteHistory { get; } = new History();

        // The default microphone: whether there is one, whether an app is using it, and whether it's muted.
        public MicState Mic { get; private set; }

        public AppUsage TopApp { get; private set; }

        // The busiest app's icon as a PNG, or null when it has none (a background process, not an app).
        public Byte[] TopAppIcon { get; private set; }

        // 0 to 100, or null if the GPU doesn't report it.
        public Double? Gpu { get; private set; }

        public History GpuHistory { get; } = new History();

        // °C, or null without CPU sensors.
        public Double? CpuTemperature { get; private set; }

        public History TemperatureHistory { get; } = new History();

        // Null without a battery.
        public BatteryState Battery { get; private set; }

        // Ethernet's link speed in bits per second, or null when no Ethernet is connected.
        public Double? EthernetSpeed { get; private set; }

        // Wi-Fi's link speed in bits per second, or null when Wi-Fi isn't connected.
        public Double? WifiSpeed { get; private set; }

        // Wi-Fi signal above the noise in dB.
        public Int64 WifiSnr { get; private set; }

        public Boolean HasReadings { get; private set; }

        public void Use()
        {
            lock (this._lock)
            {
                this._lastUse = DateTime.UtcNow;

                if (this._timer == null)
                {
                    PluginLog.Info("Monitor started");
                    this._tick = 0;
                    this._lastBytes = null;
                    this._lastDisk = null;
                    this._lastCpu = Mac.CpuTicks();
                    this._timer = new Timer(_ => this.Sample(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
                }
            }
        }

        public void Dispose()
        {
            lock (this._lock)
            {
                this._timer?.Dispose();
                this._timer = null;
            }
        }

        private void Sample()
        {
            if (Interlocked.Exchange(ref this._busy, 1) == 1)
            {
                return;
            }

            try
            {
                lock (this._lock)
                {
                    if (DateTime.UtcNow - this._lastUse > IdleAfter)
                    {
                        PluginLog.Info("Monitor stopped: no key is showing");
                        this._timer?.Dispose();
                        this._timer = null;
                        return;
                    }
                }

                this.ReadCpu();
                this.ReadMemory();
                this.ReadNetwork();
                this.ReadDiskActivity();
                this.Mic = Audio.Mic();

                this.Gpu = Sensors.GpuLoad();
                this.GpuHistory.Add(this.Gpu ?? 0);

                if (this._tick % TemperatureEvery == 0)
                {
                    this.CpuTemperature = Sensors.CpuTemperature();
                    this.TemperatureHistory.Add(this.CpuTemperature ?? 0);
                }

                if (this._tick % DiskEvery == 0)
                {
                    (this.DiskFree, this.DiskTotal) = Mac.Disk();
                    this.Battery = Sensors.Battery();
                }

                if (this._tick % LinkEvery == 0)
                {
                    this.ReadLinks();
                }

                if (this._tick % AppEvery == 0)
                {
                    this.TopApp = ReadTopApp() ?? this.TopApp;
                    this.TopAppIcon = this.Icon(this.TopApp?.Path);
                }

                if (this._tick == 1)
                {
                    this.LogReadings();
                }

                this._tick++;
                this.HasReadings = true;
                this.Changed?.Invoke();
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Sampling failed");
            }
            finally
            {
                Interlocked.Exchange(ref this._busy, 0);
            }
        }

        private void ReadCpu()
        {
            var now = Mac.CpuTicks();
            var busy = now.Busy - this._lastCpu.Busy;
            var total = busy + (now.Idle - this._lastCpu.Idle);
            this._lastCpu = now;

            if (total > 0)
            {
                this.Cpu = 100.0 * busy / total;
                this.CpuHistory.Add(this.Cpu);
            }
        }

        private void ReadMemory()
        {
            this.MemoryUsed = Mac.MemoryUsed();
            this.MemoryTotal = Mac.MemoryTotal();
            this.PressureLevel = Mac.MemoryPressureLevel();
            this.Pressure = Math.Clamp(100 - Mac.MemoryFreeLevel(), 0, 100);
        }

        // Counts only the physical interfaces (en0, en1 and so on). VPN tunnels carry the same traffic again,
        // and loopback isn't the network.
        private void ReadNetwork()
        {
            Int64 down = 0, up = 0;

            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.Name.StartsWith("en", StringComparison.Ordinal) && adapter.OperationalStatus == OperationalStatus.Up)
                {
                    var stats = adapter.GetIPStatistics();
                    down += stats.BytesReceived;
                    up += stats.BytesSent;
                }
            }

            var now = DateTime.UtcNow;

            if (this._lastBytes is (Int64 lastDown, Int64 lastUp) && down >= lastDown && up >= lastUp)
            {
                var seconds = Math.Max(0.1, (now - this._lastBytesAt).TotalSeconds);
                this.Down = (down - lastDown) / seconds;
                this.Up = (up - lastUp) / seconds;
                this.DownHistory.Add(this.Down);
                this.UpHistory.Add(this.Up);
            }

            this._lastBytes = (down, up);
            this._lastBytesAt = now;
        }

        private void ReadDiskActivity()
        {
            var (read, written) = Sensors.DiskBytes();
            var now = DateTime.UtcNow;

            if (this._lastDisk is (Double lastRead, Double lastWritten) && read >= lastRead && written >= lastWritten)
            {
                var seconds = Math.Max(0.1, (now - this._lastDiskAt).TotalSeconds);
                this.DiskRead = (read - lastRead) / seconds;
                this.DiskWrite = (written - lastWritten) / seconds;
                this.DiskReadHistory.Add(this.DiskRead);
                this.DiskWriteHistory.Add(this.DiskWrite);
            }

            this._lastDisk = (read, written);
            this._lastDiskAt = now;
        }

        // Mutes or unmutes the default microphone, then reads it again so keys redraw at once.
        public void SetMicMuted(Boolean muted)
        {
            Audio.SetMicMuted(muted);
            this.Mic = Audio.Mic();
            this.Changed?.Invoke();
        }

        // Ethernet and Wi-Fi with their link speeds. Both can be connected at once.
        private void ReadLinks()
        {
            var (wifiName, rssi, noise, rate) = Mac.Wifi();
            var wired = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(adapter =>
                adapter.Name.StartsWith("en", StringComparison.Ordinal) && adapter.Name != wifiName
                && adapter.OperationalStatus == OperationalStatus.Up && adapter.GetIPProperties().GatewayAddresses.Count > 0);

            this.EthernetSpeed = wired == null ? null : Math.Max(0, wired.Speed);
            this.WifiSpeed = rssi == 0 ? null : rate * 1e6;
            this.WifiSnr = rssi != 0 && noise != 0 ? rssi - noise : 0;
        }

        // Adds up every process by the app it belongs to, so Chrome's helpers count as Chrome.
        private static AppUsage ReadTopApp()
        {
            var start = new ProcessStartInfo("/bin/ps", "-Axo pcpu=,comm=")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };

            using var ps = Process.Start(start);
            var output = ps.StandardOutput.ReadToEnd();
            ps.WaitForExit(2000);

            var apps = new Dictionary<String, AppUsage>();

            foreach (var line in output.Split('\n'))
            {
                var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 2 || parts[1] == "/bin/ps"
                    || !Double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var cpu))
                {
                    continue;
                }

                var (name, path) = AppName(parts[1].Trim());
                var app = apps.TryGetValue(name, out var found) ? found : apps[name] = new AppUsage { Name = name, Path = path };
                app.Cpu += cpu;
            }

            return apps.Values.OrderByDescending(app => app.Cpu).FirstOrDefault();
        }

        // The app a process belongs to, and the app's path; a process outside any app has no path.
        private static (String Name, String Path) AppName(String path)
        {
            var app = path.IndexOf(".app/", StringComparison.Ordinal);
            return app > 0 ? (Path.GetFileName(path.Substring(0, app)), path.Substring(0, app + 4)) : (Path.GetFileName(path), null);
        }

        // Icons are read once per app and kept.
        private Byte[] Icon(String appPath)
        {
            if (appPath == null)
            {
                return null;
            }

            if (!this._icons.TryGetValue(appPath, out var icon))
            {
                icon = this._icons[appPath] = Mac.AppIcon(appPath, 128);
            }

            return icon;
        }

        private void LogReadings() => PluginLog.Info(FormattableString.Invariant(
            $"Readings: CPU {this.Cpu:0}%, memory {this.MemoryUsed / 1073741824.0:0.0} of {this.MemoryTotal / 1073741824.0:0} GB, pressure {this.Pressure}% level {this.PressureLevel}, ") +
            FormattableString.Invariant($"disk {this.DiskFree / 1e9:0.0} free of {this.DiskTotal / 1e9:0.0} GB, down {this.Down / 1e3:0} KB/s up {this.Up / 1e3:0} KB/s, ") +
            FormattableString.Invariant($"top {this.TopApp?.Name} {this.TopApp?.Cpu:0}%, ") +
            FormattableString.Invariant($"GPU {this.Gpu:0}%, CPU {this.CpuTemperature:0}°C, battery {this.Battery?.Percent:0}%, ") +
            FormattableString.Invariant($"Ethernet {this.EthernetSpeed / 1e6:0} Mb/s, Wi-Fi {this.WifiSpeed / 1e6:0} Mb/s SNR {this.WifiSnr} dB, ") +
            FormattableString.Invariant($"disk read {this.DiskRead / 1e3:0} KB/s write {this.DiskWrite / 1e3:0} KB/s, mic {this.Mic}"));
    }

    public class AppUsage
    {
        public String Name { get; set; }

        // The .app bundle, or null for a process that isn't part of an app.
        public String Path { get; set; }

        public Double Cpu { get; set; }
    }
}
