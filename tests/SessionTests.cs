using System;
using FortniteStretch;

static class SessionTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static int Main()
    {
        int applies = 0, restores = 0;
        var now = new DateTime(2026, 1, 1);
        var s = new Session(() => applies++, () => restores++);
        s.Tick(false, true, now);
        Check(applies == 0, "Idle must not change desktop");
        s.Tick(true, true, now);
        s.Tick(true, true, now.AddSeconds(1));
        Check(applies == 1 && s.Active, "Detect once");
        s.Tick(false, true, now.AddSeconds(2));
        s.Tick(true, true, now.AddSeconds(5));
        Check(restores == 0, "Process handoff must not restore");
        s.Tick(false, true, now.AddSeconds(6));
        s.Tick(false, true, now.AddSeconds(18));
        Check(restores == 1 && !s.Active, "Restore after grace");
        s.Launch(now);
        s.Tick(false, true, now.AddSeconds(179));
        Check(s.Active, "Keep launch wait");
        s.Tick(false, true, now.AddSeconds(180));
        Check(!s.Active && restores == 2, "Failed launch restores");
        s.Tick(true, true, now);
        s.Stop(true);
        s.Tick(true, true, now);
        Check(!s.Active && applies == 3, "Manual restore inhibits running game");
        s.Tick(false, true, now);
        s.Tick(true, true, now);
        Check(s.Active && applies == 4, "New game re-arms");
        s.Stop(false);
        s.Tick(true, false, now);
        Check(!s.Active, "Disabled automatic mode");
        int failures = 0;
        var failed = new Session(() => { failures++; throw new Exception("rejected"); }, () => {});
        try { failed.Tick(true, true, now); } catch { }
        failed.Tick(true, true, now);
        Check(!failed.Active && failures == 1, "Failed apply not spammed");
        bool failRestore = true;
        var retry = new Session(() => {}, () => { if (failRestore) throw new Exception("disconnected"); });
        retry.Launch(now);
        try { retry.Stop(false); } catch { }
        Check(retry.Active, "Failed restore must retain state");
        failRestore = false; retry.Stop(false);
        Check(!retry.Active, "Restore retry succeeds");
        Check(System.Runtime.InteropServices.Marshal.SizeOf(typeof(DisplayMode)) == 220, "DEVMODEW ABI size");
        Console.WriteLine("PASS: 12 lifecycle/ABI checks; no display changes.");
        return 0;
    }
}
