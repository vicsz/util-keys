namespace Loupedeck.UtilKeysPlugin
{
    using System;
    using System.Linq;

    // Download and upload speeds, each with its own graph of the last minute. Each graph scales to its own peak.

    public class NetworkCommand : StatCommand
    {
        // Below this a graph stays flat, so idle chatter doesn't look like traffic.
        private const Double MinScale = 50e3;

        public NetworkCommand()
            : base("Network", "Download and upload speeds, with the last minute as graphs")
        {
        }

        protected override BitmapImage Draw(PluginImageSize imageSize)
        {
            var monitor = this.Monitor;
            var down = monitor.DownHistory.ToArray();
            var up = monitor.UpHistory.ToArray();
            var graphs = KeyArt.Graph(down, Math.Max(MinScale, down.DefaultIfEmpty().Max()), KeyArt.Blue, 74, Layout.GraphBottom) +
                KeyArt.Graph(up, Math.Max(MinScale, up.DefaultIfEmpty().Max()), KeyArt.Purple, 88, Layout.GraphBottom);

            return KeyArt.Key(imageSize, "Network", graphs,
                new Line("↓ " + Speed(monitor.Down), 37, 15, KeyArt.Blue, true),
                new Line("↑ " + Speed(monitor.Up), 57, 15, KeyArt.Purple, true));
        }
    }
}
