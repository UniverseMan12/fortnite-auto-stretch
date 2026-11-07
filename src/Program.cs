using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FortniteStretch
{
    public class Settings
    {
        public string Device = "";
        public uint Width = 1720, Height = 1080, Frequency = 0;
        public bool Automatic = true;
    }

    internal static class Program
    {
        internal const string MutexName = "Local\\FortniteAutoStretch.Singleton.v1";
        internal const string ShowEventName = "Local\\FortniteAutoStretch.Show.v1";
        internal const string LaunchUri = "com.epicgames.launcher://apps/Fortnite?action=launch&silent=true";
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--guard") return Guard(int.Parse(args[1]));
            if (args.Length == 2 && args[0] == "--probe")
            {
                try
                {
                    var lines = Display.Targets().SelectMany(t => new[] { t.Label, "Current: " + Display.Current(t.Device) }
                        .Concat(Display.Modes(t.Device).Where(m => m.Width == 1720 && m.Height == 1080)
                        .Select(m => "Target: " + m + " | CDS_TEST=" + Display.Test(t.Device, m))));
                    File.WriteAllLines(args[1], lines);
                    return 0;
                }
                catch (Exception ex) { File.WriteAllText(args[1], ex.ToString()); return 1; }
            }
            bool owned = false;
            using (var mutex = new Mutex(false, MutexName))
            {
                try { owned = mutex.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
                if (!owned)
                {
                    try { using (var signal = EventWaitHandle.OpenExisting(ShowEventName)) signal.Set(); }
                    catch (WaitHandleCannotBeOpenedException) { MessageBox.Show("Fortnite Auto Stretch startet noch. Bitte kurz warten.", "Fortnite Auto Stretch"); }
                    return 0;
                }
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Storage.Recover();
                    // A separate small process restores the display even if this window crashes/is killed.
                    Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--guard " + Process.GetCurrentProcess().Id)
                    { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
                    Application.Run(new MainForm(args.Contains("--tray")));
                    return 0;
                }
                catch (Exception ex) { Storage.Log(ex); MessageBox.Show(ex.Message, "Fortnite Auto Stretch"); return 1; }
                finally { try { Storage.Recover(); } catch (Exception ex) { Storage.Log(ex); } mutex.ReleaseMutex(); }
            }
        }
        private static int Guard(int parentId)
        {
            try { using (var parent = Process.GetProcessById(parentId)) parent.WaitForExit(); }
            catch (ArgumentException) { }
            using (var mutex = new Mutex(false, MutexName))
            {
                bool owned = false;
                try
                {
                    try { owned = mutex.WaitOne(3000); } catch (AbandonedMutexException) { owned = true; }
                    if (owned) Storage.Recover(); // A replacement app owns recovery if it acquired the mutex first.
                }
                catch (Exception ex) { Storage.Log(ex); return 1; }
                finally { if (owned) mutex.ReleaseMutex(); }
            }
            return 0;
        }
        internal static bool GameRunning()
        {
            foreach (var name in new[] { "FortniteClient-Win64-Shipping", "FortniteClient-Win64-Shipping_EAC_EOS", "FortniteClient-Win64-Shipping_BE" })
            {
                var processes = Process.GetProcessesByName(name);
                bool found = processes.Length > 0;
                foreach (var p in processes) p.Dispose();
                if (found) return true;
            }
            return false;
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly ComboBox monitors = new ComboBox(), modes = new ComboBox();
        private readonly CheckBox automatic = new CheckBox(), startup = new CheckBox();
        private readonly Label status = new Label(), current = new Label();
        private readonly Button launch = new Button(), preview = new Button(), restore = new Button(), refresh = new Button();
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly Session session;
        private readonly EventWaitHandle showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ShowEventName);
        private Settings settings;
        private bool loading, exiting, previewing;
        private DateTime previewUntil;
        private string sessionDevice;
        private DisplayMode original;
        private readonly string config = Path.Combine(Storage.Root, "settings.xml");
        private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string RunName = "FortniteAutoStretch";

        internal MainForm(bool hidden)
        {
            Text = "Fortnite Auto Stretch";
            ClientSize = new Size(620, 510);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(20, 24, 34);
            ForeColor = Color.FromArgb(237, 240, 247);
            Font = new Font("Segoe UI", 10);
            Icon = SystemIcons.Application;
            AddLabel("FORTNITE / AUTO STRETCH", 26, 22, 560, 32, 19, true);
            AddLabel("Deine Auflösung. Automatisch beim Spielen.", 28, 60, 560, 25, 10, false);
            AddLabel("MONITOR", 28, 103, 250, 20, 9, true);
            monitors.SetBounds(28, 127, 432, 30);
            monitors.DropDownStyle = ComboBoxStyle.DropDownList;
            Controls.Add(monitors);
            StyleButton(refresh, "Neu laden", 470, 125, 122, false);
            AddLabel("AUFLÖSUNG & BILDWIEDERHOLRATE", 28, 174, 560, 20, 9, true);
            modes.SetBounds(28, 198, 564, 30);
            modes.DropDownStyle = ComboBoxStyle.DropDownList;
            Controls.Add(modes);
            current.SetBounds(28, 234, 564, 24); Controls.Add(current);
            automatic.Text = "Bei Fortnite-Start automatisch umschalten";
            automatic.SetBounds(28, 270, 560, 26); Controls.Add(automatic);
            startup.Text = "Mit Windows starten (im Infobereich)";
            startup.SetBounds(28, 300, 560, 26); Controls.Add(startup);
            StyleButton(launch, "Fortnite starten", 28, 346, 224, true);
            StyleButton(preview, "10 Sek. testen", 264, 346, 158, false);
            StyleButton(restore, "Zurückstellen", 434, 346, 158, false);
            status.SetBounds(28, 403, 564, 54); status.ForeColor = Color.FromArgb(124, 217, 190); Controls.Add(status);
            AddLabel("Schließen = im Hintergrund weiterlaufen · Beenden über das Tray-Symbol", 28, 474, 570, 22, 8, false);

            settings = new Settings();
            if (File.Exists(config))
            {
                try { settings = Storage.Read<Settings>(config); }
                catch (Exception ex) { Storage.Log(ex); status.Text = "Einstellungen unlesbar; Standardwerte geladen."; }
            }
            session = new Session(ApplySelected, RestoreOriginal);
            tray.Icon = SystemIcons.Application; tray.Text = "Fortnite Auto Stretch"; tray.Visible = true;
            var menu = new ContextMenuStrip();
            menu.Items.Add("Öffnen", null, delegate { ShowWindow(); });
            menu.Items.Add("Fortnite starten", null, delegate { RunSafely(StartGame); });
            menu.Items.Add("Desktop wiederherstellen", null, delegate { RunSafely(StopSession); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Beenden", null, delegate { RunSafely(delegate { StopSession(); exiting = true; Close(); }); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowWindow(); };
            loading = true;
            automatic.Checked = settings.Automatic;
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey)) startup.Checked = key != null && key.GetValue(RunName) != null;
            loading = false;
            monitors.SelectedIndexChanged += delegate { if (!loading) RunSafely(LoadModes); };
            modes.SelectedIndexChanged += delegate { if (!loading) RunSafely(SaveSettings); };
            automatic.CheckedChanged += delegate { if (!loading) RunSafely(delegate { SaveSettings(); if (!automatic.Checked) StopSession(); }); };
            startup.CheckedChanged += delegate
            {
                if (loading) return;
                RunSafely(delegate
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                    {
                        if (startup.Checked) key.SetValue(RunName, "\"" + Application.ExecutablePath + "\" --tray");
                        else key.DeleteValue(RunName, false);
                    }
                });
            };
            refresh.Click += delegate { RunSafely(LoadMonitors); };
            launch.Click += delegate { RunSafely(StartGame); };
            preview.Click += delegate { RunSafely(StartPreview); };
            restore.Click += delegate { RunSafely(StopSession); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
                try { StopSession(); } catch (Exception ex) { Storage.Log(ex); }
                timer.Stop(); tray.Visible = false; tray.Dispose(); showSignal.Dispose();
            };
            RunSafely(LoadMonitors);
            timer.Interval = 1000;
            timer.Tick += delegate { RunSafely(Tick); };
            timer.Start();
            Shown += delegate { if (hidden) Hide(); };
        }
        private void AddLabel(string text, int x, int y, int width, int height, int size, bool bold)
        {
            var label = new Label { Text = text, UseMnemonic = false, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular) };
            label.SetBounds(x, y, width, height); Controls.Add(label);
        }
        private void StyleButton(Button button, string text, int x, int y, int width, bool primary)
        {
            button.Text = text; button.SetBounds(x, y, width, 40); button.FlatStyle = FlatStyle.Flat;
            button.BackColor = primary ? Color.FromArgb(112, 224, 190) : Color.FromArgb(37, 44, 59);
            button.ForeColor = primary ? Color.FromArgb(15, 30, 28) : ForeColor;
            button.FlatAppearance.BorderSize = 0; Controls.Add(button);
        }
        private void ShowWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        private void RunSafely(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                Storage.Log(ex); status.Text = ex.Message;
                tray.ShowBalloonTip(5000, "Fortnite Auto Stretch", ex.Message, ToolTipIcon.Warning);
            }
            UpdateControls();
        }
        private void LoadMonitors()
        {
            loading = true;
            try
            {
                monitors.Items.Clear();
                foreach (var item in Display.Targets()) monitors.Items.Add(item);
                var selected = monitors.Items.Cast<DisplayTarget>().FirstOrDefault(t => t.Device == settings.Device);
                if (selected != null) monitors.SelectedItem = selected;
                else if (monitors.Items.Count > 0) monitors.SelectedIndex = 0;
            }
            finally { loading = false; }
            LoadModes();
        }
        private void LoadModes()
        {
            loading = true;
            try
            {
                modes.Items.Clear();
                var target = monitors.SelectedItem as DisplayTarget;
                if (target == null) { status.Text = "Kein aktiver Monitor gefunden."; return; }
                var desktop = Display.Current(target.Device);
                current.Text = "Desktop: " + desktop;
                var available = Display.SelectableModes(target.Device);
                foreach (var mode in available) modes.Items.Add(mode);
                var candidates = available.Where(m => m.Width == settings.Width && m.Height == settings.Height).ToList();
                var chosen = candidates.FirstOrDefault(m => m.Frequency == settings.Frequency);
                if (chosen.Width == 0) chosen = candidates.FirstOrDefault(m => m.Frequency == desktop.Frequency);
                if (chosen.Width == 0) chosen = candidates.Where(m => m.Frequency <= desktop.Frequency).OrderByDescending(m => m.Frequency).FirstOrDefault();
                if (chosen.Width == 0 && candidates.Count > 0) chosen = candidates.Last();
                if (chosen.Width != 0) { modes.SelectedItem = chosen; status.Text = "Bereit. Warte auf Fortnite."; }
                else status.Text = settings.Width + " × " + settings.Height + " fehlt. Einmal in NVIDIA anwenden, dann hier Neu laden.";
            }
            finally { loading = false; }
        }
        private void SaveSettings()
        {
            var target = monitors.SelectedItem as DisplayTarget;
            if (target != null) settings.Device = target.Device;
            if (modes.SelectedItem is DisplayMode)
            {
                var mode = (DisplayMode)modes.SelectedItem;
                settings.Width = mode.Width; settings.Height = mode.Height; settings.Frequency = mode.Frequency;
            }
            settings.Automatic = automatic.Checked;
            Storage.Save(config, settings);
        }
        private void ApplySelected()
        {
            if (previewing) throw new InvalidOperationException("Auflösungstest läuft noch.");
            var target = monitors.SelectedItem as DisplayTarget;
            if (target == null || !(modes.SelectedItem is DisplayMode)) throw new InvalidOperationException("Bitte eine vorhandene Auflösung auswählen.");
            var selected = (DisplayMode)modes.SelectedItem;
            original = Display.Current(target.Device); sessionDevice = target.Device;
            // Recovery is persisted before the first display change.
            Storage.Save(Storage.RecoveryPath, new Recovery { Device = sessionDevice, Mode = original });
            try { Display.Apply(sessionDevice, selected); }
            catch { Storage.Recover(); throw; }
            status.Text = "Stretch aktiv: " + selected;
        }
        private void RestoreOriginal()
        {
            Storage.Recover();
            status.Text = "Desktop wiederhergestellt: " + original;
            current.Text = "Desktop: " + original;
        }
        private void StopSession()
        {
            if (previewing) { RestoreOriginal(); previewing = false; }
            session.Stop(true);
        }
        private void StartPreview()
        {
            if (session.Active || previewing) return;
            ApplySelected(); previewUntil = DateTime.UtcNow.AddSeconds(10); previewing = true;
        }
        private void StartGame()
        {
            if (previewing || session.Active) return;
            using (var key = Registry.ClassesRoot.OpenSubKey("com.epicgames.launcher\\shell\\open\\command"))
                if (key == null) throw new InvalidOperationException("Epic Games Launcher fehlt. Bitte zuerst installieren/anmelden.");
            session.Launch(DateTime.UtcNow);
            try
            {
                if (!Program.GameRunning()) Process.Start(new ProcessStartInfo(Program.LaunchUri) { UseShellExecute = true });
                status.Text = "Stretch aktiv. Warte auf Fortnite (max. 3 Minuten).";
            }
            catch { session.Stop(true); throw; }
        }
        private void Tick()
        {
            if (showSignal.WaitOne(0)) ShowWindow();
            if (previewing)
            {
                if (DateTime.UtcNow >= previewUntil) { RestoreOriginal(); previewing = false; }
                else status.Text = "Test aktiv. Automatisch zurück in " + (int)Math.Ceiling((previewUntil - DateTime.UtcNow).TotalSeconds) + " Sekunden.";
            }
            else session.Tick(Program.GameRunning(), automatic.Checked && modes.SelectedItem is DisplayMode, DateTime.UtcNow);
        }
        private void UpdateControls()
        {
            bool active = session.Active || previewing;
            monitors.Enabled = modes.Enabled = refresh.Enabled = !active;
            preview.Enabled = launch.Enabled = !active && modes.SelectedItem is DisplayMode;
            restore.Enabled = active;
        }
    }
}
