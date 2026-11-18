namespace Loupedeck.UtilKeysPlugin
{
    using System;

    // Battery charge, with a battery icon. Beside the icon: time left, or time to full while charging.

    public class BatteryCommand : StatCommand
    {
        private const Int32 IconTop = Layout.ZoneMiddle - 11;

        public BatteryCommand()
            : base("Battery", "Battery charge and time left")
        {
        }

        protected override BitmapImage Draw(PluginImageSize imageSize)
        {
            if (this.Monitor.Battery is not BatteryState battery)
            {
                return KeyArt.Key(imageSize, "Battery", KeyArt.Battery(32, IconTop, 0, KeyArt.Muted, false), new Line("—", Layout.Value, 28, KeyArt.Muted));
            }

            var color = battery.IsCharging || battery.Percent >= 20 ? KeyArt.Green : battery.Percent >= 10 ? KeyArt.Amber : KeyArt.Red;
            var value = new Line($"{battery.Percent:0}%", Layout.Value, 28, KeyArt.Text, true);

            // On power and not charging there's no time to show, so the icon sits in the middle.
            if (battery.Minutes is not Int32 minutes || (battery.OnPower && !battery.IsCharging))
            {
                return KeyArt.Key(imageSize, "Battery", KeyArt.Battery(32, IconTop, battery.Percent / 100, color, battery.IsCharging), value);
            }

            return KeyArt.Key(imageSize, "Battery", KeyArt.Battery(12, IconTop, battery.Percent / 100, color, battery.IsCharging), value,
                new Line($"{minutes / 60}:{minutes % 60:00}", Layout.ZoneMiddle - 1, 16, battery.IsCharging ? KeyArt.Green : KeyArt.Text, true, 66, 44));
        }
    }
}
