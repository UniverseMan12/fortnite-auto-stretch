using System;

namespace FortniteStretch
{
    // Independent of WinForms and Win32, so lifecycle behaviour can be tested without changing a display.
    public sealed class Session
    {
        private readonly Action apply;
        private readonly Action restore;
        public bool Active { get; private set; }
        public bool Waiting { get; private set; }
        private bool seenGame;
        private DateTime deadline;
        private DateTime? missingSince;
        private bool inhibited;
        public Session(Action apply, Action restore) { this.apply = apply; this.restore = restore; }
        public void Launch(DateTime now)
        {
            if (Active) return;
            apply();
            Active = true;
            Waiting = true;
            seenGame = false;
            missingSince = null;
            deadline = now.AddMinutes(3);
            inhibited = false;
        }
        public void Tick(bool running, bool automatic, DateTime now)
        {
            if (!running) inhibited = false;
            if (!Active && running && automatic && !inhibited)
            {
                // Avoid retrying a rejected mode every second.
                inhibited = true;
                apply();
                Active = true;
                seenGame = true;
                Waiting = false;
                missingSince = null;
            }
            if (!Active) return;
            if (running) { seenGame = true; Waiting = false; missingSince = null; return; }
            if (!seenGame) { if (now >= deadline) Stop(false); return; }
            if (!missingSince.HasValue) missingSince = now;
            // Allow launcher / anti-cheat handoff and short process restarts.
            if ((now - missingSince.Value).TotalSeconds >= 12) Stop(false);
        }
        public void Stop(bool inhibit)
        {
            if (Active) restore(); // Keep active if restoration fails, so it can be retried.
            Active = false;
            Waiting = false;
            seenGame = false;
            missingSince = null;
            inhibited = inhibit;
        }
    }
}
