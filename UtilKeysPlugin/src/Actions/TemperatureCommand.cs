namespace Loupedeck.UtilKeysPlugin
{
    using System;
    using System.Globalization;
    using System.Linq;

    // CPU temperature and fan speed, with the last two minutes of temperature as a graph.

    public class TemperatureCommand : StatCommand
    {
        public TemperatureCommand()
            : base("CPU temp", "CPU temperature and fan speed")
        {
        }

        protected override BitmapImage Draw(PluginImageSize imageSize)
        {
            var monitor = this.Monitor;
            var fans = monitor.Fan is Double rpm ? "fans " + rpm.ToString("#,0", CultureInfo.InvariantCulture) + " rpm" : "no fans";

            if (monitor.CpuTemperature is not Double celsius)
            {
                return KeyArt.Key(imageSize, "CPU temp", null, new Line("—", 42, 28, KeyArt.Muted), new Line(fans, 66, 11, KeyArt.Muted));
            }

            // Apple silicon idles around 35–50° and runs hot under load by design, so amber starts at 80 and red at 95.
            var color = celsius >= 95 ? KeyArt.Red : celsius >= 80 ? KeyArt.Amber : KeyArt.Green;
            var history = monitor.TemperatureHistory.ToArray().Where(value => value > 0).ToArray();
            var low = Math.Min(30, history.DefaultIfEmpty(30).Min());
            var graph = KeyArt.Graph(history.Select(value => value - low).ToArray(), 100 - low, color, 78, 104);

            return KeyArt.Key(imageSize, "CPU temp", graph,
                new Line($"{celsius:0}°", 42, 28, KeyArt.Text, true),
                new Line(fans, 66, 11, KeyArt.Muted));
        }
    }
}
