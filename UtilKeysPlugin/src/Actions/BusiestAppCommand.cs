namespace Loupedeck.UtilKeysPlugin
{
    using System;
    using System.Diagnostics;

    // The app using the most CPU right now, helpers included: its name, then its icon inside a ring and its CPU.
    // The ring fills at one full core (100%) and turns amber past it. A process that isn't part of an app gets a
    // plain terminal tile. Checked every 5 seconds. A tap opens Activity Monitor.

    public class BusiestAppCommand : StatCommand
    {
        private const Int32 RingX = 30;
        private const Int32 RingRadius = 18;

        // macOS icons carry a transparent margin, so they're drawn larger than the tile to look the same size.
        private const Int32 IconSize = 30;
        private const Int32 TileSize = 24;

        public BusiestAppCommand()
            : base("Busiest app", "The app using the most CPU, with its helpers counted in. Tap to open Activity Monitor")
        {
        }

        // Activity Monitor can't be told which process to select, so it opens on whatever tab and sort it had last.
        protected override void RunCommand(String actionParameter)
        {
            try
            {
                Process.Start("/usr/bin/open", "-b com.apple.ActivityMonitor")?.Dispose();
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "Couldn't open Activity Monitor");
            }
        }

        protected override BitmapImage Draw(PluginImageSize imageSize)
        {
            var app = this.Monitor.TopApp;

            if (app == null)
            {
                return KeyArt.Key(imageSize, "Busiest app", null, new Line("…", Layout.Value, 28, KeyArt.Muted));
            }

            var y = Layout.ZoneMiddle;
            var color = app.Cpu > 100 ? KeyArt.Amber : KeyArt.Green;
            var ring = KeyArt.Ring(RingX, y, RingRadius, app.Cpu / 100, color);
            var icon = this.Monitor.TopAppIcon;
            var (name, size) = Fit(app.Name);
            var lines = new[]
            {
                new Line(name, Layout.Value - 2, size, KeyArt.Text, true),
                new Line($"{app.Cpu:0}%", y - 1, 21, app.Cpu > 100 ? KeyArt.Amber : KeyArt.Text, true, 50, 62),
            };

            return icon == null
                ? KeyArt.Key(imageSize, "Busiest app", ring + KeyArt.TerminalTile(RingX - (TileSize / 2), y - (TileSize / 2), TileSize), lines)
                : KeyArt.Key(imageSize, "Busiest app", ring, (icon, RingX - (IconSize / 2), y - (IconSize / 2), IconSize), lines);
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
