namespace Loupedeck.UtilKeysPlugin
{
    using System;
    using System.Linq;

    // The app using the most CPU right now, helpers included, with its CPU and memory.
    // Checked every 5 seconds, so the graph covers the last 5 minutes.

    public class BusiestAppCommand : StatCommand
    {
        public BusiestAppCommand()
            : base("Busiest app", "The app using the most CPU, with its helpers counted in")
        {
        }

        protected override BitmapImage Draw(PluginImageSize imageSize)
        {
            var app = this.Monitor.TopApp;

            if (app == null)
            {
                return KeyArt.Key(imageSize, "Busiest app", null, new Line("…", 44, 26, KeyArt.Muted));
            }

            var hot = app.Cpu > 100;
            var history = this.Monitor.TopAppHistory.ToArray();
            var graph = KeyArt.Graph(history, Math.Max(100, history.Max()), hot ? KeyArt.Amber : KeyArt.Green, 78, 104);
            var (name, size) = Fit(app.Name);

            return KeyArt.Key(imageSize, "Busiest app", graph,
                new Line(name, 42, size, KeyArt.Text, true),
                new Line($"{app.Cpu:0}% CPU · {MemoryGb(app.Memory)}", 64, 11, hot ? KeyArt.Amber : KeyArt.Muted));
        }

        // Drops the maker's name, then shrinks long names and cuts the longest.
        private static (String Name, Int32 Size) Fit(String name)
        {
            foreach (var maker in new[] { "Google ", "Microsoft " })
            {
                if (name.StartsWith(maker, StringComparison.Ordinal) && name.Length > maker.Length)
                {
                    name = name.Substring(maker.Length);
                }
            }

            return name.Length switch
            {
                <= 7 => (name, 22),
                <= 10 => (name, 17),
                <= 13 => (name, 13),
                _ => (name.Substring(0, 12) + "…", 13),
            };
        }
    }
}
