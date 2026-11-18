namespace Loupedeck.UtilKeysPlugin
{
    using System;
    using System.Linq;

    // Disk read and write speeds, each with its own graph of the last minute, laid out like Network.

    public class DiskActivityCommand : StatCommand
    {
        // Below this a graph stays flat, so background trickle doesn't look like work.
        private const Double MinScale = 1e6;

        public DiskActivityCommand()
            : base("Disk activity", "Disk read and write speeds, with the last minute as graphs")
        {
        }

        protected override BitmapImage Draw(PluginImageSize imageSize)
        {
            var monitor = this.Monitor;
            var read = monitor.DiskReadHistory.ToArray();
            var write = monitor.DiskWriteHistory.ToArray();
            var graphs = KeyArt.Graph(read, Math.Max(MinScale, read.DefaultIfEmpty().Max()), KeyArt.Blue, 74, Layout.GraphBottom) +
                KeyArt.Graph(write, Math.Max(MinScale, write.DefaultIfEmpty().Max()), KeyArt.Purple, 88, Layout.GraphBottom);

            return KeyArt.Key(imageSize, "Disk", graphs,
                new Line("R " + Speed(monitor.DiskRead), 37, 15, KeyArt.Blue, true),
                new Line("W " + Speed(monitor.DiskWrite), 57, 15, KeyArt.Purple, true));
        }
    }
}
