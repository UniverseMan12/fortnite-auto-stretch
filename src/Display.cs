using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace FortniteStretch
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DisplayMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        public ushort SpecVersion, DriverVersion, Size, DriverExtra;
        public uint Fields;
        public int PositionX, PositionY;
        public uint Orientation, FixedOutput;
        public short Color, Duplex, YResolution, TTOption, Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string FormName;
        public ushort LogPixels;
        public uint BitsPerPixel, Width, Height, Flags, Frequency;
        public uint ICMMethod, ICMIntent, MediaType, DitherType, Reserved1, Reserved2, PanningWidth, PanningHeight;
        public override string ToString() { return Width + " × " + Height + "  ·  " + Frequency + " Hz"; }
    }

    public class DisplayTarget
    {
        public string Device;
        public string Label;
        public override string ToString() { return Label; }
    }

    public static class Display
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DeviceInfo
        {
            public int Size;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplayDevices(string device, uint index, ref DeviceInfo info, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string device, int index, ref DisplayMode mode);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettingsEx(string device, ref DisplayMode mode, IntPtr hwnd, uint flags, IntPtr param);

        private static DisplayMode Empty()
        {
            return new DisplayMode { Size = (ushort)Marshal.SizeOf(typeof(DisplayMode)), DeviceName = "", FormName = "" };
        }
        public static List<DisplayTarget> Targets()
        {
            var result = new List<DisplayTarget>();
            for (uint i = 0; ; i++)
            {
                var info = new DeviceInfo { Size = Marshal.SizeOf(typeof(DeviceInfo)) };
                if (!EnumDisplayDevices(null, i, ref info, 0)) break;
                if ((info.Flags & 1) == 0 || (info.Flags & 8) != 0) continue;
                result.Add(new DisplayTarget { Device = info.Name, Label = info.Name + ((info.Flags & 4) != 0 ? " (Hauptmonitor)" : "") });
            }
            return result.OrderByDescending(t => t.Label.Contains("Hauptmonitor")).ToList();
        }
        public static DisplayMode Current(string device)
        {
            var mode = Empty();
            if (!EnumDisplaySettings(device, -1, ref mode)) throw new IOException("Monitor nicht erreichbar: " + device);
            return mode;
        }
        public static List<DisplayMode> Modes(string device)
        {
            var result = new List<DisplayMode>();
            for (int i = 0; ; i++)
            {
                var mode = Empty();
                if (!EnumDisplaySettings(device, i, ref mode)) break;
                if (mode.BitsPerPixel == 32 && (mode.Flags & 2) == 0) result.Add(mode);
            }
            return result.GroupBy(m => new { m.Width, m.Height, m.Frequency }).Select(g => g.First())
                .OrderBy(m => m.Width).ThenBy(m => m.Height).ThenByDescending(m => m.Frequency).ToList();
        }
        public static List<DisplayMode> SelectableModes(string device)
        {
            var result = Modes(device);
            var current = Current(device);
            string path = Path.Combine(Storage.Root, "learned-modes.xml");
            var learned = new List<Recovery>();
            if (File.Exists(path))
            {
                try { learned = Storage.Read<List<Recovery>>(path); }
                catch (Exception ex) { Storage.Log(ex); }
            }
            // NVIDIA sometimes omits a working custom mode from EnumDisplaySettings.
            // Learn the exact mode only while it is actually active, never invent timings.
            if (!learned.Any(r => r.Device == device && SameMode(r.Mode, current)))
            {
                learned.Add(new Recovery { Device = device, Mode = current });
                Storage.Save(path, learned);
            }
            result.AddRange(learned.Where(r => r.Device == device && Test(device, r.Mode) == 0).Select(r => r.Mode));
            return result.GroupBy(m => new { m.Width, m.Height, m.Frequency }).Select(g => g.First())
                .OrderBy(m => m.Width).ThenBy(m => m.Height).ThenByDescending(m => m.Frequency).ToList();
        }
        public static bool SameMode(DisplayMode a, DisplayMode b)
        {
            return a.Width == b.Width && a.Height == b.Height && a.Frequency == b.Frequency;
        }
        public static int Test(string device, DisplayMode mode)
        {
            mode.DriverExtra = 0;
            return ChangeDisplaySettingsEx(device, ref mode, IntPtr.Zero, 2, IntPtr.Zero);
        }
        public static void Apply(string device, DisplayMode mode)
        {
            int result = Test(device, mode);
            if (result != 0) throw new IOException("Windows lehnt diese Auflösung ab (Code " + result + ").");
            mode.DriverExtra = 0;
            // Temporary only: never update the registry or manufacture custom timings.
            result = ChangeDisplaySettingsEx(device, ref mode, IntPtr.Zero, 0, IntPtr.Zero);
            if (result != 0) throw new IOException("Auflösung konnte nicht geändert werden (Code " + result + ").");
        }
    }

    public class Recovery
    {
        public string Device;
        public DisplayMode Mode;
    }

    public static class Storage
    {
        public static string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteAutoStretch");
        public static string RecoveryPath { get { return Path.Combine(Root, "restore.xml"); } }
        public static void Save<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            using (var stream = File.Create(temporary)) new XmlSerializer(typeof(T)).Serialize(stream, value);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        public static T Read<T>(string path)
        {
            using (var stream = File.OpenRead(path)) return (T)new XmlSerializer(typeof(T)).Deserialize(stream);
        }
        public static void Recover()
        {
            if (!File.Exists(RecoveryPath)) return;
            var recovery = Read<Recovery>(RecoveryPath);
            Display.Apply(recovery.Device, recovery.Mode);
            File.Delete(RecoveryPath);
        }
        public static void Log(Exception exception)
        {
            try { Directory.CreateDirectory(Root); File.AppendAllText(Path.Combine(Root, "error.log"), DateTime.Now + " " + exception + Environment.NewLine); }
            catch { }
        }
    }
}
