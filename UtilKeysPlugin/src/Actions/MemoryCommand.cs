namespace Loupedeck.UtilKeysPlugin
{
    using System;

    // Memory used of total. The bar is memory pressure, which says more than the GB figure: macOS fills
    // spare memory with cache on purpose.

    public class MemoryCommand : StatCommand
    {
        public MemoryCommand()
            : base("Memory", "Memory used of total, with a memory pressure bar")
        {
        }

        protected override BitmapImage Draw(PluginImageSize imageSize)
        {
            var monitor = this.Monitor;
            var (color, word) = monitor.PressureLevel switch
            {
                >= 4 => (KeyArt.Red, "critical"),
                2 => (KeyArt.Amber, "high"),
                _ => (KeyArt.Green, "ok"),
            };

            var total = Math.Round(monitor.MemoryTotal / 1073741824.0);
            return KeyArt.Key(imageSize, "Memory", KeyArt.Bar(monitor.Pressure / 100.0, color, 86),
                new Line(MemoryGb(monitor.MemoryUsed), 42, 26, KeyArt.Text, true),
                new Line($"of {total:0} · pressure {word}", 68, 11, KeyArt.Muted));
        }
    }
}
