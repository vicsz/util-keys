namespace Loupedeck.UtilKeysPlugin
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    // A 25-minute Pomodoro timer. Tap to start, tap to pause, tap to carry on; hold to reset. The ring around the
    // tomato fills as time runs, amber for the last 2 minutes. When it's full the key glows green, a sound plays,
    // and it stays Done until tapped.

    public class PomodoroCommand : PluginDynamicCommand
    {
        private const String DoneSound = "/System/Library/Sounds/Glass.aiff";
        private static readonly TimeSpan Length = TimeSpan.FromMinutes(25);
        private static readonly TimeSpan Warning = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan Hold = TimeSpan.FromMilliseconds(600);

        private readonly Object _lock = new Object();
        private Timer _tick;

        // Running: when it ends. Paused: how much was left. Neither: ready, or done.
        private DateTime? _endsAt;
        private TimeSpan? _pausedLeft;
        private Boolean _done;

        // Press and release, for telling a tap from a hold. Keys that only report taps fall back to RunCommand.
        private Boolean _getsButtonEvents;
        private DateTime? _pressedAt;
        private Boolean _holdHandled;

        public PomodoroCommand()
            : base("Pomodoro", "A 25-minute timer. Tap to start or pause, hold to reset", "System")
        {
            this.IsWidget = true;
        }

        protected override Boolean OnUnload()
        {
            lock (this._lock)
            {
                this.StopTicking();
            }

            return true;
        }

        protected override Boolean ProcessButtonEvent2(String actionParameter, DeviceButtonEvent2 buttonEvent)
        {
            if (!this._getsButtonEvents)
            {
                this._getsButtonEvents = true;
                PluginLog.Info($"Pomodoro gets button events ({buttonEvent.EventType})");
            }

            switch (buttonEvent.EventType)
            {
                case DeviceButtonEventType.Press:
                    this._pressedAt = DateTime.UtcNow;
                    this._holdHandled = false;
                    break;

                case DeviceButtonEventType.LongPress:
                    if (!this._holdHandled)
                    {
                        this._holdHandled = true;
                        this.Reset();
                    }

                    break;

                case DeviceButtonEventType.Release:
                    var held = this._pressedAt is DateTime pressed && DateTime.UtcNow - pressed >= Hold;
                    this._pressedAt = null;

                    if (!this._holdHandled)
                    {
                        if (held)
                        {
                            this.Reset();
                        }
                        else
                        {
                            this.Tap();
                        }
                    }

                    break;
            }

            return true;
        }

        protected override void RunCommand(String actionParameter)
        {
            if (!this._getsButtonEvents)
            {
                this.Tap();
            }
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            TimeSpan left;
            Boolean running, done;

            lock (this._lock)
            {
                done = this._done;
                running = this._endsAt != null;
                left = this._endsAt is DateTime end ? end - DateTime.UtcNow : this._pausedLeft ?? Length;
            }

            var y = Layout.ZoneMiddle;

            if (done)
            {
                return KeyArt.Key(imageSize, "Pomodoro", KeyArt.Glow(KeyArt.Green) + KeyArt.Ring(58, y, 19, 1, KeyArt.Green) + KeyArt.Tomato(58, y, KeyArt.Red, KeyArt.Green),
                    new Line("Done", Layout.Value, 28, KeyArt.Green, true));
            }

            if (left < TimeSpan.Zero)
            {
                left = TimeSpan.Zero;
            }

            var started = running || left < Length;
            var filled = 1 - (left / Length);

            // Ready is all grey; paused keeps its ring but greys it; running is green, amber near the end.
            var (ringColor, textColor) = !running ? (KeyArt.Muted, KeyArt.Muted) : left <= Warning ? (KeyArt.Amber, KeyArt.Amber) : (KeyArt.Green, KeyArt.Text);
            var tomato = started ? KeyArt.Tomato(58, y, KeyArt.Red, KeyArt.Green) : KeyArt.Tomato(58, y, KeyArt.Dim, KeyArt.Muted);

            return KeyArt.Key(imageSize, "Pomodoro", KeyArt.Ring(58, y, 19, filled, ringColor) + tomato,
                new Line(Clock(left), Layout.Value, 28, textColor, true));
        }

        // Ready starts, running pauses, paused carries on, done goes back to ready.
        private void Tap()
        {
            lock (this._lock)
            {
                if (this._done)
                {
                    this._done = false;
                }
                else if (this._endsAt is DateTime end)
                {
                    this._pausedLeft = end - DateTime.UtcNow;
                    this._endsAt = null;
                    this.StopTicking();
                }
                else
                {
                    this._endsAt = DateTime.UtcNow + (this._pausedLeft ?? Length);
                    this._pausedLeft = null;
                    this._tick = new Timer(_ => this.Tick(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
                }
            }

            this.ActionImageChanged();
        }

        private void Reset()
        {
            lock (this._lock)
            {
                this.StopTicking();
                this._endsAt = null;
                this._pausedLeft = null;
                this._done = false;
            }

            PluginLog.Info("Pomodoro reset");
            this.ActionImageChanged();
        }

        private void Tick()
        {
            var finished = false;

            lock (this._lock)
            {
                if (this._endsAt is DateTime end && DateTime.UtcNow >= end)
                {
                    this.StopTicking();
                    this._endsAt = null;
                    this._done = true;
                    finished = true;
                }
            }

            if (finished)
            {
                PluginLog.Info("Pomodoro done");
                PlaySound();
            }

            this.ActionImageChanged();
        }

        private void StopTicking()
        {
            this._tick?.Dispose();
            this._tick = null;
        }

        private static void PlaySound()
        {
            try
            {
                Process.Start("/usr/bin/afplay", DoneSound)?.Dispose();
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "Couldn't play the Pomodoro sound");
            }
        }

        // Minutes and seconds, rounded up so the timer shows 25:00 at the start and 0:01 just before the end.
        private static String Clock(TimeSpan time)
        {
            var seconds = (Int32)Math.Ceiling(time.TotalSeconds);
            return $"{seconds / 60}:{seconds % 60:00}";
        }
    }
}
