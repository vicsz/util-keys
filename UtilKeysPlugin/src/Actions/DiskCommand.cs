namespace Loupedeck.UtilKeysPlugin
{
    using System;

    // Free space on the startup disk, as Finder counts it. The bar is the space used.

    public class DiskCommand : StatCommand
    {
        public DiskCommand()
            : base("SSD", "Free space on the startup disk, as Finder shows it")
        {
        }

        protected override BitmapImage Draw(PluginImageSize imageSize)
        {
            var monitor = this.Monitor;
            var free = monitor.DiskTotal > 0 ? (Double)monitor.DiskFree / monitor.DiskTotal : 1;
            var color = free < 0.05 ? KeyArt.Red : free < 0.15 ? KeyArt.Amber : KeyArt.Blue;

            return KeyArt.Key(imageSize, "SSD", KeyArt.Bar(1 - free, color, 86),
                new Line(DiskSize(monitor.DiskFree), 42, 26, KeyArt.Text, true),
                new Line("free of " + DiskSize(monitor.DiskTotal), 68, 11, KeyArt.Muted));
        }
    }
}
