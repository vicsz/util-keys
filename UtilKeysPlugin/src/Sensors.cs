namespace Loupedeck.UtilKeysPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;

    // Reads hardware through IOKit: GPU load, temperatures and the battery.
    // Temperatures use the HID sensor API that Apple's own tools use. It's private, so an update could change it.

    internal static class Sensors
    {
        private const String IOKit = "/System/Library/Frameworks/IOKit.framework/IOKit";
        private const String CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private const UInt32 Utf8 = 0x08000100;
        private const Int32 SInt32Type = 3;
        private const Int32 SInt64Type = 4;
        private const Int32 Float64Type = 6;
        private const Int64 TemperatureEvent = 15;

        private static IntPtr _hidClient;

        // GPU load, 0 to 100, or null if the GPU doesn't report it.
        public static Double? GpuLoad()
        {
            var gpu = IOServiceGetMatchingService(0, IOServiceMatching("IOAccelerator"));

            if (gpu == 0)
            {
                return null;
            }

            try
            {
                using var key = new CFString("PerformanceStatistics");
                var stats = IORegistryEntryCreateCFProperty(gpu, key.Handle, IntPtr.Zero, 0);

                try
                {
                    return stats == IntPtr.Zero ? null : DictionaryNumber(stats, "Device Utilization %");
                }
                finally
                {
                    Release(stats);
                }
            }
            finally
            {
                IOObjectRelease(gpu);
            }
        }

        // The average CPU die temperature in °C, or null if there are no CPU sensors.
        public static Double? CpuTemperature()
        {
            var readings = Temperatures()
                .Where(sensor => sensor.Name.StartsWith("PMU tdie", StringComparison.Ordinal) && sensor.Celsius > 0 && sensor.Celsius < 150)
                .Select(sensor => sensor.Celsius)
                .ToList();

            return readings.Count > 0 ? readings.Average() : null;
        }

        // Every temperature sensor, by name.
        public static List<(String Name, Double Celsius)> Temperatures()
        {
            var sensors = new List<(String, Double)>();

            if (_hidClient == IntPtr.Zero)
            {
                _hidClient = IOHIDEventSystemClientCreate(IntPtr.Zero);

                // Usage page 0xFF00, usage 5: Apple's temperature sensors.
                using var page = new CFString("PrimaryUsagePage");
                using var usage = new CFString("PrimaryUsage");
                var keys = new[] { page.Handle, usage.Handle };
                var values = new[] { Number(0xFF00), Number(5) };
                var matching = CFDictionaryCreate(IntPtr.Zero, keys, values, 2, CFExport("kCFTypeDictionaryKeyCallBacks"), CFExport("kCFTypeDictionaryValueCallBacks"));
                IOHIDEventSystemClientSetMatching(_hidClient, matching);
                Release(matching);
                Release(values[0]);
                Release(values[1]);
            }

            var services = IOHIDEventSystemClientCopyServices(_hidClient);

            if (services == IntPtr.Zero)
            {
                return sensors;
            }

            using var product = new CFString("Product");

            for (var i = 0L; i < CFArrayGetCount(services); i++)
            {
                var service = CFArrayGetValueAtIndex(services, i);
                var name = IOHIDServiceClientCopyProperty(service, product.Handle);
                var reading = IOHIDServiceClientCopyEvent(service, TemperatureEvent, 0, 0);

                if (name != IntPtr.Zero && reading != IntPtr.Zero)
                {
                    sensors.Add((ReadString(name), IOHIDEventGetFloatValue(reading, (Int32)(TemperatureEvent << 16))));
                }

                Release(name);
                Release(reading);
            }

            Release(services);
            return sensors;
        }

        // The internal battery, or null if there isn't one.
        public static BatteryState Battery()
        {
            var info = IOPSCopyPowerSourcesInfo();

            if (info == IntPtr.Zero)
            {
                return null;
            }

            var list = IOPSCopyPowerSourcesList(info);

            try
            {
                for (var i = 0L; list != IntPtr.Zero && i < CFArrayGetCount(list); i++)
                {
                    var source = IOPSGetPowerSourceDescription(info, CFArrayGetValueAtIndex(list, i));

                    if (source == IntPtr.Zero || DictionaryString(source, "Type") != "InternalBattery")
                    {
                        continue;
                    }

                    var current = DictionaryNumber(source, "Current Capacity") ?? 0;
                    var max = DictionaryNumber(source, "Max Capacity") ?? 100;
                    var onPower = DictionaryString(source, "Power Source State") == "AC Power";
                    var charging = DictionaryBool(source, "Is Charging");
                    var minutes = DictionaryNumber(source, charging ? "Time to Full Charge" : "Time to Empty") ?? -1;

                    return new BatteryState
                    {
                        Percent = max > 0 ? 100 * current / max : 0,
                        IsCharging = charging,
                        OnPower = onPower,
                        Minutes = minutes > 0 ? (Int32)minutes : null,
                    };
                }

                return null;
            }
            finally
            {
                Release(list);
                Release(info);
            }
        }

        private static IntPtr CFExport(String name) => NativeLibrary.GetExport(NativeLibrary.Load(CoreFoundation), name);

        private static IntPtr Number(Int32 value) => CFNumberCreate(IntPtr.Zero, SInt32Type, ref value);

        private static Double? DictionaryNumber(IntPtr dictionary, String key)
        {
            using var name = new CFString(key);
            var value = CFDictionaryGetValue(dictionary, name.Handle);

            if (value == IntPtr.Zero || CFGetTypeID(value) != CFNumberGetTypeID())
            {
                return null;
            }

            if (CFNumberIsFloatType(value))
            {
                return CFNumberGetValue(value, Float64Type, out Double real) ? real : null;
            }

            return CFNumberGetValue(value, SInt64Type, out Int64 whole) ? whole : null;
        }

        private static String DictionaryString(IntPtr dictionary, String key)
        {
            using var name = new CFString(key);
            var value = CFDictionaryGetValue(dictionary, name.Handle);
            return value == IntPtr.Zero || CFGetTypeID(value) != CFStringGetTypeID() ? null : ReadString(value);
        }

        private static Boolean DictionaryBool(IntPtr dictionary, String key)
        {
            using var name = new CFString(key);
            var value = CFDictionaryGetValue(dictionary, name.Handle);
            return value != IntPtr.Zero && CFGetTypeID(value) == CFBooleanGetTypeID() && CFBooleanGetValue(value);
        }

        private static String ReadString(IntPtr text)
        {
            if (CFGetTypeID(text) != CFStringGetTypeID())
            {
                return String.Empty;
            }

            var buffer = new Byte[256];
            return CFStringGetCString(text, buffer, buffer.Length, Utf8) ? Encoding.UTF8.GetString(buffer).TrimEnd('\0') : String.Empty;
        }

        private static void Release(IntPtr reference)
        {
            if (reference != IntPtr.Zero)
            {
                CFRelease(reference);
            }
        }

        private sealed class CFString : IDisposable
        {
            public CFString(String text) => this.Handle = CFStringCreateWithCString(IntPtr.Zero, text, Utf8);

            public IntPtr Handle { get; }

            public void Dispose() => Release(this.Handle);
        }

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, String text, UInt32 encoding);

        [DllImport(CoreFoundation)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern Boolean CFStringGetCString(IntPtr text, Byte[] buffer, Int64 size, UInt32 encoding);

        [DllImport(CoreFoundation)]
        private static extern void CFRelease(IntPtr reference);

        [DllImport(CoreFoundation)]
        private static extern UInt64 CFGetTypeID(IntPtr reference);

        [DllImport(CoreFoundation)]
        private static extern UInt64 CFStringGetTypeID();

        [DllImport(CoreFoundation)]
        private static extern UInt64 CFNumberGetTypeID();

        [DllImport(CoreFoundation)]
        private static extern UInt64 CFBooleanGetTypeID();

        [DllImport(CoreFoundation)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern Boolean CFBooleanGetValue(IntPtr boolean);

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFNumberCreate(IntPtr allocator, Int32 type, ref Int32 value);

        [DllImport(CoreFoundation)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern Boolean CFNumberIsFloatType(IntPtr number);

        [DllImport(CoreFoundation)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern Boolean CFNumberGetValue(IntPtr number, Int32 type, out Double value);

        [DllImport(CoreFoundation)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern Boolean CFNumberGetValue(IntPtr number, Int32 type, out Int64 value);

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFDictionaryCreate(IntPtr allocator, IntPtr[] keys, IntPtr[] values, Int64 count, IntPtr keyCallbacks, IntPtr valueCallbacks);

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFDictionaryGetValue(IntPtr dictionary, IntPtr key);

        [DllImport(CoreFoundation)]
        private static extern Int64 CFArrayGetCount(IntPtr array);

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, Int64 index);

        [DllImport(IOKit)]
        private static extern IntPtr IOServiceMatching(String name);

        [DllImport(IOKit)]
        private static extern UInt32 IOServiceGetMatchingService(UInt32 mainPort, IntPtr matching);

        [DllImport(IOKit)]
        private static extern IntPtr IORegistryEntryCreateCFProperty(UInt32 entry, IntPtr key, IntPtr allocator, UInt32 options);

        [DllImport(IOKit)]
        private static extern Int32 IOObjectRelease(UInt32 entry);

        [DllImport(IOKit)]
        private static extern IntPtr IOHIDEventSystemClientCreate(IntPtr allocator);

        [DllImport(IOKit)]
        private static extern Int32 IOHIDEventSystemClientSetMatching(IntPtr client, IntPtr matching);

        [DllImport(IOKit)]
        private static extern IntPtr IOHIDEventSystemClientCopyServices(IntPtr client);

        [DllImport(IOKit)]
        private static extern IntPtr IOHIDServiceClientCopyProperty(IntPtr service, IntPtr key);

        [DllImport(IOKit)]
        private static extern IntPtr IOHIDServiceClientCopyEvent(IntPtr service, Int64 type, Int32 options, Int64 timestamp);

        [DllImport(IOKit)]
        private static extern Double IOHIDEventGetFloatValue(IntPtr eventRef, Int32 field);

        [DllImport(IOKit)]
        private static extern IntPtr IOPSCopyPowerSourcesInfo();

        [DllImport(IOKit)]
        private static extern IntPtr IOPSCopyPowerSourcesList(IntPtr blob);

        [DllImport(IOKit)]
        private static extern IntPtr IOPSGetPowerSourceDescription(IntPtr blob, IntPtr source);
    }

    public class BatteryState
    {
        public Double Percent { get; set; }

        public Boolean IsCharging { get; set; }

        public Boolean OnPower { get; set; }

        // Minutes to empty, or to full while charging. Null while macOS is still working it out.
        public Int32? Minutes { get; set; }
    }
}
