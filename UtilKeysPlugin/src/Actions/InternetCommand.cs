namespace Loupedeck.UtilKeysPlugin
{
    using System;

    // Round trip to the internet (a ping to 1.1.1.1 every 5 seconds), and the connection with its link speed.
    // On Wi-Fi the bars are signal above noise, so a strong signal in a noisy room still loses bars.

    public class InternetCommand : StatCommand
    {
        public InternetCommand()
            : base("Internet", "Ping time to the internet, and the connection's speed and signal")
        {
        }

        protected override BitmapImage Draw(PluginImageSize imageSize)
        {
            var monitor = this.Monitor;
            var bars = monitor.Link switch
            {
                "Ethernet" => 4,
                "Wi-Fi" => monitor.WifiSnr switch { >= 40 => 4, >= 25 => 3, >= 15 => 2, _ => 1 },
                _ => 0,
            };
            var link = monitor.Link == null ? "not connected"
                : monitor.LinkSpeed > 0 ? $"{monitor.Link} · {LinkSpeed(monitor.LinkSpeed)}"
                : monitor.Link;

            if (!monitor.HasPinged)
            {
                return KeyArt.Key(imageSize, "Internet", KeyArt.SignalBars(bars, KeyArt.Muted), new Line("…", 42, 26, KeyArt.Muted), new Line(link, 66, 11, KeyArt.Muted));
            }

            var (value, color) = monitor.Ping switch
            {
                Double ms when ms < 60 => ($"{ms:0} ms", KeyArt.Green),
                Double ms when ms < 150 => ($"{ms:0} ms", KeyArt.Amber),
                Double ms => ($"{ms:0} ms", KeyArt.Red),
                _ => ("Offline", KeyArt.Red),
            };

            return KeyArt.Key(imageSize, "Internet", KeyArt.SignalBars(bars, color),
                new Line(value, 42, value.Length > 6 ? 22 : 26, color, true),
                new Line(link, 66, 11, KeyArt.Muted));
        }

        private static String LinkSpeed(Double bitsPerSecond) =>
            bitsPerSecond >= 1e9 ? Number(bitsPerSecond / 1e9) + " Gb/s" : Number(bitsPerSecond / 1e6) + " Mb/s";
    }
}
