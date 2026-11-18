namespace Loupedeck.UtilKeysPlugin
{
    using System;
    using System.Linq;

    // CPU temperature, with the last two minutes as a graph.

    public class TemperatureCommand : StatCommand
    {
        public TemperatureCommand()
            : base("CPU temp", "CPU temperature, with a graph")
        {
        }

        protected override BitmapImage Draw(PluginImageSize imageSize)
        {
            var monitor = this.Monitor;
            if (monitor.CpuTemperature is not Double celsius)
            {
                return KeyArt.Key(imageSize, "CPU temp", null, new Line("—", Layout.Value, 28, KeyArt.Muted));
            }

            // Apple silicon idles around 35–50° and runs hot under load by design, so amber starts at 80 and red at 95.
            var color = celsius >= 95 ? KeyArt.Red : celsius >= 80 ? KeyArt.Amber : KeyArt.Green;
            var history = monitor.TemperatureHistory.ToArray().Where(value => value > 0).ToArray();
            var low = Math.Min(30, history.DefaultIfEmpty(30).Min());
            var graph = KeyArt.Graph(history.Select(value => value - low).ToArray(), 100 - low, color, Layout.GraphTop, Layout.GraphBottom);

            return KeyArt.Key(imageSize, "CPU temp", graph,
                new Line($"{celsius:0}°", Layout.Value, 28, KeyArt.Text, true));
        }
    }
}
