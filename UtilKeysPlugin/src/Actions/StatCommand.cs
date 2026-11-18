namespace Loupedeck.UtilKeysPlugin
{
    using System;
    using System.Globalization;

    // What every key shares: it fills the key, redraws on each reading and keeps the monitor running while it shows.
    // Taps do nothing for now.

    public abstract class StatCommand : PluginDynamicCommand
    {
        protected StatCommand(String displayName, String description)
            : base(displayName, description, "System")
        {
            this.IsWidget = true;
        }

        protected SystemMonitor Monitor => ((UtilKeysPlugin)this.Plugin).Monitor;

        protected override Boolean OnLoad()
        {
            this.Monitor.Changed += this.Redraw;
            return true;
        }

        protected override Boolean OnUnload()
        {
            this.Monitor.Changed -= this.Redraw;
            return true;
        }

        protected override void RunCommand(String actionParameter)
        {
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            this.Monitor.Use();
            return this.Monitor.HasReadings ? this.Draw(imageSize) : KeyArt.Key(imageSize, this.DisplayName, null, new Line("…", Layout.Value, 28, KeyArt.Muted));
        }

        protected abstract BitmapImage Draw(PluginImageSize imageSize);

        // Memory in binary GB, as Activity Monitor shows it.
        protected static String MemoryGb(Double bytes) => Number(bytes / 1073741824.0) + " GB";

        // Disk sizes in decimal units, as Finder shows them.
        protected static String DiskSize(Double bytes) =>
            bytes >= 1e12 ? Number(bytes / 1e12) + " TB" : Number(bytes / 1e9) + " GB";

        protected static String Speed(Double bytesPerSecond) =>
            bytesPerSecond >= 1e9 ? Number(bytesPerSecond / 1e9) + " GB/s" : bytesPerSecond >= 1e6 ? Number(bytesPerSecond / 1e6) + " MB/s" : Number(bytesPerSecond / 1e3) + " KB/s";

        // One decimal below 10, whole numbers above.
        protected static String Number(Double value) =>
            value.ToString(value < 9.95 ? "0.#" : "0", CultureInfo.InvariantCulture);

        private void Redraw() => this.ActionImageChanged();
    }
}
