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
        /// <summary>
        /// Quick launch backend: 0=Off, 1=AutoHotkey, 2=Native (app).
        /// Null means migrate from AutoStartAHK on first load after upgrade.
        /// </summary>
        public int? QuickLaunchMode { get; set; } = null;
        public bool AutoStartWithWindows { get; set; } = false;
        // 托盘图标单击行为: 0=打开主界面, 1=打开搜索页面
        public int TrayClickAction { get; set; } = 0;
        public bool HideDesktopIni { get; set; } = false;
        public bool HideExtensions { get; set; } = false;
        public bool SingleClickOpen { get; set; } = false;
        public bool IsListView { get; set; } = true;
        public int HotkeyModifiers { get; set; } = 1;
        public int HotkeyKey { get; set; } = 0x53;
        // 搜索快捷键 (默认 Alt+Shift+Space)
        public int SearchHotkeyModifiers { get; set; } = 0x0005;
        public int SearchHotkeyKey { get; set; } = 0x20;
        public bool DisableHotkeyInFullscreen { get; set; } = true;
        public int ListIconSize { get; set; } = 30;
        public int IconIconSize { get; set; } = 60;
        // 0=长条模式(默认), 1=宽胖模式, 2=自定义模式
        public int WindowSizeMode { get; set; } = 0;
        public int CustomWindowWidth { get; set; } = 500;
        public int CustomWindowHeight { get; set; } = 600;
        // 触控模式: 顶部/底部栏整体放大 2 倍
        public bool TouchMode { get; set; } = false;
        /// <summary>
        /// Toast/HUD 位置: 0正上 1左上 2右上 3正下 4左下 5右下
        /// </summary>
        public int ToastPosition { get; set; } = 0;
        // 手柄组合键 (XInput button bitmask), 0=禁用
        public int GamepadHotkeyButtons { get; set; } = 0;
        // Key 是文件夹编号 (0~9) 的字符串形式,Value 是用户自定义的别名
        public Dictionary<string, string> FolderAliases { get; set; } = new Dictionary<string, string>();
        // 最近打开过的文件/文件夹路径 (最多保留 20 条)
        public List<string> RecentFiles { get; set; } = new List<string>();

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