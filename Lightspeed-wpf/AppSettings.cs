using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Lightspeed_wpf
{
    public class AppSettings
    {
        private static readonly string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public bool AutoStartAHK { get; set; } = false;
        public bool AutoStartWithWindows { get; set; } = false;
        public bool HideDesktopIni { get; set; } = false;
        public bool HideExtensions { get; set; } = false;
        public bool IsListView { get; set; } = true;
        public int HotkeyModifiers { get; set; } = 1;
        public int HotkeyKey { get; set; } = 0x53;
        public bool DisableHotkeyInFullscreen { get; set; } = true;
        public int ListIconSize { get; set; } = 30;
        public int IconIconSize { get; set; } = 60;
        // 0=长条模式(默认), 1=宽胖模式, 2=自定义模式
        public int WindowSizeMode { get; set; } = 0;
        public int CustomWindowWidth { get; set; } = 500;
        public int CustomWindowHeight { get; set; } = 600;
        // Key 是文件夹编号 (0~9) 的字符串形式,Value 是用户自定义的别名
        public Dictionary<string, string> FolderAliases { get; set; } = new Dictionary<string, string>();

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