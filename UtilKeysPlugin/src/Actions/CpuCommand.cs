namespace Loupedeck.UtilKeysPlugin
{
    using System;

    // CPU load across all cores, with the last minute as a graph.

    public class CpuCommand : StatCommand
    {
        public CpuCommand()
            : base("CPU", "CPU load across all cores, with the last minute as a graph")
        {
        }

        protected override BitmapImage Draw(PluginImageSize imageSize)
        {
            var cpu = this.Monitor.Cpu;
            var color = cpu > 90 ? KeyArt.Red : cpu > 70 ? KeyArt.Amber : KeyArt.Green;
            var graph = KeyArt.Graph(this.Monitor.CpuHistory.ToArray(), 100, color, Layout.GraphTop, Layout.GraphBottom);
            return KeyArt.Key(imageSize, "CPU", graph, new Line($"{cpu:0}%", Layout.Value, 28, KeyArt.Text, true));
        }
    }
}
