using System;
using System.IO;
using System.Text.Json;

namespace Lightspeed_wpf
{
    public class AppSettings
    {
        private static readonly string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public bool AutoStartAHK { get; set; } = false;
        public bool HideDesktopIni { get; set; } = false;
        public bool IsListView { get; set; } = true;
        public int HotkeyModifiers { get; set; } = 1;
        public int HotkeyKey { get; set; } = 0x53;
        public bool DisableHotkeyInFullscreen { get; set; } = true;
        public int ListIconSize { get; set; } = 30;
        public int IconIconSize { get; set; } = 60;

        private static AppSettings? _instance;
        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Load();
                }
                return _instance;
            }
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsPath, json);
            }
            catch { }
        }
    }
}