namespace Loupedeck.UtilKeysPlugin
{
    using System;

    // Ethernet or Wi-Fi and its link speed. When both are connected it shows Ethernet, which macOS uses first,
    // and a tap switches to the other. On Wi-Fi, the symbol's arcs and the number beside it are the signal above
    // the noise, so a strong signal in a noisy room still loses arcs.
    // (The class keeps its first name so keys already on the keypad stay put.)

    public class InternetCommand : StatCommand
    {
        private Boolean _preferWifi;

        public InternetCommand()
            : base("Connection", "Ethernet or Wi-Fi and its link speed. Tap to switch when both are connected")
        {
        }

        protected override void RunCommand(String actionParameter)
        {
            if (this.Monitor.EthernetSpeed != null && this.Monitor.WifiSpeed != null)
            {
                this._preferWifi = !this._preferWifi;
                this.ActionImageChanged();
            }
        }

        protected override BitmapImage Draw(PluginImageSize imageSize)
        {
            var monitor = this.Monitor;
            var showWifi = monitor.WifiSpeed != null && (this._preferWifi || monitor.EthernetSpeed == null);

            if (showWifi)
            {
                var snr = monitor.WifiSnr;
                var (arcs, color) = snr switch
                {
                    >= 40 => (3, KeyArt.Green),
                    >= 25 => (2, KeyArt.Green),
                    >= 15 => (1, KeyArt.Amber),
                    _ => (0, KeyArt.Red),
                };

                return KeyArt.Key(imageSize, "Wi-Fi", KeyArt.WifiSymbol(34, Layout.ZoneMiddle + 9, arcs, color),
                    new Line(LinkSpeed(monitor.WifiSpeed.Value), Layout.SmallValue, 26, KeyArt.Text, true),
                    new Line($"{snr} dB", Layout.ZoneMiddle - 1, 16, color, true, 56, 54));
            }

            if (monitor.EthernetSpeed is Double ethernet)
            {
                return KeyArt.Key(imageSize, "Ethernet", KeyArt.EthernetSocket(58, Layout.ZoneMiddle, KeyArt.Green),
                    new Line(LinkSpeed(ethernet), Layout.SmallValue, 26, KeyArt.Text, true));
            }

            return KeyArt.Key(imageSize, "Connection", null, new Line("Offline", Layout.SmallValue, 22, KeyArt.Red, true));
        }

        private static String LinkSpeed(Double bitsPerSecond) =>
            bitsPerSecond <= 0 ? "—" : bitsPerSecond >= 1e9 ? Number(bitsPerSecond / 1e9) + " Gb/s" : Number(bitsPerSecond / 1e6) + " Mb/s";
    }
}
