namespace Loupedeck.UtilKeysPlugin
{
    using System;
    using System.Runtime.InteropServices;

    // Stops the Mac from sleeping on its own, the way caffeinate does: it holds a power assertion, the request macOS
    // checks before idle sleep. The screen can still turn off. macOS drops the assertion if the plugin quits or crashes,
    // so the Mac can't get stuck awake.

    public class KeepAwake : IDisposable
    {
        private const String IOKit = "/System/Library/Frameworks/IOKit.framework/IOKit";
        private const String CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private const UInt32 Utf8 = 0x08000100;
        private const UInt32 AssertionOn = 255;

        private readonly Object _lock = new Object();
        private UInt32 _assertion;

        public event Action Changed;

        public Boolean IsOn
        {
            get
            {
                lock (this._lock)
                {
                    return this._assertion != 0;
                }
            }
        }

        public void Toggle()
        {
            lock (this._lock)
            {
                if (this._assertion != 0)
                {
                    this.Release();
                }
                else
                {
                    var type = CFStringCreateWithCString(IntPtr.Zero, "PreventUserIdleSystemSleep", Utf8);
                    var name = CFStringCreateWithCString(IntPtr.Zero, "Util Keys: Keep awake key", Utf8);
                    var result = IOPMAssertionCreateWithName(type, AssertionOn, name, out this._assertion);
                    CFRelease(type);
                    CFRelease(name);

                    if (result != 0)
                    {
                        this._assertion = 0;
                        PluginLog.Error($"Keep awake failed: IOKit error {result:X}");
                    }
                    else
                    {
                        PluginLog.Info("Keep awake on");
                    }
                }
            }

            this.Changed?.Invoke();
        }

        public void Dispose()
        {
            lock (this._lock)
            {
                this.Release();
            }
        }

        private void Release()
        {
            if (this._assertion != 0)
            {
                IOPMAssertionRelease(this._assertion);
                this._assertion = 0;
                PluginLog.Info("Keep awake off");
            }
        }

        [DllImport(IOKit)]
        private static extern Int32 IOPMAssertionCreateWithName(IntPtr type, UInt32 level, IntPtr name, out UInt32 assertion);

        [DllImport(IOKit)]
        private static extern Int32 IOPMAssertionRelease(UInt32 assertion);

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, String text, UInt32 encoding);

        [DllImport(CoreFoundation)]
        private static extern void CFRelease(IntPtr reference);
    }
}
