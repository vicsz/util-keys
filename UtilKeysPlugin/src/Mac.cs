namespace Loupedeck.UtilKeysPlugin
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;

    // Calls into macOS: the kernel, Foundation and Wi-Fi. IOKit hardware readings are in Sensors.cs.

    internal static class Mac
    {
        private const String System = "/usr/lib/libSystem.dylib";
        private const String ObjC = "/usr/lib/libobjc.dylib";
        private const Int32 HostCpuLoadInfo = 3;
        private const Int32 HostVmInfo64 = 4;
        private const Int32 VmInfo64Count = 38;

        private static readonly UInt32 Host = mach_host_self();

        static Mac()
        {
            NativeLibrary.Load("/System/Library/Frameworks/Foundation.framework/Foundation");
            NativeLibrary.Load("/System/Library/Frameworks/CoreWLAN.framework/CoreWLAN");
        }

        // Total CPU ticks for all cores since boot: busy (user, system, nice) and idle.
        public static (UInt64 Busy, UInt64 Idle) CpuTicks()
        {
            var ticks = new UInt32[4];
            var count = 4;

            if (host_statistics(Host, HostCpuLoadInfo, ticks, ref count) != 0)
            {
                return (0, 0);
            }

            return ((UInt64)ticks[0] + ticks[1] + ticks[3], ticks[2]);
        }

        // Used memory the way Activity Monitor counts it: app memory (anonymous pages that can't be purged),
        // wired memory, and the pages the compressor holds.
        public static UInt64 MemoryUsed()
        {
            var info = new UInt32[VmInfo64Count];
            var count = VmInfo64Count;

            if (host_statistics64(Host, HostVmInfo64, info, ref count) != 0)
            {
                return 0;
            }

            const Int32 wired = 3, purgeable = 22, compressor = 32, anonymous = 35;
            var pages = (Int64)info[anonymous] - info[purgeable] + info[wired] + info[compressor];
            return (UInt64)Math.Max(0, pages) * SysctlLong("hw.pagesize");
        }

        public static UInt64 MemoryTotal() => SysctlLong("hw.memsize");

        // 1 is normal, 2 is warning, 4 is critical.
        public static Int32 MemoryPressureLevel() => SysctlInt("kern.memorystatus_vm_pressure_level");

        // The share of memory the kernel considers free, 0 to 100. Activity Monitor's pressure is 100 minus this.
        public static Int32 MemoryFreeLevel() => SysctlInt("kern.memorystatus_level");

        // Free space on the startup disk as Finder shows it, which counts purgeable space as free.
        // Falls back to plain free space if Foundation can't answer.
        public static (Int64 Free, Int64 Total) Disk()
        {
            var drive = new global::System.IO.DriveInfo("/");
            var free = VolumeValue("NSURLVolumeAvailableCapacityForImportantUsageKey");
            return (free > 0 ? free : drive.AvailableFreeSpace, drive.TotalSize);
        }

        private static Int64 VolumeValue(String key)
        {
            var pool = objc_autoreleasePoolPush();

            try
            {
                var url = Send(Class("NSURL"), "fileURLWithPath:", NSString("/"));
                var value = IntPtr.Zero;

                if (url == IntPtr.Zero || SendGet(url, Selector("getResourceValue:forKey:error:"), ref value, NSString(key), IntPtr.Zero) == 0 || value == IntPtr.Zero)
                {
                    return 0;
                }

                return SendLong(value, Selector("longLongValue"));
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "Couldn't read " + key);
                return 0;
            }
            finally
            {
                objc_autoreleasePoolPop(pool);
            }
        }

        // The Wi-Fi interface's name (like en1), signal and noise in dBm, and link rate in Mb/s.
        // Signal is 0 when Wi-Fi isn't connected.
        public static (String Name, Int64 Rssi, Int64 Noise, Double Rate) Wifi()
        {
            var pool = objc_autoreleasePoolPush();

            try
            {
                var client = objc_msgSend(Class("CWWiFiClient"), Selector("sharedWiFiClient"));
                var wifi = client == IntPtr.Zero ? IntPtr.Zero : objc_msgSend(client, Selector("interface"));

                if (wifi == IntPtr.Zero)
                {
                    return (null, 0, 0, 0);
                }

                var name = objc_msgSend(wifi, Selector("interfaceName"));
                var text = name == IntPtr.Zero ? IntPtr.Zero : objc_msgSend(name, Selector("UTF8String"));
                return (Marshal.PtrToStringUTF8(text), SendLong(wifi, Selector("rssiValue")), SendLong(wifi, Selector("noiseMeasurement")),
                    SendDouble(wifi, Selector("transmitRate")));
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "Couldn't read Wi-Fi");
                return (null, 0, 0, 0);
            }
            finally
            {
                objc_autoreleasePoolPop(pool);
            }
        }

        private static UInt64 SysctlLong(String name)
        {
            UInt64 value = 0;
            var size = (UIntPtr)8;
            return sysctlbyname(name, ref value, ref size, IntPtr.Zero, UIntPtr.Zero) == 0 ? value : 0;
        }

        private static Int32 SysctlInt(String name)
        {
            var value = 0;
            var size = (UIntPtr)4;
            return sysctlbyname(name, ref value, ref size, IntPtr.Zero, UIntPtr.Zero) == 0 ? value : 0;
        }

        private static IntPtr NSString(String text) => Send(Class("NSString"), "stringWithUTF8String:", Encoding.UTF8.GetBytes(text + "\0"));

        private static IntPtr Class(String name) => objc_getClass(name);

        private static IntPtr Selector(String name) => sel_registerName(name);

        private static IntPtr Send(IntPtr target, String selector, IntPtr argument) => objc_msgSend(target, Selector(selector), argument);

        private static IntPtr Send(IntPtr target, String selector, Byte[] argument) => objc_msgSend(target, Selector(selector), argument);

        [DllImport(System)]
        private static extern UInt32 mach_host_self();

        [DllImport(System)]
        private static extern Int32 host_statistics(UInt32 host, Int32 flavor, [Out] UInt32[] info, ref Int32 count);

        [DllImport(System)]
        private static extern Int32 host_statistics64(UInt32 host, Int32 flavor, [Out] UInt32[] info, ref Int32 count);

        [DllImport(System)]
        private static extern Int32 sysctlbyname(String name, ref UInt64 value, ref UIntPtr size, IntPtr newValue, UIntPtr newSize);

        [DllImport(System)]
        private static extern Int32 sysctlbyname(String name, ref Int32 value, ref UIntPtr size, IntPtr newValue, UIntPtr newSize);

        [DllImport(ObjC)]
        private static extern IntPtr objc_getClass(String name);

        [DllImport(ObjC)]
        private static extern IntPtr sel_registerName(String name);

        [DllImport(ObjC)]
        private static extern IntPtr objc_autoreleasePoolPush();

        [DllImport(ObjC)]
        private static extern void objc_autoreleasePoolPop(IntPtr pool);

        [DllImport(ObjC)]
        private static extern IntPtr objc_msgSend(IntPtr target, IntPtr selector);

        [DllImport(ObjC)]
        private static extern IntPtr objc_msgSend(IntPtr target, IntPtr selector, IntPtr argument);

        [DllImport(ObjC)]
        private static extern IntPtr objc_msgSend(IntPtr target, IntPtr selector, Byte[] argument);

        [DllImport(ObjC, EntryPoint = "objc_msgSend")]
        private static extern Byte SendGet(IntPtr target, IntPtr selector, ref IntPtr value, IntPtr key, IntPtr error);

        [DllImport(ObjC, EntryPoint = "objc_msgSend")]
        private static extern Int64 SendLong(IntPtr target, IntPtr selector);

        [DllImport(ObjC, EntryPoint = "objc_msgSend")]
        private static extern Double SendDouble(IntPtr target, IntPtr selector);
    }
}
