namespace Loupedeck.UtilKeysPlugin
{
    using System;
    using System.Threading;

    // Tap to keep the Mac awake, tap again to let it sleep. While on, the cup steams: the key redraws a few times a
    // second, and only while it's on.

    public class KeepAwakeCommand : PluginDynamicCommand
    {
        private const Int32 FrameMs = 240;

        private readonly Object _lock = new Object();
        private Timer _animation;
        private Int32 _frame;

        public KeepAwakeCommand()
            : base("Keep awake", "Tap to stop the Mac sleeping on its own. The screen can still turn off", "System")
        {
            this.IsWidget = true;
        }

        private KeepAwake Awake => ((UtilKeysPlugin)this.Plugin).Awake;

        protected override Boolean OnLoad()
        {
            this.Awake.Changed += this.OnChanged;
            return true;
        }

        protected override Boolean OnUnload()
        {
            this.Awake.Changed -= this.OnChanged;
            this.Animate(false);
            return true;
        }

        protected override void RunCommand(String actionParameter) => this.Awake.Toggle();

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            var on = this.Awake.IsOn;
            return KeyArt.Key(imageSize, "Keep awake", KeyArt.Cup(42, Layout.ZoneMiddle - 4, on ? KeyArt.Green : KeyArt.Dim, on ? this._frame : null),
                new Line(on ? "On" : "Off", Layout.Value, 28, on ? KeyArt.Text : KeyArt.Muted, true));
        }

        private void OnChanged()
        {
            this.Animate(this.Awake.IsOn);
            this.ActionImageChanged();
        }

        private void Animate(Boolean on)
        {
            lock (this._lock)
            {
                if (on && this._animation == null)
                {
                    this._animation = new Timer(_ =>
                    {
                        this._frame = (this._frame + 1) % KeyArt.SteamFrames;
                        this.ActionImageChanged();
                    }, null, FrameMs, FrameMs);
                }
                else if (!on && this._animation != null)
                {
                    this._animation.Dispose();
                    this._animation = null;
                }
            }
        }
    }
}
