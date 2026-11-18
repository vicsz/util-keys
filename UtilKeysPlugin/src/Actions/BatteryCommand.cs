namespace Loupedeck.UtilKeysPlugin
{
    using System;

    // Battery charge and time left, or time to full while charging.

    public class BatteryCommand : StatCommand
    {
        public BatteryCommand()
            : base("Battery", "Battery charge and time left")
        {
        }

        protected override BitmapImage Draw(PluginImageSize imageSize)
        {
            if (this.Monitor.Battery is not BatteryState battery)
            {
                return KeyArt.Key(imageSize, "Battery", KeyArt.Battery(0, KeyArt.Muted), new Line("—", 42, 28, KeyArt.Muted), new Line("no battery", 102, 11, KeyArt.Muted));
            }

            var color = battery.IsCharging || battery.Percent >= 20 ? KeyArt.Green : battery.Percent >= 10 ? KeyArt.Amber : KeyArt.Red;
            var time = battery.Minutes is Int32 minutes ? $"{minutes / 60}h {minutes % 60:00}m" : null;
            var line = battery.IsCharging ? (time != null ? time + " to full" : "charging")
                : battery.OnPower ? "on power"
                : time != null ? time + " left" : "working it out…";

            return KeyArt.Key(imageSize, "Battery", KeyArt.Battery(battery.Percent / 100, color),
                new Line($"{battery.Percent:0}%", 42, 28, KeyArt.Text, true),
                new Line(line, 102, 11, KeyArt.Muted));
        }
    }
}
