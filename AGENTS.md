# Util Keys: agent notes

C# Logi Actions plugin for the MX Creative Keypad (3×3 keys). Its nine keys show live Mac system data. Read this before changing code.

## Rules
- Keep it minimal: no generated, optional or sample files.
- Commit only when asked.
- You can't see the keypad. Build, then ask the user to check on hardware.

## Build
- Needs the .NET 10 SDK and `LogiPluginTool`.
- Build from `UtilKeysPlugin/` only: `dotnet build`. It links Logi's Plugins folder to this repo's `bin/` and reloads the plugin. Expect 0 warnings.
- Never build from `src/` or from a copy of the repo: the build points `UtilKeysPlugin.link` at whatever folder it ran in.
- Log: `~/Library/Application Support/Logi/LogiPluginService/Logs/plugin_logs/UtilKeys.log`. A line starting `Readings:` is logged each time the monitor starts.
- To uninstall: `dotnet clean` in `UtilKeysPlugin/` removes the link.

## Code map (`UtilKeysPlugin/src/`)
- `SystemMonitor.cs`: one sampler for all keys, once a second (temperature every 2 s, busiest app and ping every 5 s, disk and battery every 10 s). Keys call `Use` when drawn; it stops 5 s after the last draw.
- `Mac.cs`: kernel, Foundation and Wi-Fi calls (Mach host statistics, sysctl, disk space, CoreWLAN).
- `Sensors.cs`: IOKit hardware readings: GPU, temperatures, fans, battery.
- `History.cs`: the last 60 samples for graphs.
- `KeyArt.cs`: draws every key: label, big value, graph, bar.
- `Actions/StatCommand.cs`: what every key shares. One file per key beside it.

## macOS sources
- CPU: `host_statistics` `HOST_CPU_LOAD_INFO`, tick deltas between samples.
- Memory used: `host_statistics64` `HOST_VM_INFO64`: (internal − purgeable + wired + compressor pages) × `hw.pagesize`, as Activity Monitor counts it. Total `hw.memsize`.
- Pressure: level from `kern.memorystatus_vm_pressure_level` (1, 2, 4); bar is 100 − `kern.memorystatus_level`.
- Disk: `NSURLVolumeAvailableCapacityForImportantUsageKey` on `/` (Finder's figure, counts purgeable space), via the Objective-C runtime.
- Network: .NET `NetworkInterface` byte counters for `en*` interfaces that are up.
- Busiest app: `ps -Axo pcpu=,rss=,comm=`, grouped by the first `.app` in each path.
- GPU: `IOAccelerator` registry property `PerformanceStatistics` → `Device Utilization %`.
- CPU temp: private HID sensor API (`IOHIDEventSystemClient`, usage page 0xFF00, usage 5), average of sensors named `PMU tdie*`. Checked on an M4 Mac mini.
- Fans: AppleSMC keys `FNum` and `F<n>Ac` (`flt ` on Apple silicon, `fpe2` on Intel).
- Battery: `IOPSCopyPowerSourcesInfo`, the source whose `Type` is `InternalBattery`.
- Internet: .NET `Ping` to 1.1.1.1 every 5 s, in the background. Wi-Fi name, `rssiValue`, `noiseMeasurement` and `transmitRate` from CoreWLAN (`CWWiFiClient`); bars come from signal minus noise. An `en*` interface with a gateway that isn't Wi-Fi counts as Ethernet, and its speed is .NET `NetworkInterface.Speed`.

## Logi SDK gotchas
- Keys are 116×116. `IsWidget = true` removes the caption so the image fills the key.
- One-colour SVGs turn white, so drawings carry an invisible second colour.
- `BitmapBuilder.DrawText` takes a font name as its last argument; `Helvetica Neue Medium` gives the semibold look.
- `package/metadata/LoupedeckPackage.yaml` is YAML: a `: ` inside a value breaks it and the plugin won't load.
