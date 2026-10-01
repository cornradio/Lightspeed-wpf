using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace Lightspeed_wpf
{
    /// <summary>
    /// Native replacement for AHK quick-launch:
    /// Alt+0~9 open folders; digit+letter opens items.
    /// Only active when desktop or taskbar is focused.
    /// </summary>
    public sealed class NativeQuickLaunchService : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;
        private const int VK_MENU = 0x12;
        private const int HC_ACTION = 0;

        private readonly Dispatcher _dispatcher;
        private readonly string _basePath;
        private readonly HashSet<int> _heldDigits = new();
        private readonly object _mapLock = new();

        private Dictionary<string, lightspeed_obj> _itemMap = new(StringComparer.OrdinalIgnoreCase);
        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc? _proc;
        private bool _disposed;

        public bool IsRunning => _hookId != IntPtr.Zero;

        public event Action<string, string>? LaunchRequested; // title, path

        public NativeQuickLaunchService(string basePath, Dispatcher dispatcher)
        {
            _basePath = basePath;
            _dispatcher = dispatcher;
        }

        public void Start()
        {
            if (_hookId != IntPtr.Zero) return;
            RebuildBindings();
            _proc = HookCallback;
            // WH_KEYBOARD_LL: module handle may be null on .NET; IntPtr.Zero is accepted for low-level hooks
            IntPtr hMod = IntPtr.Zero;
            try
            {
                using var curProcess = Process.GetCurrentProcess();
                using var curModule = curProcess.MainModule;
                if (curModule != null)
                    hMod = GetModuleHandle(curModule.ModuleName);
            }
            catch { }
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, hMod, 0);
            if (_hookId == IntPtr.Zero)
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);
        }

        public void Stop()
        {
            if (_hookId == IntPtr.Zero) return;
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _heldDigits.Clear();
        }

        public void RebuildBindings()
        {
            var map = new Dictionary<string, lightspeed_obj>(StringComparer.OrdinalIgnoreCase);
            var list = LoadBindings(_basePath);
            foreach (var item in list)
            {
                if (!map.ContainsKey(item.HotkeyStr))
                    map[item.HotkeyStr] = item;
            }
            lock (_mapLock)
            {
                _itemMap = map;
            }
        }

        public static List<lightspeed_obj> LoadBindings(string folderPath)
        {
            var list = new List<lightspeed_obj>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < 10; i++)
            {
                string folder = System.IO.Path.Combine(folderPath, i.ToString());
                if (!Directory.Exists(folder)) continue;

                foreach (var entry in Directory.GetFileSystemEntries(folder))
                {
                    string name = System.IO.Path.GetFileName(entry);
                    if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.Length == 0) continue;

                    char letter;
                    if (name.StartsWith('[') && name.Length > 1)
                        letter = char.ToLowerInvariant(name[1]);
                    else
                        letter = char.ToLowerInvariant(name[0]);

                    if (letter < 'a' || letter > 'z') continue;

                    string hotkey = $"{i} & {letter}";
                    if (!seen.Add(hotkey)) continue;

                    string title = System.IO.Path.GetFileNameWithoutExtension(entry)
                        .Replace(" - 快捷方式", "")
                        .Replace(" - 副本", "");
                    list.Add(new lightspeed_obj(title, entry, hotkey));
                }
            }

            return list;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HC_ACTION)
            {
                int msg = wParam.ToInt32();
                var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int vk = (int)info.vkCode;

                if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    if (IsDigitVk(vk))
                        _heldDigits.Remove(DigitFromVk(vk));
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                if (msg != WM_KEYDOWN && msg != WM_SYSKEYDOWN)
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);

                // Never interfere unless desktop / taskbar has focus
                if (!IsDesktopOrTaskbarFocused())
                {
                    _heldDigits.Clear();
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                bool altDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;

                if (IsDigitVk(vk))
                {
                    int digit = DigitFromVk(vk);
                    if (altDown)
                    {
                        string folderPath = System.IO.Path.Combine(_basePath, digit.ToString());
                        RequestLaunch($"文件夹 {digit}", folderPath);
                        return (IntPtr)1;
                    }

                    _heldDigits.Add(digit);
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                if (_heldDigits.Count > 0 && vk >= 0x41 && vk <= 0x5A)
                {
                    char letter = char.ToLowerInvariant((char)vk);
                    // Prefer the most recently held digit conceptually: use any held digit that matches a binding
                    foreach (int digit in _heldDigits)
                    {
                        string hotkey = $"{digit} & {letter}";
                        lightspeed_obj? item;
                        lock (_mapLock)
                        {
                            _itemMap.TryGetValue(hotkey, out item);
                        }
                        if (item != null)
                        {
                            RequestLaunch(item.Title, item.Path);
                            return (IntPtr)1;
                        }
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void RequestLaunch(string title, string path)
        {
            _dispatcher.BeginInvoke(() =>
            {
                LaunchRequested?.Invoke(title, path);
            });
        }

        public static bool IsDesktopOrTaskbarFocused()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;

            var className = new StringBuilder(256);
            GetClassName(hwnd, className, className.Capacity);
            string cls = className.ToString();
            return cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
        }

        private static bool IsDigitVk(int vk) =>
            (vk >= 0x30 && vk <= 0x39) || (vk >= 0x60 && vk <= 0x69);

        private static int DigitFromVk(int vk) =>
            vk >= 0x60 ? vk - 0x60 : vk - 0x30;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _proc = null;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
    }
}
