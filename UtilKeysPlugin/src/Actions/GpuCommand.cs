namespace Loupedeck.UtilKeysPlugin
{
    using System;

    // GPU load, with the last minute as a graph.

    public class GpuCommand : StatCommand
    {
        public GpuCommand()
            : base("GPU", "GPU load, with the last minute as a graph")
        {
        }

        protected override BitmapImage Draw(PluginImageSize imageSize)
        {
            if (this.Monitor.Gpu is not Double gpu)
            {
                return KeyArt.Key(imageSize, "GPU", null, new Line("—", Layout.Value, 28, KeyArt.Muted));
            }

            var color = gpu > 90 ? KeyArt.Red : gpu > 70 ? KeyArt.Amber : KeyArt.Purple;
            var graph = KeyArt.Graph(this.Monitor.GpuHistory.ToArray(), 100, color, Layout.GraphTop, Layout.GraphBottom);
            return KeyArt.Key(imageSize, "GPU", graph, new Line($"{gpu:0}%", Layout.Value, 28, KeyArt.Text, true));
        }
    }
}
