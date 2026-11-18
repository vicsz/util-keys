namespace Loupedeck.UtilKeysPlugin
{
    using System;

    // The app using the most CPU right now, helpers included: its name, then its CPU.
    // Checked every 5 seconds.

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
                return KeyArt.Key(imageSize, "Busiest app", null, new Line("…", Layout.Value, 28, KeyArt.Muted));
            }

            var (name, size) = Fit(app.Name);
            return KeyArt.Key(imageSize, "Busiest app", null,
                new Line(name, Layout.Value - 2, size, KeyArt.Text, true),
                new Line($"{app.Cpu:0}%", Layout.ZoneMiddle + 4, 28, app.Cpu > 100 ? KeyArt.Amber : KeyArt.Text, true));
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
