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
    using System.Threading.Tasks;

    // One sampler for every key. It reads once a second (slower readings less often) and raises Changed. It runs only while a key is being
    // drawn: each key calls Use when it draws, and the sampler stops when nothing has drawn for a few seconds.

    public class SystemMonitor : IDisposable
    {
        private const Int32 TemperatureEvery = 2;
        private const Int32 AppEvery = 5;
        private const Int32 PingEvery = 5;
        private const Int32 DiskEvery = 10;
        private const String PingHost = "1.1.1.1";
        private static readonly TimeSpan IdleAfter = TimeSpan.FromSeconds(5);

        private readonly Object _lock = new Object();
        private Timer _timer;
        private DateTime _lastUse;
        private Int32 _tick;
        private Int32 _busy;
        private (UInt64 Busy, UInt64 Idle) _lastCpu;
        private (Int64 Down, Int64 Up)? _lastBytes;
        private DateTime _lastBytesAt;
        private Int32 _pinging;

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

        public AppUsage TopApp { get; private set; }

        public History TopAppHistory { get; } = new History();

        // 0 to 100, or null if the GPU doesn't report it.
        public Double? Gpu { get; private set; }

        public History GpuHistory { get; } = new History();

        // °C, or null without CPU sensors.
        public Double? CpuTemperature { get; private set; }

        public History TemperatureHistory { get; } = new History();

        // The fastest fan in rpm, or null without fans.
        public Double? Fan { get; private set; }

        // Null without a battery.
        public BatteryState Battery { get; private set; }

        // Round trip to the internet in ms, or null when the last ping failed.
        public Double? Ping { get; private set; }

        public Boolean HasPinged { get; private set; }

        // "Ethernet", "Wi-Fi" or null when neither is connected.
        public String Link { get; private set; }

        // Wi-Fi signal above the noise in dB, 0 on Ethernet.
        public Int64 WifiSnr { get; private set; }

        // The connection's link speed in bits per second, 0 if unknown.
        public Double LinkSpeed { get; private set; }

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

                this.Gpu = Sensors.GpuLoad();
                this.GpuHistory.Add(this.Gpu ?? 0);

                if (this._tick % TemperatureEvery == 0)
                {
                    this.CpuTemperature = Sensors.CpuTemperature();
                    this.Fan = Sensors.FanSpeed();
                    this.TemperatureHistory.Add(this.CpuTemperature ?? 0);
                }

                if (this._tick % DiskEvery == 0)
                {
                    (this.DiskFree, this.DiskTotal) = Mac.Disk();
                    this.Battery = Sensors.Battery();
                }

                if (this._tick % PingEvery == 0)
                {
                    this.ReadInternet();
                }

                if (this._tick % AppEvery == 0)
                {
                    this.TopApp = ReadTopApp() ?? this.TopApp;
                    this.TopAppHistory.Add(this.TopApp?.Cpu ?? 0);
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

        // Pings in the background, so a slow or lost reply never holds up the other readings.
        // Wired beats Wi-Fi when both are connected, as macOS does by default.
        private void ReadInternet()
        {
            var (wifiName, rssi, noise, rate) = Mac.Wifi();
            var wired = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(adapter =>
                adapter.Name.StartsWith("en", StringComparison.Ordinal) && adapter.Name != wifiName
                && adapter.OperationalStatus == OperationalStatus.Up && adapter.GetIPProperties().GatewayAddresses.Count > 0);

            this.Link = wired != null ? "Ethernet" : rssi != 0 ? "Wi-Fi" : null;
            this.WifiSnr = wired == null && rssi != 0 && noise != 0 ? rssi - noise : 0;
            this.LinkSpeed = wired != null ? Math.Max(0, wired.Speed) : rate * 1e6;

            if (Interlocked.Exchange(ref this._pinging, 1) == 1)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    using var ping = new Ping();
                    var clock = Stopwatch.StartNew();
                    var reply = await ping.SendPingAsync(PingHost, 2000);

                    // .NET sometimes reports 0 ms for a real reply, so the stopwatch stands in.
                    this.Ping = reply.Status != IPStatus.Success ? null : reply.RoundtripTime > 0 ? reply.RoundtripTime : clock.ElapsedMilliseconds;
                }
                catch (Exception)
                {
                    this.Ping = null;
                }
                finally
                {
                    this.HasPinged = true;
                    Interlocked.Exchange(ref this._pinging, 0);
                }
            });
        }

        // Adds up every process by the app it belongs to, so Chrome's helpers count as Chrome.
        private static AppUsage ReadTopApp()
        {
            var start = new ProcessStartInfo("/bin/ps", "-Axo pcpu=,rss=,comm=")
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
                var parts = line.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 3 || parts[2] == "/bin/ps"
                    || !Double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var cpu)
                    || !Int64.TryParse(parts[1], out var rss))
                {
                    continue;
                }

                var name = AppName(parts[2].Trim());
                var app = apps.TryGetValue(name, out var found) ? found : apps[name] = new AppUsage { Name = name };
                app.Cpu += cpu;
                app.Memory += rss * 1024;
            }

            return apps.Values.OrderByDescending(app => app.Cpu).FirstOrDefault();
        }

        private static String AppName(String path)
        {
            var app = path.IndexOf(".app/", StringComparison.Ordinal);
            return app > 0 ? Path.GetFileName(path.Substring(0, app)) : Path.GetFileName(path);
        }

        private void LogReadings() => PluginLog.Info(FormattableString.Invariant(
            $"Readings: CPU {this.Cpu:0}%, memory {this.MemoryUsed / 1073741824.0:0.0} of {this.MemoryTotal / 1073741824.0:0} GB, pressure {this.Pressure}% level {this.PressureLevel}, ") +
            FormattableString.Invariant($"disk {this.DiskFree / 1e9:0.0} free of {this.DiskTotal / 1e9:0.0} GB, down {this.Down / 1e3:0} KB/s up {this.Up / 1e3:0} KB/s, ") +
            FormattableString.Invariant($"top {this.TopApp?.Name} {this.TopApp?.Cpu:0}% {this.TopApp?.Memory / 1e9:0.0} GB, ") +
            FormattableString.Invariant($"GPU {this.Gpu:0}%, CPU {this.CpuTemperature:0}°C, fan {this.Fan:0} rpm, battery {this.Battery?.Percent:0}%, ") +
            FormattableString.Invariant($"{this.Link} {this.LinkSpeed / 1e6:0} Mb/s SNR {this.WifiSnr} dB, ping {this.Ping:0} ms"));
    }

    public class AppUsage
    {
        public String Name { get; set; }

        public Double Cpu { get; set; }

        // Bytes. Resident memory, so shared pages count once per process.
        public Int64 Memory { get; set; }
    }
}
