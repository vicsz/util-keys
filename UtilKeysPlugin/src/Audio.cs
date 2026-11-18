namespace Loupedeck.UtilKeysPlugin
{
    using System;
    using System.Runtime.InteropServices;

    // The default microphone through CoreAudio: is there one, is an app recording from it, is it muted.
    // Muting uses the device's own mute switch, or its input volume when it has no switch. Only the default
    // microphone is muted, so an app set to a different microphone still hears that one.

    internal static class Audio
    {
        private const String CoreAudio = "/System/Library/Frameworks/CoreAudio.framework/CoreAudio";
        private const UInt32 SystemObject = 1;
        private const Single UnmuteVolume = 0.75f;

        private static readonly UInt32 Global = Code("glob");
        private static readonly UInt32 Input = Code("inpt");

        // The input volume before we muted by volume, so unmuting puts it back.
        private static Single? _volumeBeforeMute;

        public static MicState Mic()
        {
            var device = DefaultInput();

            if (device == 0)
            {
                return new MicState();
            }

            return new MicState
            {
                Exists = true,
                InUse = GetUInt(device, Code("gone"), Global, 0) is UInt32 running && running != 0,
                Muted = IsMuted(device),
            };
        }

        public static void SetMicMuted(Boolean muted)
        {
            var device = DefaultInput();

            if (device == 0)
            {
                return;
            }

            var mute = Code("mute");

            if (IsSettable(device, mute, Input, 0))
            {
                SetUInt(device, mute, Input, 0, muted ? 1u : 0u);
                return;
            }

            // No mute switch: set the input volume on the main element, or on each channel.
            foreach (var element in VolumeElements(device))
            {
                if (muted)
                {
                    _volumeBeforeMute ??= GetFloat(device, Code("volm"), Input, element);
                    SetFloat(device, Code("volm"), Input, element, 0);
                }
                else
                {
                    var volume = _volumeBeforeMute is Single before && before > 0 ? before : UnmuteVolume;
                    SetFloat(device, Code("volm"), Input, element, volume);
                }
            }

            if (!muted)
            {
                _volumeBeforeMute = null;
            }
        }

        private static Boolean IsMuted(UInt32 device)
        {
            if (GetUInt(device, Code("mute"), Input, 0) is UInt32 mute)
            {
                return mute != 0;
            }

            foreach (var element in VolumeElements(device))
            {
                return GetFloat(device, Code("volm"), Input, element) is Single volume && volume <= 0;
            }

            return false;
        }

        // The elements that carry input volume: the main one (0) if it has it, otherwise channels 1 and 2.
        private static UInt32[] VolumeElements(UInt32 device)
        {
            if (IsSettable(device, Code("volm"), Input, 0))
            {
                return new UInt32[] { 0 };
            }

            return IsSettable(device, Code("volm"), Input, 1)
                ? (IsSettable(device, Code("volm"), Input, 2) ? new UInt32[] { 1, 2 } : new UInt32[] { 1 })
                : Array.Empty<UInt32>();
        }

        private static UInt32 DefaultInput() => GetUInt(SystemObject, Code("dIn "), Global, 0) ?? 0;

        private static UInt32 Code(String fourCharacters) =>
            ((UInt32)fourCharacters[0] << 24) | ((UInt32)fourCharacters[1] << 16) | ((UInt32)fourCharacters[2] << 8) | fourCharacters[3];

        private static Boolean IsSettable(UInt32 device, UInt32 selector, UInt32 scope, UInt32 element)
        {
            var address = new Address(selector, scope, element);
            return AudioObjectHasProperty(device, ref address) != 0 && AudioObjectIsPropertySettable(device, ref address, out var settable) == 0 && settable != 0;
        }

        private static UInt32? GetUInt(UInt32 device, UInt32 selector, UInt32 scope, UInt32 element)
        {
            var address = new Address(selector, scope, element);
            var size = (UInt32)sizeof(UInt32);

            if (AudioObjectHasProperty(device, ref address) == 0)
            {
                return null;
            }

            return AudioObjectGetPropertyData(device, ref address, 0, IntPtr.Zero, ref size, out UInt32 value) == 0 ? value : null;
        }

        private static Single? GetFloat(UInt32 device, UInt32 selector, UInt32 scope, UInt32 element)
        {
            var address = new Address(selector, scope, element);
            var size = (UInt32)sizeof(Single);

            if (AudioObjectHasProperty(device, ref address) == 0)
            {
                return null;
            }

            return AudioObjectGetPropertyData(device, ref address, 0, IntPtr.Zero, ref size, out Single value) == 0 ? value : null;
        }

        private static void SetUInt(UInt32 device, UInt32 selector, UInt32 scope, UInt32 element, UInt32 value)
        {
            var address = new Address(selector, scope, element);
            var result = AudioObjectSetPropertyData(device, ref address, 0, IntPtr.Zero, sizeof(UInt32), ref value);

            if (result != 0)
            {
                PluginLog.Warning($"Couldn't set the microphone's mute: CoreAudio error {result}");
            }
        }

        private static void SetFloat(UInt32 device, UInt32 selector, UInt32 scope, UInt32 element, Single value)
        {
            var address = new Address(selector, scope, element);
            var result = AudioObjectSetPropertyData(device, ref address, 0, IntPtr.Zero, sizeof(Single), ref value);

            if (result != 0)
            {
                PluginLog.Warning($"Couldn't set the microphone's volume: CoreAudio error {result}");
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct Address
        {
            public readonly UInt32 Selector;
            public readonly UInt32 Scope;
            public readonly UInt32 Element;

            public Address(UInt32 selector, UInt32 scope, UInt32 element)
            {
                this.Selector = selector;
                this.Scope = scope;
                this.Element = element;
            }
        }

        [DllImport(CoreAudio)]
        private static extern Byte AudioObjectHasProperty(UInt32 objectId, ref Address address);

        [DllImport(CoreAudio)]
        private static extern Int32 AudioObjectIsPropertySettable(UInt32 objectId, ref Address address, out Byte settable);

        [DllImport(CoreAudio)]
        private static extern Int32 AudioObjectGetPropertyData(UInt32 objectId, ref Address address, UInt32 qualifierSize, IntPtr qualifier, ref UInt32 size, out UInt32 data);

        [DllImport(CoreAudio)]
        private static extern Int32 AudioObjectGetPropertyData(UInt32 objectId, ref Address address, UInt32 qualifierSize, IntPtr qualifier, ref UInt32 size, out Single data);

        [DllImport(CoreAudio)]
        private static extern Int32 AudioObjectSetPropertyData(UInt32 objectId, ref Address address, UInt32 qualifierSize, IntPtr qualifier, UInt32 size, ref UInt32 data);

        [DllImport(CoreAudio)]
        private static extern Int32 AudioObjectSetPropertyData(UInt32 objectId, ref Address address, UInt32 qualifierSize, IntPtr qualifier, UInt32 size, ref Single data);
    }

    public class MicState
    {
        public Boolean Exists { get; set; }

        // An app is recording from it right now.
        public Boolean InUse { get; set; }

        public Boolean Muted { get; set; }

        public override String ToString() => !this.Exists ? "none" : $"{(this.Muted ? "muted" : "on")}{(this.InUse ? ", in use" : String.Empty)}";
    }
}
