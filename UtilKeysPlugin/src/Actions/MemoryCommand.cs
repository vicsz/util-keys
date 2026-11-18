namespace Loupedeck.UtilKeysPlugin
{
    using System;

    // Memory used. The bar is memory pressure, which says more than the GB figure: macOS fills spare memory with
    // cache on purpose. Its colour is the pressure level: green, amber, red.

    public class MemoryCommand : StatCommand
    {
        public MemoryCommand()
            : base("Memory", "Memory used, with a memory pressure bar")
        {
        }

        protected override BitmapImage Draw(PluginImageSize imageSize)
        {
            var monitor = this.Monitor;
            var color = monitor.PressureLevel switch
            {
                >= 4 => KeyArt.Red,
                2 => KeyArt.Amber,
                _ => KeyArt.Green,
            };

            return KeyArt.Key(imageSize, "Memory", KeyArt.Bar(monitor.Pressure / 100.0, color, Layout.BarTop),
                new Line(MemoryGb(monitor.MemoryUsed), Layout.SmallValue, 26, KeyArt.Text, true));
        }
    }
}
