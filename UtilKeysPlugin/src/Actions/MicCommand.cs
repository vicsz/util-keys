namespace Loupedeck.UtilKeysPlugin
{
    using System;

    // The default microphone: red "Live" while an app records from it, green "On" while it's free, grey "Muted".
    // A tap mutes or unmutes it.

    public class MicCommand : StatCommand
    {
        public MicCommand()
            : base("Mic", "The default microphone, red while an app is using it. Tap to mute or unmute")
        {
        }

        protected override void RunCommand(String actionParameter)
        {
            var mic = this.Monitor.Mic;

            if (mic?.Exists == true)
            {
                this.Monitor.SetMicMuted(!mic.Muted);
            }
        }

        protected override BitmapImage Draw(PluginImageSize imageSize)
        {
            var mic = this.Monitor.Mic;
            var (value, valueColor, iconColor, muted) = mic switch
            {
                null or { Exists: false } => ("None", KeyArt.Muted, KeyArt.Dim, false),
                { Muted: true } => ("Muted", KeyArt.Muted, KeyArt.Dim, true),
                { InUse: true } => ("Live", KeyArt.Red, KeyArt.Red, false),
                _ => ("On", KeyArt.Text, KeyArt.Green, false),
            };

            return KeyArt.Key(imageSize, "Mic", KeyArt.Microphone(58, Layout.ZoneMiddle, iconColor, muted),
                new Line(value, Layout.Value, 28, valueColor, true));
        }
    }
}
