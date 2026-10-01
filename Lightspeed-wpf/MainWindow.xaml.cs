using System.Drawing;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = System.Windows.MessageBox;
using WpfClipboard = System.Windows.Clipboard;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfListViewItem = System.Windows.Controls.ListViewItem;
using System.Runtime.InteropServices.ComTypes;

namespace Lightspeed_wpf
{
    public partial class MainWindow : Window
    {
        private const int HOTKEY_ID = 9000;
        private const int SEARCH_HOTKEY_ID = 9001;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        private List<WpfButton> folderButtons = new List<WpfButton>();
        private int currentFolder = 0;
        private string basePath = @"C:\lightspeed";
        private double listIconSize = 30;
        private double iconIconSize = 60;
        private Forms.NotifyIcon? notifyIcon;
        private bool isListView = true;
        private IntPtr windowHandle;
        private HwndSource? source;

        private Dictionary<int, Dictionary<bool, List<FileItem>>> folderCache = new Dictionary<int, Dictionary<bool, List<FileItem>>>();
        private Dictionary<int, DateTime> folderCacheTime = new Dictionary<int, DateTime>();

        private uint currentModifiers = 0x0001;
        private uint currentKey = 0x53;
        private bool isCapturingKey = false;
        private FileItem? editingItem = null;
        private Forms.ToolStripMenuItem? trayHotkeyItem;
        private uint searchModifiers = 0x0005;
        private uint searchKey = 0x20;
        private bool isCapturingSearchKey = false;
        private bool _loadingSettings = false;
        private SearchWindow? _searchWindow;
        private NativeQuickLaunchService? _nativeQuickLaunch;
        private int _quickLaunchMode = 0; // 0=Off, 1=AHK, 2=Native


        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, IntPtr[] apidl, uint dwFlags);

        [DllImport("shell32.dll", EntryPoint = "SHParseDisplayName")]
        private static extern IntPtr SHParseDisplayName([MarshalAs(UnmanagedType.LPWStr)] string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr pszPath);

        private const uint FO_DELETE = 0x0003;
        private const uint FOF_ALLOWUNDO = 0x0040;
        private const uint FOF_NOCONFIRMATION = 0x0010;
        private const uint FOF_SILENT = 0x0032;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

        [DllImport("imm32.dll")]
        private static extern bool ImmDisableIME(IntPtr hkl);

        // --- XInput 手柄支持 ---
        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState14(int dwUserIndex, ref XINPUT_STATE pState);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState91(int dwUserIndex, ref XINPUT_STATE pState);

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        private const ushort XINPUT_GAMEPAD_DPAD_UP = 0x0001;
        private const ushort XINPUT_GAMEPAD_DPAD_DOWN = 0x0002;
        private const ushort XINPUT_GAMEPAD_DPAD_LEFT = 0x0004;
        private const ushort XINPUT_GAMEPAD_DPAD_RIGHT = 0x0008;
        private const ushort XINPUT_GAMEPAD_START = 0x0010;
        private const ushort XINPUT_GAMEPAD_BACK = 0x0020;
        private const ushort XINPUT_GAMEPAD_A = 0x1000;
        private const ushort XINPUT_GAMEPAD_B = 0x2000;
        private const ushort XINPUT_GAMEPAD_X = 0x4000;
        private const ushort XINPUT_GAMEPAD_Y = 0x8000;
        private const ushort XINPUT_GAMEPAD_LEFT_SHOULDER = 0x0100;
        private const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200;
        private const short STICK_DEADZONE = 15000;

        private DispatcherTimer? _gamepadTimer;
        private ushort _lastGamepadButtons;
        private DateTime _lbHoldStart = DateTime.MinValue;
        private DateTime _rbHoldStart = DateTime.MinValue;
        private DateTime _upHoldStart = DateTime.MinValue;
        private DateTime _downHoldStart = DateTime.MinValue;
        private DateTime _leftHoldStart = DateTime.MinValue;
        private DateTime _rightHoldStart = DateTime.MinValue;
        private DateTime _lastRepeatTime = DateTime.MinValue;
        private DateTime _stickLastAction = DateTime.MinValue;
        private bool _aHandled;
        private bool _xHandled;
        private bool _bHandled;
        private bool _backHandled;
        private bool _startHandled;
        private ContextMenu? _activeMenu;
        private int _menuIndex = -1;
        private ushort _gamepadHotkeyButtons;
        private bool _gamepadHotkeyTriggered;
        private bool _capturingGamepadHotkey;
        private bool _iconSizeDirty;
        private bool _gamepadEnabled = true;
        private const int initialRepeatDelayMs = 200;
        private const int repeatIntervalMs = 50;

        public MainWindow()
        {
            InitializeComponent();
            InitializeFolderButtons();
            InitializeTrayIcon();
            Loaded += MainWindow_Loaded;
            App.SecondInstanceLaunched += () => ShowFromTray();
        }

        private void InitializeFolderButtons()
        {
            folderButtons.Add(Btn0);
            folderButtons.Add(Btn1);
            folderButtons.Add(Btn2);
            folderButtons.Add(Btn3);
            folderButtons.Add(Btn4);
            folderButtons.Add(Btn5);
            folderButtons.Add(Btn6);
            folderButtons.Add(Btn7);
            folderButtons.Add(Btn8);
            folderButtons.Add(Btn9);

            foreach (var btn in folderButtons)
            {
                btn.AllowDrop = true;
                btn.PreviewMouseWheel += FolderButton_MouseWheel;
                btn.MouseEnter += FolderButton_MouseEnter;
                btn.MouseLeave += FolderButton_MouseLeave;
                btn.DragOver += FolderButton_DragOver;
                btn.Drop += FolderButton_Drop;
            }

            FileListView.PreviewMouseMove += ListView_PreviewMouseMove;
            IconListView.PreviewMouseMove += ListView_PreviewMouseMove;
            FileListView.PreviewMouseLeftButtonUp += ListView_PreviewMouseLeftButtonUp;
            IconListView.PreviewMouseLeftButtonUp += ListView_PreviewMouseLeftButtonUp;
        }

        private void FolderButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is WpfButton btn)
            {
                int index = folderButtons.IndexOf(btn);
                if (index < 0) return;
                string alias = AppSettings.Instance.FolderAliases.TryGetValue(index.ToString(), out var a) ? a : "";
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    TxtAliasOverlay.Text = alias;
                    AliasPopup.IsOpen = true;
                }
                else
                {
                    AliasPopup.IsOpen = false;
                }
            }
        }

        private void FolderButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            AliasPopup.IsOpen = false;
        }
        
        private DateTime lastWheelTime = DateTime.MinValue;
        private const int wheelCooldownMs = 50;
        
        private void FolderButton_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            DateTime now = DateTime.Now;
            if ((now - lastWheelTime).TotalMilliseconds < wheelCooldownMs)
            {
                e.Handled = true;
                return;
            }
            lastWheelTime = now;
            
            if (sender is WpfButton btn)
            {
                int currentIndex = folderButtons.IndexOf(btn);
                if (currentIndex >= 0)
                {
                    if (e.Delta > 0)
                    {
                        int prevFolder = currentFolder > 0 ? currentFolder - 1 : 9;
                        NavigateToFolder(prevFolder);
                    }
                    else
                    {
                        int nextFolder = currentFolder < 9 ? currentFolder + 1 : 0;
                        NavigateToFolder(nextFolder);
                    }
                    e.Handled = true;
                }
            }
        }

        private void InitializeTrayIcon()
        {
            notifyIcon = new Forms.NotifyIcon();
            notifyIcon.Icon = LoadAppIcon() ?? SystemIcons.Application;
            notifyIcon.Text = "Lightspeed";
            notifyIcon.Visible = true;
            notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left)
                    TrayIcon_Click();
            };
            notifyIcon.DoubleClick += (s, e) => TrayIcon_Click();

            var contextMenu = new Forms.ContextMenuStrip();
            var showItem = new Forms.ToolStripMenuItem("显示窗口");
            showItem.Click += (s, e) => ShowFromTray();
            contextMenu.Items.Add(showItem);

            var hotkeyItem = new Forms.ToolStripMenuItem($"快捷键: {GetHotkeyDisplayString(currentModifiers, currentKey)}");
            hotkeyItem.Enabled = false;
            trayHotkeyItem = hotkeyItem;
            contextMenu.Items.Add(hotkeyItem);

            contextMenu.Items.Add(new Forms.ToolStripSeparator());

            var quitItem = new Forms.ToolStripMenuItem("退出");
            quitItem.Click += (s, e) => ForceClose();
            contextMenu.Items.Add(quitItem);

            notifyIcon.ContextMenuStrip = contextMenu;
        }

        private static System.Drawing.Icon? LoadAppIcon()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using var stream = asm.GetManifestResourceStream("icon.ico");
                if (stream != null)
                {
                    return new System.Drawing.Icon(stream);
                }
            }
            catch { }

            try
            {
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    return new System.Drawing.Icon(iconPath);
                }
            }
            catch { }

            return null;
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void TrayIcon_Click()
        {
            if (AppSettings.Instance.TrayClickAction == 1)
                GetSearchWindow().ShowSearch();
            else
                ShowFromTray();
        }

        private void ToggleVisibility()
        {
            if (Visibility == Visibility.Visible && IsActive)
            {
                Hide();
            }
            else
            {
                ShowFromTray();
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            windowHandle = new WindowInteropHelper(this).Handle;
            source = HwndSource.FromHwnd(windowHandle);
            source?.AddHook(HwndHook);

            ImmDisableIME(IntPtr.Zero);

            LoadSettings();

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }
            NavigateToFolder(0);
            PreloadAllFolders();
            InitializeGamepadPolling();
        }

        private int XInputGetState(int dwUserIndex, ref XINPUT_STATE pState)
        {
            try { return XInputGetState14(dwUserIndex, ref pState); }
            catch { return XInputGetState91(dwUserIndex, ref pState); }
        }

        private void InitializeGamepadPolling()
        {
            try
            {
                XINPUT_STATE testState = new XINPUT_STATE();
                XInputGetState(0, ref testState);
            }
            catch { return; } // XInput DLL 不可用

            _gamepadTimer = new DispatcherTimer();
            _gamepadTimer.Interval = TimeSpan.FromMilliseconds(100);
            _gamepadTimer.Tick += GamepadTimer_Tick;
            _gamepadTimer.Start();
        }

        private void GamepadTimer_Tick(object? sender, EventArgs e)
        {
            XINPUT_STATE state = new XINPUT_STATE();
            int result = XInputGetState(0, ref state);
            if (result != 0) return;

            ushort cur = state.wButtons;
            ushort pressed = (ushort)(cur & ~_lastGamepadButtons);
            ushort released = (ushort)(~cur & _lastGamepadButtons);
            _lastGamepadButtons = cur;

            DateTime now = DateTime.Now;

            // Build combined mask including LT/RT as virtual button bits
            const ushort VIRTUAL_LT = 0x0040;
            const ushort VIRTUAL_RT = 0x0080;
            ushort combined = cur;
            if (state.bLeftTrigger > 100) combined |= VIRTUAL_LT;
            if (state.bRightTrigger > 100) combined |= VIRTUAL_RT;

            // --- 手柄快捷键捕获模式 ---
            if (_capturingGamepadHotkey && combined != 0)
            {
                if (BitOperations.PopCount(combined) >= 3)
                {
                    _gamepadHotkeyButtons = combined;
                    AppSettings.Instance.GamepadHotkeyButtons = combined;
                    AppSettings.Instance.Save();
                    _capturingGamepadHotkey = false;
                    Dispatcher.BeginInvoke(new Action(UpdateGamepadHotkeyDisplay));
                }
                return;
            }

            // --- 手柄快捷键: 召唤/隐藏 (后台也能响应) ---
            if (_gamepadHotkeyButtons != 0 && (combined & _gamepadHotkeyButtons) == _gamepadHotkeyButtons)
            {
                if (!_gamepadHotkeyTriggered)
                {
                    _gamepadHotkeyTriggered = true;
                    ToggleVisibility();
                }
            }
            else
            {
                _gamepadHotkeyTriggered = false;
            }

            // 手柄控制被关闭时，不响应其他手柄操作（快捷键仍然有效）
            if (!_gamepadEnabled) return;

            if (Visibility != Visibility.Visible) return;

            // --- Back (Select) 键: 关闭设置面板 ---
            if (SettingsPanel.Visibility == Visibility.Visible)
            {
                if ((pressed & XINPUT_GAMEPAD_BACK) != 0) _backHandled = false;
                if ((cur & XINPUT_GAMEPAD_BACK) != 0 && !_backHandled)
                {
                    BtnSettings_Click(this, new RoutedEventArgs());
                    _backHandled = true;
                }
                if ((released & XINPUT_GAMEPAD_BACK) != 0) _backHandled = false;
                return;
            }

            // 窗口不在前台时不响应其他手柄操作
            if (!IsActive) return;

            // --- 右键菜单打开时: 手柄控制菜单 ---
            if (_activeMenu != null && _activeMenu.IsOpen)
            {
                var menuItems = _activeMenu.Items.OfType<MenuItem>().ToList();

                if (menuItems.Count == 0) return;

                // D-pad 上下: 长按连续
                if ((pressed & XINPUT_GAMEPAD_DPAD_UP) != 0)
                {
                    if (_menuIndex <= 0) _menuIndex = menuItems.Count - 1; else _menuIndex--;
                    _upHoldStart = now; _lastRepeatTime = now;
                    HighlightMenuItem(menuItems);
                    return;
                }
                if ((cur & XINPUT_GAMEPAD_DPAD_UP) != 0 && (now - _upHoldStart).TotalMilliseconds >= initialRepeatDelayMs && (now - _lastRepeatTime).TotalMilliseconds >= repeatIntervalMs)
                {
                    if (_menuIndex <= 0) _menuIndex = menuItems.Count - 1; else _menuIndex--;
                    _lastRepeatTime = now;
                    HighlightMenuItem(menuItems);
                    return;
                }

                if ((pressed & XINPUT_GAMEPAD_DPAD_DOWN) != 0)
                {
                    if (_menuIndex >= menuItems.Count - 1) _menuIndex = 0; else _menuIndex++;
                    _downHoldStart = now; _lastRepeatTime = now;
                    HighlightMenuItem(menuItems);
                    return;
                }
                if ((cur & XINPUT_GAMEPAD_DPAD_DOWN) != 0 && (now - _downHoldStart).TotalMilliseconds >= initialRepeatDelayMs && (now - _lastRepeatTime).TotalMilliseconds >= repeatIntervalMs)
                {
                    if (_menuIndex >= menuItems.Count - 1) _menuIndex = 0; else _menuIndex++;
                    _lastRepeatTime = now;
                    HighlightMenuItem(menuItems);
                    return;
                }

                // 摇杆上下: 80ms cooldown
                bool stickUp = state.sThumbLY > STICK_DEADZONE && (now - _stickLastAction).TotalMilliseconds >= 80;
                bool stickDown = state.sThumbLY < -STICK_DEADZONE && (now - _stickLastAction).TotalMilliseconds >= 80;

                if (stickUp)
                {
                    if (_menuIndex <= 0) _menuIndex = menuItems.Count - 1; else _menuIndex--;
                    _stickLastAction = now;
                    HighlightMenuItem(menuItems);
                    return;
                }
                if (stickDown)
                {
                    if (_menuIndex >= menuItems.Count - 1) _menuIndex = 0; else _menuIndex++;
                    _stickLastAction = now;
                    HighlightMenuItem(menuItems);
                    return;
                }

                // A 键: 执行当前高亮项
                if ((pressed & XINPUT_GAMEPAD_A) != 0) _aHandled = false;
                if ((cur & XINPUT_GAMEPAD_A) != 0 && !_aHandled)
                {
                    _aHandled = true;
                    if (_menuIndex >= 0 && _menuIndex < menuItems.Count)
                    {
                        var menuItem = menuItems[_menuIndex];
                        _activeMenu.IsOpen = false;
                        _activeMenu = null;
                        _menuIndex = -1;
                        menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    }
                    return;
                }
                if ((released & XINPUT_GAMEPAD_A) != 0) _aHandled = false;

                // B / X 键: 关闭菜单
                if ((pressed & XINPUT_GAMEPAD_B) != 0) _bHandled = false;
                if ((pressed & XINPUT_GAMEPAD_X) != 0) _xHandled = false;
                if (((cur & XINPUT_GAMEPAD_B) != 0 && !_bHandled) ||
                    ((cur & XINPUT_GAMEPAD_X) != 0 && !_xHandled))
                {
                    _bHandled = true; _xHandled = true;
                    _activeMenu.IsOpen = false;
                    _activeMenu = null;
                    _menuIndex = -1;
                    return;
                }
                if ((released & XINPUT_GAMEPAD_B) != 0) _bHandled = false;
                if ((released & XINPUT_GAMEPAD_X) != 0) _xHandled = false;

                return; // 菜单打开时不处理其他输入
            }
            else
            {
                _activeMenu = null;
                _menuIndex = -1;
            }

            // --- LB / RB: 文件夹切换 (长按连续) ---
            if ((pressed & XINPUT_GAMEPAD_LEFT_SHOULDER) != 0)
            { NavigateToFolder(currentFolder > 0 ? currentFolder - 1 : 9); _lbHoldStart = now; _lastRepeatTime = now; }
            else if ((cur & XINPUT_GAMEPAD_LEFT_SHOULDER) != 0 && (now - _lbHoldStart).TotalMilliseconds >= initialRepeatDelayMs && (now - _lastRepeatTime).TotalMilliseconds >= repeatIntervalMs)
            { NavigateToFolder(currentFolder > 0 ? currentFolder - 1 : 9); _lastRepeatTime = now; }
            if ((released & XINPUT_GAMEPAD_LEFT_SHOULDER) != 0) _lbHoldStart = DateTime.MinValue;

            if ((pressed & XINPUT_GAMEPAD_RIGHT_SHOULDER) != 0)
            { NavigateToFolder(currentFolder < 9 ? currentFolder + 1 : 0); _rbHoldStart = now; _lastRepeatTime = now; }
            else if ((cur & XINPUT_GAMEPAD_RIGHT_SHOULDER) != 0 && (now - _rbHoldStart).TotalMilliseconds >= initialRepeatDelayMs && (now - _lastRepeatTime).TotalMilliseconds >= repeatIntervalMs)
            { NavigateToFolder(currentFolder < 9 ? currentFolder + 1 : 0); _lastRepeatTime = now; }
            if ((released & XINPUT_GAMEPAD_RIGHT_SHOULDER) != 0) _rbHoldStart = DateTime.MinValue;

            // --- D-pad: 项目导航 (长按连续) ---
            if ((pressed & XINPUT_GAMEPAD_DPAD_UP) != 0)
            { NavigateItems(-1, true); _upHoldStart = now; _lastRepeatTime = now; }
            else if ((cur & XINPUT_GAMEPAD_DPAD_UP) != 0 && (now - _upHoldStart).TotalMilliseconds >= initialRepeatDelayMs && (now - _lastRepeatTime).TotalMilliseconds >= repeatIntervalMs)
            { NavigateItems(-1, true); _lastRepeatTime = now; }
            if ((released & XINPUT_GAMEPAD_DPAD_UP) != 0) _upHoldStart = DateTime.MinValue;

            if ((pressed & XINPUT_GAMEPAD_DPAD_DOWN) != 0)
            { NavigateItems(1, true); _downHoldStart = now; _lastRepeatTime = now; }
            else if ((cur & XINPUT_GAMEPAD_DPAD_DOWN) != 0 && (now - _downHoldStart).TotalMilliseconds >= initialRepeatDelayMs && (now - _lastRepeatTime).TotalMilliseconds >= repeatIntervalMs)
            { NavigateItems(1, true); _lastRepeatTime = now; }
            if ((released & XINPUT_GAMEPAD_DPAD_DOWN) != 0) _downHoldStart = DateTime.MinValue;

            if ((pressed & XINPUT_GAMEPAD_DPAD_LEFT) != 0)
            { if (!isListView) { NavigateItems(-1); _leftHoldStart = now; _lastRepeatTime = now; } }
            else if ((cur & XINPUT_GAMEPAD_DPAD_LEFT) != 0 && (now - _leftHoldStart).TotalMilliseconds >= initialRepeatDelayMs && (now - _lastRepeatTime).TotalMilliseconds >= repeatIntervalMs)
            { if (!isListView) { NavigateItems(-1); _lastRepeatTime = now; } }
            if ((released & XINPUT_GAMEPAD_DPAD_LEFT) != 0) _leftHoldStart = DateTime.MinValue;

            if ((pressed & XINPUT_GAMEPAD_DPAD_RIGHT) != 0)
            { if (!isListView) { NavigateItems(1); _rightHoldStart = now; _lastRepeatTime = now; } }
            else if ((cur & XINPUT_GAMEPAD_DPAD_RIGHT) != 0 && (now - _rightHoldStart).TotalMilliseconds >= initialRepeatDelayMs && (now - _lastRepeatTime).TotalMilliseconds >= repeatIntervalMs)
            { if (!isListView) { NavigateItems(1); _lastRepeatTime = now; } }
            if ((released & XINPUT_GAMEPAD_DPAD_RIGHT) != 0) _rightHoldStart = DateTime.MinValue;

            // --- 左摇杆: 项目导航 (80ms cooldown) ---
            if ((now - _stickLastAction).TotalMilliseconds >= 80)
            {
                if (state.sThumbLY > STICK_DEADZONE) { NavigateItems(-1, true); _stickLastAction = now; }
                else if (state.sThumbLY < -STICK_DEADZONE) { NavigateItems(1, true); _stickLastAction = now; }
                else if (!isListView)
                {
                    if (state.sThumbLX < -STICK_DEADZONE) { NavigateItems(-1); _stickLastAction = now; }
                    else if (state.sThumbLX > STICK_DEADZONE) { NavigateItems(1); _stickLastAction = now; }
                }
            }

            // --- A 键: 打开 ---
            if ((pressed & XINPUT_GAMEPAD_A) != 0) _aHandled = false;
            if ((cur & XINPUT_GAMEPAD_A) != 0 && !_aHandled) { GamepadOpenSelectedItem(); _aHandled = true; }
            if ((released & XINPUT_GAMEPAD_A) != 0) _aHandled = false;

            // --- X 键: 右键菜单 ---
            if ((pressed & XINPUT_GAMEPAD_X) != 0) _xHandled = false;
            if ((cur & XINPUT_GAMEPAD_X) != 0 && !_xHandled) { GamepadShowContextMenu(); _xHandled = true; }
            if ((released & XINPUT_GAMEPAD_X) != 0) _xHandled = false;

            // --- Back (Select) 键: 打开/关闭设置 ---
            if ((pressed & XINPUT_GAMEPAD_BACK) != 0) _backHandled = false;
            if ((cur & XINPUT_GAMEPAD_BACK) != 0 && !_backHandled) { BtnSettings_Click(this, new RoutedEventArgs()); _backHandled = true; }
            if ((released & XINPUT_GAMEPAD_BACK) != 0) _backHandled = false;

            // --- Start 键: 在文件资源管理器中打开 ---
            if ((pressed & XINPUT_GAMEPAD_START) != 0) _startHandled = false;
            if ((cur & XINPUT_GAMEPAD_START) != 0 && !_startHandled) { BtnOpenInExplorer_Click(this, new RoutedEventArgs()); _startHandled = true; }
            if ((released & XINPUT_GAMEPAD_START) != 0) _startHandled = false;
        }

        private void NavigateItems(int delta, bool vertical = false)
        {
            if (isListView)
            {
                if (FileListView.Items.Count == 0) return;
                int idx = FileListView.SelectedIndex;
                if (idx < 0) idx = delta > 0 ? -1 : FileListView.Items.Count;
                int newIdx = Math.Clamp(idx + delta, 0, FileListView.Items.Count - 1);
                FileListView.SelectedIndex = newIdx;
                FileListView.ScrollIntoView(FileListView.SelectedItem);
            }
            else
            {
                if (IconListView.Items.Count == 0) return;
                int idx = IconListView.SelectedIndex;
                if (idx < 0) idx = delta > 0 ? -1 : IconListView.Items.Count;

                if (vertical)
                {
                    int cols = GetIconViewColumns();
                    if (delta < 0)
                        idx = idx >= cols ? idx - cols : 0;
                    else
                        idx = Math.Min(idx + cols, IconListView.Items.Count - 1);
                }
                else
                {
                    idx = Math.Clamp(idx + delta, 0, IconListView.Items.Count - 1);
                }

                IconListView.SelectedIndex = idx;
                IconListView.ScrollIntoView(IconListView.SelectedItem);
            }
        }

        private int GetIconViewColumns()
        {
            try
            {
                double panelWidth = IconListView.ActualWidth;
                if (panelWidth <= 0) return 1;

                var scrollViewer = FindVisualChild<System.Windows.Controls.ScrollViewer>(IconListView);
                if (scrollViewer != null)
                    panelWidth = scrollViewer.ViewportWidth > 0 ? scrollViewer.ViewportWidth : panelWidth;

                double itemWidth = 96; // MinWidth(90) + Margin(3*2)
                int cols = Math.Max(1, (int)(panelWidth / itemWidth));
                return cols;
            }
            catch { return 1; }
        }

        private void GamepadOpenSelectedItem()
        {
            if (isListView && FileListView.SelectedItem is FileItem li)
                OpenItem(li);
            else if (!isListView && IconListView.SelectedItem is FileItem ii)
                OpenItem(ii);
        }

        private void GamepadShowContextMenu()
        {
            if (isListView && FileListView.SelectedItem is FileItem li)
            {
                var container = FileListView.ItemContainerGenerator.ContainerFromItem(li) as FrameworkElement;
                ShowContextMenu(li, container ?? FileListView, container != null);
            }
            else if (!isListView && IconListView.SelectedItem is FileItem ii)
            {
                var container = IconListView.ItemContainerGenerator.ContainerFromItem(ii) as FrameworkElement;
                ShowContextMenu(ii, container ?? IconListView, container != null);
            }
        }

        private void PreloadAllFolders()
        {
            for (int i = 1; i <= 9; i++)
            {
                int folderNum = i;
                Dispatcher.BeginInvoke(new Action(() => PreloadFolderCache(folderNum)),
                    System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void PreloadFolderCache(int folderNum)
        {
            // 确保尺寸变量与设置一致
            listIconSize = AppSettings.Instance.ListIconSize;
            iconIconSize = AppSettings.Instance.IconIconSize;

            if (folderCache.ContainsKey(folderNum)
                && folderCache[folderNum].ContainsKey(false)
                && folderCache[folderNum].ContainsKey(true))
            {
                return;
            }

            string path = Path.Combine(basePath, folderNum.ToString());
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            try
            {
                var dirs = Directory.GetDirectories(path);
                var files = Directory.GetFiles(path);
                var listItems = new List<FileItem>();
                var iconItems = new List<FileItem>();

                foreach (var dir in dirs)
                {
                    listItems.Add(CreateFileItem(dir, true, false));
                    iconItems.Add(CreateFileItem(dir, true, true));
                }

                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    if (AppSettings.Instance.HideDesktopIni && fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    listItems.Add(CreateFileItem(file, false, false));
                    iconItems.Add(CreateFileItem(file, false, true));
                }

                if (!folderCache.ContainsKey(folderNum))
                {
                    folderCache[folderNum] = new Dictionary<bool, List<FileItem>>();
                }
                folderCache[folderNum][false] = listItems;
                folderCache[folderNum][true] = iconItems;
                folderCacheTime[folderNum] = DateTime.Now;
            }
            catch { }
        }

        private void LoadSettings()
        {
            _loadingSettings = true;

            int mode = ResolveQuickLaunchMode();
            _quickLaunchMode = mode;
            if (AppSettings.Instance.QuickLaunchMode == null)
            {
                AppSettings.Instance.QuickLaunchMode = mode;
                AppSettings.Instance.AutoStartAHK = mode == 1;
                AppSettings.Instance.Save();
            }
            RbQuickLaunchOff.IsChecked = mode == 0;
            RbQuickLaunchAHK.IsChecked = mode == 1;
            RbQuickLaunchNative.IsChecked = mode == 2;
            UpdateQuickLaunchPanels();

            ChkAutoStartWithWindows.IsChecked = AppSettings.Instance.AutoStartWithWindows;

            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            TxtVersion.Text = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "-";
            ChkHideDesktopIni.IsChecked = AppSettings.Instance.HideDesktopIni;
            ChkHideExtensions.IsChecked = AppSettings.Instance.HideExtensions;
            ChkSingleClickOpen.IsChecked = AppSettings.Instance.SingleClickOpen;
            ChkDisableInFullscreen.IsChecked = AppSettings.Instance.DisableHotkeyInFullscreen;
            ApplyToastPositionRadio(AppSettings.Instance.ToastPosition);
            ApplyToastHostPosition();
            
            listIconSize = AppSettings.Instance.ListIconSize;
            iconIconSize = AppSettings.Instance.IconIconSize;
            SliderIconSize.Value = listIconSize;
            SliderIconSizeIcon.Value = iconIconSize;
            TxtIconSize.Text = ((int)listIconSize).ToString();
            TxtIconSizeIcon.Text = ((int)iconIconSize).ToString();

            isListView = AppSettings.Instance.IsListView;
            if (isListView)
            {
                FileListView.Visibility = Visibility.Visible;
                IconListView.Visibility = Visibility.Collapsed;
                BtnListView.IsChecked = true;
                BtnIconView.IsChecked = false;
            }
            else
            {
                FileListView.Visibility = Visibility.Collapsed;
                IconListView.Visibility = Visibility.Visible;
                BtnListView.IsChecked = false;
                BtnIconView.IsChecked = true;
            }

            currentModifiers = (uint)AppSettings.Instance.HotkeyModifiers;
            currentKey = (uint)AppSettings.Instance.HotkeyKey;
            UpdateHotkeyDisplay();

            RegisterHotKey(windowHandle, HOTKEY_ID, currentModifiers, currentKey);

            searchModifiers = (uint)AppSettings.Instance.SearchHotkeyModifiers;
            searchKey = (uint)AppSettings.Instance.SearchHotkeyKey;
            RegisterHotKey(windowHandle, SEARCH_HOTKEY_ID, searchModifiers, searchKey);
            UpdateSearchHotkeyDisplay();

            _gamepadHotkeyButtons = (ushort)AppSettings.Instance.GamepadHotkeyButtons;
            UpdateGamepadHotkeyDisplay();

            ApplyQuickLaunchMode(mode, startBackend: true);

            // 加载 0~9 文件夹别名到设置面板的 TextBox
            LoadFolderAliasesToUI();

            // 应用窗口大小模式
            int windowMode = AppSettings.Instance.WindowSizeMode;
            if (windowMode == 0)
                RbWindowMode0.IsChecked = true;
            else if (windowMode == 1)
                RbWindowMode1.IsChecked = true;
            else
                RbWindowMode2.IsChecked = true;

            TxtCustomWidth.Text = AppSettings.Instance.CustomWindowWidth.ToString();
            TxtCustomHeight.Text = AppSettings.Instance.CustomWindowHeight.ToString();
            ApplyWindowSize();

            // 触控模式
            ChkTouchMode.IsChecked = AppSettings.Instance.TouchMode;
            ApplyTouchMode();

            // 托盘图标单击行为
            if (AppSettings.Instance.TrayClickAction == 1)
                RbTraySearch.IsChecked = true;
            else
                RbTrayMain.IsChecked = true;

            this.Background = System.Windows.Media.Brushes.Transparent;
            _loadingSettings = false;
        }

        private void ApplyWindowSize()
        {
            int mode = AppSettings.Instance.WindowSizeMode;
            if (mode == 0)
            {
                this.Width = 500;
                this.Height = 800;
            }
            else if (mode == 1)
            {
                this.Width = 700;
                this.Height = 500;
            }
            else
            {
                int w = AppSettings.Instance.CustomWindowWidth;
                int h = AppSettings.Instance.CustomWindowHeight;
                if (w > 0) this.Width = w;
                if (h > 0) this.Height = h;
            }
        }

        private void ApplyTouchMode()
        {
            double f = AppSettings.Instance.TouchMode ? 1.5 : 1.0;
            TitleBarGrid.LayoutTransform = new ScaleTransform(f, f);
            BottomBarBorder.LayoutTransform = new ScaleTransform(f, f);
        }

        private void ChkTouchMode_Changed(object sender, RoutedEventArgs e)
        {
            AppSettings.Instance.TouchMode = ChkTouchMode.IsChecked ?? false;
            AppSettings.Instance.Save();
            ApplyTouchMode();
        }

        private void RbTrayClick_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton rb && rb.IsChecked == true)
            {
                AppSettings.Instance.TrayClickAction = int.Parse(rb.Tag.ToString()!);
                AppSettings.Instance.Save();
            }
        }

        private System.Windows.Media.Color GetAccentColor()
        {
            return System.Windows.Media.Color.FromRgb(0x56, 0x9C, 0xD3);
        }

        private void RbWindowMode_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton rb && rb.IsChecked == true)
            {
                int mode = int.Parse(rb.Tag.ToString()!);
                AppSettings.Instance.WindowSizeMode = mode;
                AppSettings.Instance.Save();
                ApplyWindowSize();
                PanelCustomSize.Visibility = mode == 2 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void TxtCustomSize_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtCustomWidth.Text, out int w) && w > 0)
                AppSettings.Instance.CustomWindowWidth = w;
            if (int.TryParse(TxtCustomHeight.Text, out int h) && h > 0)
                AppSettings.Instance.CustomWindowHeight = h;
            AppSettings.Instance.Save();
            ApplyWindowSize();
        }

        private void LoadFolderAliasesToUI()
        {
            var aliases = AppSettings.Instance.FolderAliases;
            TxtAlias0.Text = aliases.TryGetValue("0", out var a0) ? a0 : "";
            TxtAlias1.Text = aliases.TryGetValue("1", out var a1) ? a1 : "";
            TxtAlias2.Text = aliases.TryGetValue("2", out var a2) ? a2 : "";
            TxtAlias3.Text = aliases.TryGetValue("3", out var a3) ? a3 : "";
            TxtAlias4.Text = aliases.TryGetValue("4", out var a4) ? a4 : "";
            TxtAlias5.Text = aliases.TryGetValue("5", out var a5) ? a5 : "";
            TxtAlias6.Text = aliases.TryGetValue("6", out var a6) ? a6 : "";
            TxtAlias7.Text = aliases.TryGetValue("7", out var a7) ? a7 : "";
            TxtAlias8.Text = aliases.TryGetValue("8", out var a8) ? a8 : "";
            TxtAlias9.Text = aliases.TryGetValue("9", out var a9) ? a9 : "";
        }

        private void TxtAlias_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is WpfTextBox tb && tb.Tag is string key)
            {
                var aliases = AppSettings.Instance.FolderAliases;
                string value = tb.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(value))
                {
                    if (aliases.ContainsKey(key)) aliases.Remove(key);
                }
                else
                {
                    aliases[key] = value;
                }
                AppSettings.Instance.Save();
                UpdateFolderNameDisplay();
            }
        }

        private void UpdateHotkeyDisplay()
        {
            string text = GetHotkeyDisplayString(currentModifiers, currentKey);
            TxtHotkey.Text = text;
            if (trayHotkeyItem != null)
            {
                trayHotkeyItem.Text = $"快捷键: {text}";
            }
        }

        private string GetHotkeyDisplayString(uint modifiers, uint key)
        {
            string m = "";
            if ((modifiers & 0x0001) != 0) m += "Alt+";
            if ((modifiers & 0x0002) != 0) m += "Ctrl+";
            if ((modifiers & 0x0004) != 0) m += "Shift+";
            if ((modifiers & 0x0008) != 0) m += "Win+";
            string keyName = GetKeyDisplayName(KeyInterop.KeyFromVirtualKey((int)key));
            return m + keyName;
        }

        private void UpdateSearchHotkeyDisplay()
        {
            TxtSearchHotkey.Text = GetHotkeyDisplayString(searchModifiers, searchKey);
        }

        private string GetKeyDisplayName(Key key)
        {
            return key switch
            {
                Key.A => "A", Key.B => "B", Key.C => "C", Key.D => "D", Key.E => "E",
                Key.F => "F", Key.G => "G", Key.H => "H", Key.I => "I", Key.J => "J",
                Key.K => "K", Key.L => "L", Key.M => "M", Key.N => "N", Key.O => "O",
                Key.P => "P", Key.Q => "Q", Key.R => "R", Key.S => "S", Key.T => "T",
                Key.U => "U", Key.V => "V", Key.W => "W", Key.X => "X", Key.Y => "Y",
                Key.Z => "Z",
                Key.D0 => "0", Key.D1 => "1", Key.D2 => "2", Key.D3 => "3", Key.D4 => "4",
                Key.D5 => "5", Key.D6 => "6", Key.D7 => "7", Key.D8 => "8", Key.D9 => "9",
                Key.Space => "Space", Key.Tab => "Tab", Key.Escape => "Esc",
                Key.Enter => "Enter", Key.Back => "Back",
                Key.Home => "Home", Key.End => "End", Key.Insert => "Insert", Key.Delete => "Delete",
                Key.Up => "Up", Key.Down => "Down", Key.Left => "Left", Key.Right => "Right",
                Key.F1 => "F1", Key.F2 => "F2", Key.F3 => "F3", Key.F4 => "F4",
                Key.F5 => "F5", Key.F6 => "F6", Key.F7 => "F7", Key.F8 => "F8",
                Key.F9 => "F9", Key.F10 => "F10", Key.F11 => "F11", Key.F12 => "F12",
                _ => key.ToString()
            };
        }

        private void UpdateGamepadHotkeyDisplay()
        {
            TxtGamepadHotkey.Text = GamepadButtonsToString(_gamepadHotkeyButtons);
        }

        private string GamepadButtonsToString(ushort buttons)
        {
            if (buttons == 0) return "未设置";
            var parts = new List<string>();
            if ((buttons & 0x0001) != 0) parts.Add("↑");
            if ((buttons & 0x0002) != 0) parts.Add("↓");
            if ((buttons & 0x0004) != 0) parts.Add("←");
            if ((buttons & 0x0008) != 0) parts.Add("→");
            if ((buttons & 0x0010) != 0) parts.Add("Start");
            if ((buttons & 0x0020) != 0) parts.Add("Back");
            if ((buttons & 0x0040) != 0) parts.Add("LT");
            if ((buttons & 0x0080) != 0) parts.Add("RT");
            if ((buttons & 0x0100) != 0) parts.Add("LB");
            if ((buttons & 0x0200) != 0) parts.Add("RB");
            if ((buttons & 0x1000) != 0) parts.Add("A");
            if ((buttons & 0x2000) != 0) parts.Add("B");
            if ((buttons & 0x4000) != 0) parts.Add("X");
            if ((buttons & 0x8000) != 0) parts.Add("Y");
            return parts.Count > 0 ? string.Join("+", parts) : "未设置";
        }

        private void BtnCaptureGamepadHotkey_Click(object sender, RoutedEventArgs e)
        {
            _capturingGamepadHotkey = true;
            _gamepadHotkeyTriggered = false;
            TxtGamepadHotkey.Text = "按下组合键 (至少3键)...";
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                if (_capturingGamepadHotkey)
                {
                    _capturingGamepadHotkey = false;
                    UpdateGamepadHotkeyDisplay();
                }
            };
            timer.Start();
        }

        private void BtnClearGamepadHotkey_Click(object sender, RoutedEventArgs e)
        {
            _gamepadHotkeyButtons = 0;
            AppSettings.Instance.GamepadHotkeyButtons = 0;
            AppSettings.Instance.Save();
            UpdateGamepadHotkeyDisplay();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private bool IsFullscreenAppRunning()
        {
            IntPtr hwnd = GetForegroundWindow();
            
            StringBuilder className = new StringBuilder(256);
            GetClassName(hwnd, className, className.Capacity);
            string windowClass = className.ToString();
            
            if (windowClass == "Progman" || windowClass == "WorkerW")
            {
                return false;
            }
            
            RECT appRect;
            RECT screenRect;
            GetWindowRect(hwnd, out appRect);
            int screenWidth = GetSystemMetrics(0);
            int screenHeight = GetSystemMetrics(1);
            screenRect = new RECT { Left = 0, Top = 0, Right = screenWidth, Bottom = screenHeight };
            int appWidth = appRect.Right - appRect.Left;
            int appHeight = appRect.Bottom - appRect.Top;
            int screenArea = screenWidth * screenHeight;
            int appArea = appWidth * appHeight;
            return appArea >= screenArea * 0.95;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                if (AppSettings.Instance.DisableHotkeyInFullscreen && IsFullscreenAppRunning())
                {
                    handled = true;
                    return IntPtr.Zero;
                }
                if (hotkeyId == HOTKEY_ID)
                {
                    ToggleWindowVisibility();
                    handled = true;
                }
                else if (hotkeyId == SEARCH_HOTKEY_ID)
                {
                    SummonSearch();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void ToggleWindowVisibility()
        {
            if (Visibility == Visibility.Visible)
            {
                Hide();
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = true;
                    notifyIcon.ShowBalloonTip(500, "Lightspeed", $"已隐藏到托盘，按 {GetHotkeyDisplayString(currentModifiers, currentKey)} 显示", Forms.ToolTipIcon.Info);
                }
            }
            else
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            }
        }

        private SearchWindow GetSearchWindow()
        {
            if (_searchWindow == null)
            {
                _searchWindow = new SearchWindow();
            }
            return _searchWindow;
        }

        private void SummonSearch()
        {
            GetSearchWindow().ShowSearch();
        }

        // 供 SearchWindow 打开搜索结果时调用：记录最近使用 + 导航/打开
        public void OpenSearchResultPath(string path)
        {
            AddToRecentFiles(path);
            bool isDir = Directory.Exists(path);

            if (isDir)
            {
                string folderName = Path.GetFileName(path);
                if (int.TryParse(folderName, out int folderNum) && folderNum >= 0 && folderNum <= 9)
                {
                    ShowFromTray();
                    NavigateToFolder(folderNum);
                    return;
                }

                int? parentFolder = GetParentFolderNum(path);
                if (parentFolder.HasValue)
                {
                    ShowFromTray();
                    NavigateToFolder(parentFolder.Value);
                    Dispatcher.BeginInvoke(new Action(() => SelectItemByPath(path)), System.Windows.Threading.DispatcherPriority.Loaded);
                    return;
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                return;
            }

            // 文件：直接打开
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }

        private void NavigateToFolder(int folderNum)
        {
            // 确保尺寸变量与设置一致（防止启动时序问题）
            listIconSize = AppSettings.Instance.ListIconSize;
            iconIconSize = AppSettings.Instance.IconIconSize;

            currentFolder = folderNum;
            string path = Path.Combine(basePath, folderNum.ToString());

            FileListView.Items.Clear();
            IconListView.Items.Clear();

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            try
            {
                bool hasListCache = folderCache.ContainsKey(folderNum) && folderCache[folderNum].ContainsKey(false);
                bool hasIconCache = folderCache.ContainsKey(folderNum) && folderCache[folderNum].ContainsKey(true);

                if (hasListCache && hasIconCache)
                {
                    foreach (var item in folderCache[folderNum][false])
                    {
                        if (item.IsDirectory || (!AppSettings.Instance.HideDesktopIni || !Path.GetFileName(item.FullPath).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)))
                        {
                            FileListView.Items.Add(item);
                        }
                    }
                    foreach (var item in folderCache[folderNum][true])
                    {
                        if (item.IsDirectory || (!AppSettings.Instance.HideDesktopIni || !Path.GetFileName(item.FullPath).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)))
                        {
                            IconListView.Items.Add(item);
                        }
                    }
                }
                else
                {
                    var dirs = Directory.GetDirectories(path);
                    var files = Directory.GetFiles(path);
                    var listItems = new List<FileItem>();
                    var iconItems = new List<FileItem>();

                    foreach (var dir in dirs)
                    {
                        var listItem = CreateFileItem(dir, true, false);
                        var iconItem = CreateFileItem(dir, true, true);
                        listItems.Add(listItem);
                        iconItems.Add(iconItem);
                        FileListView.Items.Add(listItem);
                        IconListView.Items.Add(iconItem);
                    }

                    foreach (var file in files)
                    {
                        string fileName = Path.GetFileName(file);
                        if (AppSettings.Instance.HideDesktopIni && fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        var listItem = CreateFileItem(file, false, false);
                        var iconItem = CreateFileItem(file, false, true);
                        listItems.Add(listItem);
                        iconItems.Add(iconItem);
                        FileListView.Items.Add(listItem);
                        IconListView.Items.Add(iconItem);
                    }

                    if (!folderCache.ContainsKey(folderNum))
                    {
                        folderCache[folderNum] = new Dictionary<bool, List<FileItem>>();
                    }
                    folderCache[folderNum][false] = listItems;
                    folderCache[folderNum][true] = iconItems;
                    folderCacheTime[folderNum] = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"无法读取文件夹: {ex.Message}");
            }

            UpdateFolderButtonSelection(folderNum);
            UpdateFolderNameDisplay();

            // 自动选中第一项, 方便手柄操作
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (isListView && FileListView.Items.Count > 0)
                {
                    FileListView.SelectedIndex = 0;
                    FileListView.ScrollIntoView(FileListView.Items[0]);
                }
                else if (!isListView && IconListView.Items.Count > 0)
                {
                    IconListView.SelectedIndex = 0;
                    IconListView.ScrollIntoView(IconListView.Items[0]);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private FileItem CreateFileItem(string path, bool isDirectory, bool isIconMode)
        {
            double size = isIconMode ? iconIconSize : listIconSize;
            double scaleFactor = GetDpiScaleFactor();
            double scaledSize = size * scaleFactor;
            string fileName = Path.GetFileName(path);
            if (AppSettings.Instance.HideExtensions && !isDirectory && fileName.Contains('.'))
            {
                int dotIndex = fileName.LastIndexOf('.');
                fileName = fileName.Substring(0, dotIndex);
            }
            return new FileItem
            {
                Name = fileName,
                FullPath = path,
                IsDirectory = isDirectory,
                IconSize = scaledSize,
                Icon = IconHelper.GetIcon(path, isDirectory, (int)scaledSize)
            };
        }

        private double GetDpiScaleFactor()
        {
            int dpi = GetDeviceCaps(GetDC(IntPtr.Zero), 88);
            return dpi / 96.0;
        }

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        private void UpdateFolderButtonSelection(int selectedFolder)
        {
            for (int i = 0; i < folderButtons.Count; i++)
            {
                if (i == selectedFolder)
                {
                    var accent = GetAccentColor();
                    folderButtons[i].Background = new SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(0x88, accent.R, accent.G, accent.B));
                    folderButtons[i].Foreground = new SolidColorBrush(Colors.White);
                }
                else
                {
                    folderButtons[i].Background = new SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
                    folderButtons[i].Foreground = new SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));
                }
            }
        }

        private void UpdateFolderNameDisplay()
        {
            string alias = AppSettings.Instance.FolderAliases.TryGetValue(currentFolder.ToString(), out var a) ? a : "";
            TxtFolderName.Text = string.IsNullOrWhiteSpace(alias) ? "" : alias;
        }

        private void ClearFolderCache(int folderNum)
        {
            if (folderCache.ContainsKey(folderNum))
            {
                folderCache.Remove(folderNum);
            }
            if (folderCacheTime.ContainsKey(folderNum))
            {
                folderCacheTime.Remove(folderNum);
            }
        }

        private void ClearAllCache()
        {
            foreach (var folder in folderCache.Values)
            {
                foreach (var viewItems in folder.Values)
                {
                    foreach (var item in viewItems)
                    {
                        if (item.Icon is System.Windows.Media.Imaging.BitmapSource bitmapSource)
                        {
                            bitmapSource.Freeze();
                        }
                    }
                }
            }
            folderCache.Clear();
            folderCacheTime.Clear();
            IconHelper.ClearCache();
        }

        private void FolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn)
            {
                int folderNum = folderButtons.IndexOf(btn);
                if (folderNum >= 0)
                {
                    if (SettingsPanel.Visibility == Visibility.Visible)
                    {
                        SettingsPanel.Visibility = Visibility.Collapsed;
                        BtnSettings.IsChecked = false;
                        if (_iconSizeDirty)
                        {
                            SaveAndClearIconCache();
                            _iconSizeDirty = false;
                        }
                    }
                    NavigateToFolder(folderNum);
                }
            }
        }

        private void FileListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (AppSettings.Instance.SingleClickOpen) return;
            if (FileListView.SelectedItem is FileItem item)
            {
                OpenItem(item);
            }
        }

        private void IconListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (AppSettings.Instance.SingleClickOpen) return;
            if (IconListView.SelectedItem is FileItem item)
            {
                OpenItem(item);
            }
        }

        private void OpenItem(FileItem item)
        {
            try
            {
                AddToRecentFiles(item.FullPath);

                if (item.IsDirectory)
                {
                    string folderName = Path.GetFileName(item.FullPath);
                    int folderNum;
                    if (int.TryParse(folderName, out folderNum))
                    {
                        NavigateToFolder(folderNum);
                    }
                    else
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = item.FullPath,
                            UseShellExecute = true
                        });
                    }
                }
                else
                {
                    item.JustOpened = true;
                    FileListView.Items.Refresh();
                    IconListView.Items.Refresh();
                    
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = item.FullPath,
                        UseShellExecute = true
                    });

                    Task.Delay(1000).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            item.JustOpened = false;
                            FileListView.Items.Refresh();
                            IconListView.Items.Refresh();
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"无法打开: {ex.Message}");
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            ShowToast("已刷新");
            ClearFolderCache(currentFolder);
            NavigateToFolder(currentFolder);
            if (_quickLaunchMode == 2)
                _nativeQuickLaunch?.RebuildBindings();
        }

        private System.Threading.CancellationTokenSource? _toastCts;

        private async void ShowToast(string message)
        {
            if (ToastText == null || ToastBar == null) return;

            _toastCts?.Cancel();
            _toastCts = new System.Threading.CancellationTokenSource();
            var token = _toastCts.Token;

            ToastText.Text = message;
            ToastBar.Visibility = Visibility.Visible;
            ToastBar.Opacity = 0;

            try
            {
                for (int i = 1; i <= 10; i++)
                {
                    if (token.IsCancellationRequested) return;
                    ToastBar.Opacity = i / 10.0;
                    await Task.Delay(20);
                }
                ToastBar.Opacity = 1;
                await Task.Delay(1500, token);
                for (int i = 10; i >= 0; i--)
                {
                    if (token.IsCancellationRequested) return;
                    ToastBar.Opacity = i / 10.0;
                    await Task.Delay(30);
                }
                if (!token.IsCancellationRequested)
                {
                    ToastBar.Visibility = Visibility.Collapsed;
                }
            }
            catch (OperationCanceledException) { }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsPanel();
            ShowToast(SettingsPanel.Visibility == Visibility.Visible ? "已打开设置" : "已关闭设置");
        }

        private void ToggleSettingsPanel()
        {
            if (SettingsPanel.Visibility == Visibility.Collapsed)
            {
                SettingsPanel.Visibility = Visibility.Visible;
                BtnSettings.IsChecked = true;
            }
            else
            {
                SettingsPanel.Visibility = Visibility.Collapsed;
                BtnSettings.IsChecked = false;
                if (_iconSizeDirty)
                {
                    ShowToast("正在刷新图标...");
                    ApplyIconSizeChanges();
                    _iconSizeDirty = false;
                }
            }
        }

        // 点击设置面板空白区域关闭设置（交互控件除外）
        private void SettingsPanel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsSettingsInteractive(e.OriginalSource as DependencyObject)) return;
            ToggleSettingsPanel();
            e.Handled = true;
        }

        private bool IsSettingsInteractive(DependencyObject? original)
        {
            // 文本超链接属于 ContentElement, 不在视觉树中
            if (original is System.Windows.Documents.Hyperlink || original is System.Windows.Documents.Run)
                return true;

            DependencyObject? d = original;
            while (d != null)
            {
                if (d is System.Windows.Controls.Primitives.ButtonBase ||
                    d is WpfTextBox ||
                    d is System.Windows.Controls.Primitives.RangeBase ||
                    d is System.Windows.Controls.Primitives.Thumb)
                {
                    return true;
                }

                if (d is System.Windows.Media.Visual || d is System.Windows.Media.Media3D.Visual3D)
                    d = System.Windows.Media.VisualTreeHelper.GetParent(d);
                else
                    break;
            }
            return false;
        }

        private void BtnCreateFolders_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            for (int i = 0; i < 10; i++)
            {
                string folderPath = Path.Combine(basePath, i.ToString());
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
            }

            WpfMessageBox.Show("文件夹创建完成！");
            NavigateToFolder(currentFolder);
        }

        private void BtnGenerateAHK_Click(object sender, RoutedEventArgs e)
        {
            CreateAHK(basePath);
            WpfMessageBox.Show($"AHK 已生成: {Path.Combine(basePath, "lightspeed.ahk")}");
        }

        private void BtnStartAHK_Click(object sender, RoutedEventArgs e)
        {
            string ahkPath = Path.Combine(basePath, "lightspeed.ahk");
            if (File.Exists(ahkPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ahkPath,
                    UseShellExecute = true
                });
            }
            else
            {
                WpfMessageBox.Show("AHK 文件不存在，请先点击「生成 AHK」", "提示");
            }
        }

        private void BtnGenerateAndStart_Click(object sender, RoutedEventArgs e)
        {
            CreateAHK(basePath);
            BtnStartAHK_Click(sender, e);
        }

        private void BtnQuickStartAHK_Click(object sender, RoutedEventArgs e)
        {
            if (_quickLaunchMode == 1)
            {
                ShowToast("AHK 已启动");
                CreateAHK(basePath);
                StartAhkProcess();
            }
            else if (_quickLaunchMode == 2)
            {
                ApplyQuickLaunchMode(2, startBackend: true);
                ShowToast("程序本体快捷键已启用");
            }
            else
            {
                ShowToast("请先在设置中选择快速启动方式");
            }
        }

        private void RbQuickLaunchMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_loadingSettings) return;
            if (sender is not System.Windows.Controls.RadioButton rb || rb.IsChecked != true) return;
            if (!int.TryParse(rb.Tag?.ToString(), out int mode)) return;

            _quickLaunchMode = mode;
            AppSettings.Instance.QuickLaunchMode = mode;
            AppSettings.Instance.AutoStartAHK = mode == 1;
            AppSettings.Instance.Save();

            UpdateQuickLaunchPanels();
            ApplyQuickLaunchMode(mode, startBackend: true);

            ShowToast(mode switch
            {
                1 => "已切换到 AutoHotkey",
                2 => "已切换到程序本体快捷键（若 AHK 仍在运行请手动退出）",
                _ => "已关闭快速启动"
            });
        }

        private void BtnReloadNativeBindings_Click(object sender, RoutedEventArgs e)
        {
            _nativeQuickLaunch?.RebuildBindings();
            ShowToast("快捷键绑定已重新加载");
        }

        private static int ResolveQuickLaunchMode()
        {
            if (AppSettings.Instance.QuickLaunchMode is int mode && mode is >= 0 and <= 2)
                return mode;
            return AppSettings.Instance.AutoStartAHK ? 1 : 0;
        }

        private void UpdateQuickLaunchPanels()
        {
            if (PanelAhkControls != null)
                PanelAhkControls.Visibility = _quickLaunchMode == 1 ? Visibility.Visible : Visibility.Collapsed;
            if (PanelNativeControls != null)
                PanelNativeControls.Visibility = _quickLaunchMode == 2 ? Visibility.Visible : Visibility.Collapsed;

            if (BtnQuickStart != null)
            {
                BtnQuickStart.ToolTip = _quickLaunchMode switch
                {
                    1 => "生成并启动 AHK",
                    2 => "启用程序本体快捷键",
                    _ => "请先在设置中选择快速启动方式"
                };
            }
        }

        private void ApplyQuickLaunchMode(int mode, bool startBackend)
        {
            // Stop native first when leaving native mode
            if (mode != 2)
            {
                _nativeQuickLaunch?.Stop();
            }

            if (!startBackend) return;

            if (mode == 1)
            {
                CreateAHK(basePath);
                StartAhkProcess();
            }
            else if (mode == 2)
            {
                EnsureNativeQuickLaunch();
                _nativeQuickLaunch!.RebuildBindings();
                if (!_nativeQuickLaunch.IsRunning)
                    _nativeQuickLaunch.Start();
            }
        }

        private void EnsureNativeQuickLaunch()
        {
            if (_nativeQuickLaunch != null) return;
            _nativeQuickLaunch = new NativeQuickLaunchService(basePath, Dispatcher);
            _nativeQuickLaunch.LaunchRequested += OnNativeQuickLaunchRequested;
        }

        private void OnNativeQuickLaunchRequested(string title, string path)
        {
            try
            {
                bool isDir = Directory.Exists(path);
                var icon = IconHelper.GetIcon(path, isDir, 48);
                LaunchHudWindow.ShowLaunch(title, icon);
                OpenOrActivate(title, path);
                AddToRecentFiles(path);
            }
            catch (Exception ex)
            {
                ShowToast($"启动失败: {ex.Message}");
            }
        }

        private void StartAhkProcess()
        {
            string ahkPath = Path.Combine(basePath, "lightspeed.ahk");
            if (!File.Exists(ahkPath)) return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = ahkPath,
                UseShellExecute = true
            });
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private void OpenOrActivate(string title, string path)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                var sb = new StringBuilder(512);
                GetWindowText(hWnd, sb, sb.Capacity);
                string windowTitle = sb.ToString();
                if (!string.IsNullOrEmpty(windowTitle) &&
                    windowTitle.Contains(title, StringComparison.OrdinalIgnoreCase))
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            if (found != IntPtr.Zero)
            {
                ShowWindow(found, 9); // SW_RESTORE
                SetForegroundWindow(found);
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }


        private const string ReleasesPageUrl = "https://github.com/cornradio/Lightspeed-wpf/releases";
        private const string ReleasesApiUrl = "https://api.github.com/repos/cornradio/Lightspeed-wpf/releases/latest";
        private static readonly System.Net.Http.HttpClient _updateHttpClient = CreateUpdateHttpClient();

        private static System.Net.Http.HttpClient CreateUpdateHttpClient()
        {
            var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Lightspeed-wpf-update-check");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn) btn.IsEnabled = false;
            TxtUpdateStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88));
            TxtUpdateStatus.Text = "检查中...";

            try
            {
                string json = await _updateHttpClient.GetStringAsync(ReleasesApiUrl);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                string? tag = doc.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
                if (string.IsNullOrWhiteSpace(tag))
                {
                    TxtUpdateStatus.Text = "未能获取版本信息";
                    return;
                }

                var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
                if (TryParseTagVersion(tag, out var latest) && latest > current)
                {
                    TxtUpdateStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6B, 0x9F, 0xFF));
                    TxtUpdateStatus.Text = $"发现新版本 {tag}";
                    var result = WpfMessageBox.Show(
                        $"发现新版本 {tag}（当前 v{current.Major}.{current.Minor}.{current.Build}）。\n是否打开下载页面？",
                        "检查更新",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information);
                    if (result == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = ReleasesPageUrl,
                            UseShellExecute = true
                        });
                    }
                }
                else
                {
                    TxtUpdateStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
                    TxtUpdateStatus.Text = "已是最新版本";
                }
            }
            catch (Exception ex)
            {
                TxtUpdateStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0x11, 0x23));
                TxtUpdateStatus.Text = "检查失败: " + ex.Message;
            }
            finally
            {
                if (sender is System.Windows.Controls.Button btn2) btn2.IsEnabled = true;
            }
        }

        private static bool TryParseTagVersion(string tag, out Version version)
        {
            string trimmed = tag.Trim();
            if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("V"))
            {
                trimmed = trimmed.Substring(1);
            }
            return Version.TryParse(trimmed, out version!);
        }

        private const string AutoStartRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AutoStartRegistryValueName = "Lightspeed";

        private void ChkAutoStartWithWindows_Changed(object sender, RoutedEventArgs e)
        {
            bool enable = ChkAutoStartWithWindows.IsChecked ?? false;
            AppSettings.Instance.AutoStartWithWindows = enable;
            AppSettings.Instance.Save();

            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(AutoStartRegistryKey, writable: true);
                if (key == null) return;

                if (enable)
                {
                    string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AutoStartRegistryValueName, "\"" + exePath + "\"");
                    }
                }
                else
                {
                    if (key.GetValue(AutoStartRegistryValueName) != null)
                    {
                        key.DeleteValue(AutoStartRegistryValueName, throwOnMissingValue: false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("设置开机自启动失败: " + ex.Message, "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ChkHideDesktopIni_Changed(object sender, RoutedEventArgs e)
        {
            AppSettings.Instance.HideDesktopIni = ChkHideDesktopIni.IsChecked ?? false;
            AppSettings.Instance.Save();
            ClearFolderCache(currentFolder);
            NavigateToFolder(currentFolder);
        }

        private void ChkHideExtensions_Changed(object sender, RoutedEventArgs e)
        {
            AppSettings.Instance.HideExtensions = ChkHideExtensions.IsChecked ?? false;
            AppSettings.Instance.Save();
            ClearFolderCache(currentFolder);
            NavigateToFolder(currentFolder);
        }

        private void ChkSingleClickOpen_Changed(object sender, RoutedEventArgs e)
        {
            AppSettings.Instance.SingleClickOpen = ChkSingleClickOpen.IsChecked ?? false;
            AppSettings.Instance.Save();
        }

        private void RbToastPosition_Changed(object sender, RoutedEventArgs e)
        {
            if (_loadingSettings) return;
            if (sender is not System.Windows.Controls.RadioButton rb || rb.IsChecked != true) return;
            if (!int.TryParse(rb.Tag?.ToString(), out int pos)) return;

            AppSettings.Instance.ToastPosition = pos;
            AppSettings.Instance.Save();
            ApplyToastHostPosition();
            ShowToast("提示位置已更新");
        }

        private void ApplyToastPositionRadio(int position)
        {
            switch (position)
            {
                case 1: RbToastPos1.IsChecked = true; break;
                case 2: RbToastPos2.IsChecked = true; break;
                case 3: RbToastPos3.IsChecked = true; break;
                case 4: RbToastPos4.IsChecked = true; break;
                case 5: RbToastPos5.IsChecked = true; break;
                default: RbToastPos0.IsChecked = true; break;
            }
        }

        private void ApplyToastHostPosition()
        {
            if (ToastHost == null) return;
            int pos = AppSettings.Instance.ToastPosition;
            const double margin = 16;

            switch (pos)
            {
                case 1: // 左上
                    ToastHost.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                    ToastHost.VerticalAlignment = VerticalAlignment.Top;
                    ToastHost.Margin = new Thickness(margin);
                    break;
                case 2: // 右上
                    ToastHost.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                    ToastHost.VerticalAlignment = VerticalAlignment.Top;
                    ToastHost.Margin = new Thickness(margin);
                    break;
                case 3: // 正下
                    ToastHost.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                    ToastHost.VerticalAlignment = VerticalAlignment.Bottom;
                    ToastHost.Margin = new Thickness(margin);
                    break;
                case 4: // 左下
                    ToastHost.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                    ToastHost.VerticalAlignment = VerticalAlignment.Bottom;
                    ToastHost.Margin = new Thickness(margin);
                    break;
                case 5: // 右下
                    ToastHost.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                    ToastHost.VerticalAlignment = VerticalAlignment.Bottom;
                    ToastHost.Margin = new Thickness(margin);
                    break;
                default: // 0 正上
                    ToastHost.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                    ToastHost.VerticalAlignment = VerticalAlignment.Top;
                    ToastHost.Margin = new Thickness(margin);
                    break;
            }
        }

        private void ChkDisableInFullscreen_Changed(object sender, RoutedEventArgs e)
        {
            AppSettings.Instance.DisableHotkeyInFullscreen = ChkDisableInFullscreen.IsChecked ?? true;
            AppSettings.Instance.Save();
        }

        private void Grid_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Grid_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                string targetFolder = Path.Combine(basePath, currentFolder.ToString());
                
                foreach (string file in files)
                {
                    try
                    {
                        string fileName = Path.GetFileName(file);
                        string destPath = Path.Combine(targetFolder, fileName);

                        if (Directory.Exists(file))
                        {
                            if (Directory.Exists(destPath))
                                Directory.Delete(destPath, true);
                            Directory.Move(file, destPath);
                        }
                        else
                        {
                            if (File.Exists(destPath))
                                File.Delete(destPath);
                            File.Move(file, destPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        WpfMessageBox.Show($"移动文件失败: {ex.Message}");
                    }
                }
                
                ClearFolderCache(currentFolder);
                NavigateToFolder(currentFolder);
            }
        }

        private System.Windows.Point _dragStartPoint;
        private DateTime _lastSingleClickOpenTime = DateTime.MinValue;
        private string? _lastSingleClickOpenPath;

        private void ListView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!AppSettings.Instance.SingleClickOpen) return;
            if (editingItem != null) return;

            if (e.OriginalSource is FrameworkElement element && element.DataContext is FileItem item)
            {
                if ((DateTime.Now - _lastSingleClickOpenTime).TotalMilliseconds < 400 &&
                    _lastSingleClickOpenPath == item.FullPath)
                {
                    e.Handled = true;
                    return;
                }
                _lastSingleClickOpenTime = DateTime.Now;
                _lastSingleClickOpenPath = item.FullPath;
                OpenItem(item);
                e.Handled = true;
            }
        }

        private void ListView_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
            {
                _dragStartPoint = e.GetPosition(null);
                return;
            }

            System.Windows.Point pos = e.GetPosition(null);
            System.Windows.Vector diff = _dragStartPoint - pos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var control = sender as System.Windows.Controls.Primitives.Selector;
                if (control == null) return;
                var item = control.SelectedItem as FileItem;
                if (item == null) return;

                System.Windows.DragDrop.DoDragDrop(control, item.FullPath, System.Windows.DragDropEffects.Move);
            }
        }

        private void FolderButton_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ||
                e.Data.GetDataPresent(System.Windows.DataFormats.Text))
            {
                e.Effects = System.Windows.DragDropEffects.Move;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void FolderButton_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (sender is not WpfButton btn) return;
            int targetFolderNum = folderButtons.IndexOf(btn);
            if (targetFolderNum < 0) return;
            string targetFolder = Path.Combine(basePath, targetFolderNum.ToString());

            try
            {
                if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                {
                    // 从外部拖入的文件/文件夹
                    string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                    foreach (string file in files)
                    {
                        MoveItemToFolder(file, targetFolder);
                    }
                    ClearFolderCache(currentFolder);
                }
                else if (e.Data.GetDataPresent(System.Windows.DataFormats.Text))
                {
                    // 从列表内部拖拽的项目
                    string sourcePath = (string)e.Data.GetData(System.Windows.DataFormats.Text);
                    if (targetFolderNum == currentFolder)
                    {
                        ShowToast("已在当前文件夹");
                        return;
                    }
                    MoveItemToFolder(sourcePath, targetFolder);
                    ClearFolderCache(currentFolder);
                    ShowToast($"已移动到文件夹 {targetFolderNum}");
                }

                ClearFolderCache(targetFolderNum);
                NavigateToFolder(currentFolder);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"移动失败: {ex.Message}");
            }
        }

        private void MoveItemToFolder(string sourcePath, string targetFolder)
        {
            string name = Path.GetFileName(sourcePath);
            string destPath = Path.Combine(targetFolder, name);

            if (Directory.Exists(sourcePath))
            {
                if (Directory.Exists(destPath))
                    Directory.Delete(destPath, true);
                Directory.Move(sourcePath, destPath);
            }
            else
            {
                if (File.Exists(destPath))
                    File.Delete(destPath);
                File.Move(sourcePath, destPath);
            }
        }

        private void BtnCaptureHotkey_Click(object sender, RoutedEventArgs e)
        {
            isCapturingKey = true;
            TxtHotkey.Text = "请按任意键...";
            Focus();
        }

        private void CaptureHotkey_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;
            isCapturingKey = false;
            
            if (e.Key == Key.Escape)
            {
                UpdateHotkeyDisplay();
                return;
            }
            
            Key key = e.Key;
            if (key == Key.System)
            {
                key = e.SystemKey;
            }
            
            if (key == Key.LWin || key == Key.RWin || key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt)
            {
                TxtHotkey.Text = "请按一个非修饰键 (含修饰键)...";
                isCapturingKey = true;
                return;
            }
            
            uint modifiers = 0;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= 0x0001;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= 0x0002;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= 0x0004;
            
            if (modifiers == 0)
            {
                modifiers = 0x0001;
                key = Key.S;
            }
            
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            currentModifiers = modifiers;
            currentKey = vk;

            AppSettings.Instance.HotkeyModifiers = (int)modifiers;
            AppSettings.Instance.HotkeyKey = (int)vk;
            AppSettings.Instance.Save();
            
            UnregisterHotKey(windowHandle, HOTKEY_ID);
            RegisterHotKey(windowHandle, HOTKEY_ID, currentModifiers, currentKey);

            UpdateHotkeyDisplay();
        }

        private void BtnCaptureSearchHotkey_Click(object sender, RoutedEventArgs e)
        {
            isCapturingSearchKey = true;
            TxtSearchHotkey.Text = "请按任意键...";
            Focus();
        }

        private void CaptureSearchHotkey_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;
            isCapturingSearchKey = false;

            if (e.Key == Key.Escape)
            {
                UpdateSearchHotkeyDisplay();
                return;
            }

            Key key = e.Key;
            if (key == Key.System)
            {
                key = e.SystemKey;
            }

            if (key == Key.LWin || key == Key.RWin || key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt)
            {
                TxtSearchHotkey.Text = "请按一个非修饰键 (含修饰键)...";
                isCapturingSearchKey = true;
                return;
            }

            uint modifiers = 0;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= 0x0001;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= 0x0002;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= 0x0004;

            if (modifiers == 0)
            {
                modifiers = 0x0005;
                key = Key.Space;
            }

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            searchModifiers = modifiers;
            searchKey = vk;

            AppSettings.Instance.SearchHotkeyModifiers = (int)modifiers;
            AppSettings.Instance.SearchHotkeyKey = (int)vk;
            AppSettings.Instance.Save();

            UnregisterHotKey(windowHandle, SEARCH_HOTKEY_ID);
            RegisterHotKey(windowHandle, SEARCH_HOTKEY_ID, searchModifiers, searchKey);

            UpdateSearchHotkeyDisplay();
        }

        public void CreateAHK(string folderPath)
        {
            string ahkFilePath = Path.Combine(folderPath, "lightspeed.ahk");

            string ahkContent = @"
#SingleInstance , Force
Menu, Tray, Icon, netshell.dll, 30 
#If  WinActive(""ahk_exe lightspeed-UI.exe"") 
XButton1::Send, {alt down}{Left}{alt up}
XButton2::Send, {alt down}{right}{alt up}
#If WinActive(""ahk_class Shell_TrayWnd"") or WinActive(""ahk_class Shell_SecondaryTrayWnd"") or WinActive(""python  lightspeed.py"") or WinActive(""ahk_class WorkerW"")  or WinActive(""ahk_class Progman"")
SetTitleMatchMode, 2

ShowAndHideText(text, duration) {
    Gui, +LastFound +AlwaysOnTop -Caption +ToolWindow +Disabled
    Gui, Color, 000000 ; background black
    Gui, Font, s15, Verdana ; fontsize and fontname

    textWidth := 400
    textHeight := 40
    winX := 0
    winY := 20

    Gui, Add, Text, x%winX% y%winY% w%textWidth% h%textHeight% cFFFFFF Center, %text%
    Gui, Show, NA
    WinSet, Transparent, 180 ; 0 is fully transparent, 255 is fully opaque

    SetTimer, DestroyGui, %duration%
    return

    DestroyGui:
    Gui, Destroy
    return
}

open_or_activate(title, path) {
    ShowAndHideText(title, 600)
    if (WinExist(title)) {
        WinActivate, %title%
    } else {
        Run, ""%path%""
        }
}

!0::
open_or_activate(""C:\lightspeed\0"",""C:\lightspeed\0"")
return
!1::
open_or_activate(""C:\lightspeed\1"",""C:\lightspeed\1"")
return
!2::
open_or_activate(""C:\lightspeed\2"",""C:\lightspeed\2"")
return
!3::
open_or_activate(""C:\lightspeed\3"",""C:\lightspeed\3"")
return
!4::
open_or_activate(""C:\lightspeed\4"",""C:\lightspeed\4"")
return
!5::
open_or_activate(""C:\lightspeed\5"",""C:\lightspeed\5"")
return
!6::
open_or_activate(""C:\lightspeed\6"",""C:\lightspeed\6"")
return
!7::
open_or_activate(""C:\lightspeed\7"",""C:\lightspeed\7"")
return
!8::
open_or_activate(""C:\lightspeed\8"",""C:\lightspeed\8"")
return
!9::
open_or_activate(""C:\lightspeed\9"",""C:\lightspeed\9"")
return
";

            var lightspeed_obj_list = LoadFolder2objList(folderPath);
            
            StringBuilder sb = new StringBuilder();
            sb.Append("--- Hotkeys ---\n");
            sb.Append("Alt+0~9 : Open Folders\n");
            foreach (var item in lightspeed_obj_list)
            {
                sb.Append($"{item.HotkeyStr} : {item.Title}\n");
            }
            string helpStringText = sb.ToString();
            File.WriteAllText(Path.Combine(folderPath, "help.txt"), helpStringText);

            ahkContent += $@"
!/::
Run, ""{System.AppContext.BaseDirectory}Lightspeed-wpf.exe"" --help
return
" + "\n";

            foreach (var item in lightspeed_obj_list)
            {
                ahkContent += (item.getAhkString());
                ahkContent += "\n";
            }

            System.IO.File.WriteAllText(ahkFilePath, ahkContent, System.Text.Encoding.GetEncoding("GBK"));
        }

        public List<lightspeed_obj> LoadFolder2objList(string folderPath)
        {
            var lightspeed_obj_list = new List<lightspeed_obj>();
            var dubcheckList = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                var tf = folderPath + "\\" + i;
                if (!Directory.Exists(tf)) continue;
                var entries = Directory.GetFileSystemEntries(tf);

                foreach (var file in entries)
                {
                    var hotkey = "";
                    var title = "";
                    if (Path.GetFileName(file) == "desktop.ini") continue;

                    if (Path.GetFileName(file)[0] >= 'A' && Path.GetFileName(file)[0] <= 'Z' || Path.GetFileName(file)[0] >= 'a' && Path.GetFileName(file)[0] <= 'z')
                    {
                        hotkey = i + " & " + char.ToLower(Path.GetFileName(file)[0]);
                    }
                    else
                    {
                        Console.WriteLine($"nope{Path.GetFileName(file)}");
                        continue;
                    }

                    if (Path.GetFileName(file).StartsWith("["))
                    {
                        hotkey = i + " & " + char.ToLower(Path.GetFileName(file)[1]);
                        title = Path.GetFileName(file).Substring(3).Replace(".lnk", "").Replace(" - 快捷方式", "").Replace(" - 副本", "");
                    }

                    if (dubcheckList.Contains(hotkey))
                    {
                        Console.WriteLine($"{Path.GetFileName(file)} :dublicated key");
                        continue;
                    }

                    dubcheckList.Add(hotkey);
                    title = System.IO.Path.GetFileNameWithoutExtension(file).Replace(" - 快捷方式", "").Replace(" - 副本", "");
                    var filepath = Path.Combine(folderPath, file);
                    lightspeed_obj_list.Add(new lightspeed_obj(title, filepath, hotkey));

                }
            }

            return lightspeed_obj_list;
        }

        private void BtnOpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            ShowToast("已在资源管理器中打开");
            string currentPath = Path.Combine(basePath, currentFolder.ToString());
            if (Directory.Exists(currentPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", currentPath);
            }
        }

        private void SliderIconSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtIconSize == null) return;
            TxtIconSize.Text = ((int)SliderIconSize.Value).ToString();
            _iconSizeDirty = true;
        }

        private void SliderIconSizeIcon_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtIconSizeIcon == null) return;
            TxtIconSizeIcon.Text = ((int)SliderIconSizeIcon.Value).ToString();
            _iconSizeDirty = true;
        }

        private void TxtIconSize_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtIconSize.Text, out int val))
            {
                val = Math.Clamp(val, (int)SliderIconSize.Minimum, (int)SliderIconSize.Maximum);
                SliderIconSize.Value = val;
                TxtIconSize.Text = val.ToString();
                _iconSizeDirty = true;
            }
            else
            {
                TxtIconSize.Text = ((int)SliderIconSize.Value).ToString();
            }
        }

        private void TxtIconSize_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TxtIconSize_LostFocus(sender, e);
                Keyboard.ClearFocus();
                Focus();
            }
        }

        private void TxtIconSizeIcon_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtIconSizeIcon.Text, out int val))
            {
                val = Math.Clamp(val, (int)SliderIconSizeIcon.Minimum, (int)SliderIconSizeIcon.Maximum);
                SliderIconSizeIcon.Value = val;
                TxtIconSizeIcon.Text = val.ToString();
                _iconSizeDirty = true;
            }
            else
            {
                TxtIconSizeIcon.Text = ((int)SliderIconSizeIcon.Value).ToString();
            }
        }

        private void TxtIconSizeIcon_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TxtIconSizeIcon_LostFocus(sender, e);
                Keyboard.ClearFocus();
                Focus();
            }
        }

        private void SaveAndClearIconCache()
        {
            listIconSize = (int)SliderIconSize.Value;
            iconIconSize = (int)SliderIconSizeIcon.Value;
            AppSettings.Instance.ListIconSize = (int)listIconSize;
            AppSettings.Instance.IconIconSize = (int)iconIconSize;
            AppSettings.Instance.Save();
            IconHelper.ClearCache();
            folderCache.Clear();
            folderCacheTime.Clear();
        }

        private void ApplyIconSizeChanges()
        {
            SaveAndClearIconCache();
            NavigateToFolder(currentFolder);
        }

        private void BtnListView_Click(object sender, RoutedEventArgs e)
        {
            ShowToast("已切换到列表视图");
            isListView = true;
            FileListView.Visibility = Visibility.Visible;
            IconListView.Visibility = Visibility.Collapsed;
            BtnListView.IsChecked = true;
            BtnIconView.IsChecked = false;
            AppSettings.Instance.IsListView = true;
            AppSettings.Instance.Save();
        }

        private void BtnIconView_Click(object sender, RoutedEventArgs e)
        {
            ShowToast("已切换到网格视图");
            isListView = false;
            FileListView.Visibility = Visibility.Collapsed;
            IconListView.Visibility = Visibility.Visible;
            BtnListView.IsChecked = false;
            BtnIconView.IsChecked = true;
            AppSettings.Instance.IsListView = false;
            AppSettings.Instance.Save();
        }

        private void FileListView_MouseRightClick(object sender, MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as FrameworkElement;
            var item = element?.DataContext as FileItem;
            if (item != null)
            {
                FileListView.SelectedItem = item;
                ShowContextMenu(item, FileListView);
            }
            e.Handled = true;
        }

        private void IconListView_MouseRightClick(object sender, MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as FrameworkElement;
            var item = element?.DataContext as FileItem;
            if (item != null)
            {
                IconListView.SelectedItem = item;
                ShowContextMenu(item, IconListView);
            }
            e.Handled = true;
        }

        private void ShowContextMenu(FileItem item, FrameworkElement target, bool atItem = false)
        {
            ContextMenu menu = new ContextMenu();
            menu.PlacementTarget = target;
            menu.Placement = atItem ? PlacementMode.Bottom : PlacementMode.Mouse;

            MenuItem openItem = new MenuItem { Header = "打开" };
            openItem.Click += (s, args) => OpenItem(item);
            menu.Items.Add(openItem);

            if (item.IsDirectory)
            {
                MenuItem openFolderItem = new MenuItem { Header = "在文件资源管理器中打开" };
                openFolderItem.Click += (s, args) => OpenInExplorer(item.FullPath);
                menu.Items.Add(openFolderItem);
            }
            else
            {
                MenuItem openWithItem = new MenuItem { Header = "打开方式..." };
                openWithItem.Click += (s, args) => OpenWith(item.FullPath);
                menu.Items.Add(openWithItem);

                if (item.FullPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    MenuItem openLocationItem = new MenuItem { Header = "打开文件所在位置" };
                    openLocationItem.Click += (s, args) => OpenShortcutTargetLocation(item.FullPath);
                    menu.Items.Add(openLocationItem);
                }
            }

            menu.Items.Add(new Separator());

            MenuItem renameItem = new MenuItem { Header = "重命名" };
            renameItem.Click += (s, args) => RenameItem(item);
            menu.Items.Add(renameItem);

            MenuItem deleteItem = new MenuItem { Header = "删除" };
            deleteItem.Click += (s, args) => DeleteItem(item);
            menu.Items.Add(deleteItem);

            menu.Items.Add(new Separator());

            MenuItem copyItem = new MenuItem { Header = "复制路径" };
            copyItem.Click += (s, args) => WpfClipboard.SetText(item.FullPath);
            menu.Items.Add(copyItem);

            // 手柄菜单追踪
            menu.Closed += (s, args) => { _activeMenu = null; _menuIndex = -1; };
            _activeMenu = menu;
            _menuIndex = 0;
            menu.IsOpen = true;

            // 自动高亮第一项
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var items = menu.Items.OfType<MenuItem>().ToList();
                HighlightMenuItem(items);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void HighlightMenuItem(List<MenuItem> items)
        {
            if (_activeMenu == null) return;
            // 清除所有高亮
            foreach (var obj in _activeMenu.Items)
            {
                if (obj is MenuItem mi)
                {
                    mi.Background = System.Windows.Media.Brushes.Transparent;
                    mi.Foreground = System.Windows.Media.Brushes.White;
                }
            }
            // 高亮当前项
            if (_menuIndex >= 0 && _menuIndex < items.Count)
            {
                var highlighted = items[_menuIndex];
                if (highlighted != null)
                {
                    highlighted.Background = new System.Windows.Media.SolidColorBrush(GetAccentColor());
                    highlighted.Foreground = System.Windows.Media.Brushes.White;
                    highlighted.Focus();
                }
            }
        }

        private void RenameItem(FileItem item)
        {
            editingItem = item;
            item.IsEditing = true;
            if (isListView)
            {
                FileListView.SelectedItem = item;
                var container = FileListView.ItemContainerGenerator.ContainerFromItem(item) as WpfListViewItem;
                if (container != null)
                {
                    var textBox = FindVisualChild<WpfTextBox>(container);
                    if (textBox != null)
                    {
                        textBox.Text = item.Name;
                        textBox.Focus();
                        textBox.SelectAll();
                    }
                }
            }
            else
            {
                IconListView.SelectedItem = item;
                var container = IconListView.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                if (container != null)
                {
                    var textBox = FindVisualChild<WpfTextBox>(container);
                    if (textBox != null)
                    {
                        textBox.Text = item.Name;
                        textBox.Focus();
                        textBox.SelectAll();
                    }
                }
            }
        }

        private void EditTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (editingItem != null && editingItem.IsEditing)
            {
                if (sender is WpfTextBox textBox)
                {
                    CommitRename(editingItem, textBox.Text);
                }
                editingItem.IsEditing = false;
                editingItem = null;
            }
        }

        private void EditTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (sender is WpfTextBox textBox)
                {
                    FileItem? item = textBox.DataContext as FileItem;
                    if (item != null && editingItem != null)
                    {
                        string newName = textBox.Text;
                        editingItem.IsEditing = false;
                        editingItem = null;
                        CommitRename(item, newName);
                    }
                }
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                if (editingItem != null)
                {
                    editingItem.IsEditing = false;
                    editingItem = null;
                }
                e.Handled = true;
            }
        }

        private void CommitRename(FileItem item, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName) || newName == item.Name)
            {
                return;
            }

            try
            {
                string dir = Path.GetDirectoryName(item.FullPath) ?? "";
                string originalName = Path.GetFileName(item.FullPath);
                if (AppSettings.Instance.HideExtensions && !item.IsDirectory && originalName.Contains('.'))
                {
                    string extension = originalName.Substring(originalName.LastIndexOf('.'));
                    newName = newName + extension;
                }
                string newPath = Path.Combine(dir, newName);
                if (item.IsDirectory)
                {
                    Directory.Move(item.FullPath, newPath);
                }
                else
                {
                    File.Move(item.FullPath, newPath);
                }
                ClearFolderCache(currentFolder);
                NavigateToFolder(currentFolder);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"重命名失败: {ex.Message}");
            }
        }

        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    return result;
                }
                var grandChild = FindVisualChild<T>(child);
                if (grandChild != null)
                {
                    return grandChild;
                }
            }
            return null;
        }

        private void DeleteItem(FileItem item)
        {
            try
            {
                int deletedIndex = -1;
                if (isListView)
                {
                    deletedIndex = FileListView.Items.IndexOf(item);
                }
                else
                {
                    deletedIndex = IconListView.Items.IndexOf(item);
                }

                MoveToRecycleBin(item.FullPath);
                ClearFolderCache(currentFolder);
                NavigateToFolder(currentFolder);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var items = isListView ? FileListView.Items : IconListView.Items;
                    if (items.Count > 0)
                    {
                        int selectIndex = deletedIndex;
                        if (selectIndex >= items.Count)
                        {
                            selectIndex = items.Count - 1;
                        }
                        if (selectIndex < 0) selectIndex = 0;
                        
                        if (isListView)
                        {
                            FileListView.SelectedIndex = selectIndex;
                            var container = FileListView.ItemContainerGenerator.ContainerFromIndex(selectIndex) as System.Windows.Controls.ListViewItem;
                            if (container != null)
                            {
                                container.Focus();
                            }
                        }
                        else
                        {
                            IconListView.SelectedIndex = selectIndex;
                            var container = IconListView.ItemContainerGenerator.ContainerFromIndex(selectIndex) as System.Windows.Controls.ListBoxItem;
                            if (container != null)
                            {
                                container.Focus();
                            }
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"删除失败: {ex.Message}");
            }
        }

        private void MoveToRecycleBin(string path)
        {
            SHFILEOPSTRUCT fs = new SHFILEOPSTRUCT();
            fs.wFunc = FO_DELETE;
            fs.pFrom = path + "\0\0";
            fs.fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT);
            SHFileOperation(ref fs);
        }

        private void OpenInExplorer(string path)
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }

        private bool IsInputFocused()
        {
            var focused = System.Windows.Input.FocusManager.GetFocusedElement(this);
            if (focused is System.Windows.Controls.TextBox)
                return true;
            if (focused is System.Windows.Controls.PasswordBox)
                return true;
            return false;
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (isCapturingKey)
            {
                CaptureHotkey_KeyDown(sender, e);
                e.Handled = true;
                return;
            }

            if (isCapturingSearchKey)
            {
                CaptureSearchHotkey_KeyDown(sender, e);
                e.Handled = true;
                return;
            }

            // 设置面板打开时, Esc 关闭设置返回主页面
            if (SettingsPanel.Visibility == Visibility.Visible && e.Key == Key.Escape)
            {
                ToggleSettingsPanel();
                e.Handled = true;
                return;
            }

            if (editingItem != null)
            {
                return;
            }

            // Ctrl+P: 打开搜索
            if (e.Key == Key.P && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                GetSearchWindow().ShowSearch();
                e.Handled = true;
                return;
            }

            // Shift + 左右: 切换文件夹
            if ((e.Key == Key.Left || e.Key == Key.Right) &&
                (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
            {
                if (e.Key == Key.Left)
                    NavigateToFolder(currentFolder > 0 ? currentFolder - 1 : 9);
                else
                    NavigateToFolder(currentFolder < 9 ? currentFolder + 1 : 0);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete)
            {
                if (isListView && FileListView.SelectedItem is FileItem fileItem2)
                {
                    DeleteItem(fileItem2);
                    e.Handled = true;
                }
                else if (!isListView && IconListView.SelectedItem is FileItem iconItem2)
                {
                    DeleteItem(iconItem2);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Enter)
            {
                if (isListView && FileListView.SelectedItem is FileItem listItem)
                {
                    OpenItem(listItem);
                    e.Handled = true;
                }
                else if (!isListView && IconListView.SelectedItem is FileItem iconItem)
                {
                    OpenItem(iconItem);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.F2)
            {
                if (isListView && FileListView.SelectedItem is FileItem fileItem1)
                {
                    RenameItem(fileItem1);
                    e.Handled = true;
                }
                else if (!isListView && IconListView.SelectedItem is FileItem iconItem1)
                {
                    RenameItem(iconItem1);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Down || e.Key == Key.Up || e.Key == Key.Left || e.Key == Key.Right)
            {
                if (isListView)
                {
                    if (FileListView.Items.Count == 0) { e.Handled = true; return; }
                    int idx = FileListView.SelectedIndex;
                    if (idx < 0) idx = (e.Key == Key.Down || e.Key == Key.Right) ? -1 : FileListView.Items.Count;
                    int delta = (e.Key == Key.Down || e.Key == Key.Right) ? 1 : -1;
                    int newIdx = Math.Clamp(idx + delta, 0, FileListView.Items.Count - 1);
                    FileListView.SelectedIndex = newIdx;
                    FileListView.ScrollIntoView(FileListView.SelectedItem);
                }
                else
                {
                    if (IconListView.Items.Count == 0) { e.Handled = true; return; }
                    int idx = IconListView.SelectedIndex;
                    if (idx < 0) idx = (e.Key == Key.Down || e.Key == Key.Right) ? -1 : IconListView.Items.Count;

                    if (e.Key == Key.Up || e.Key == Key.Down)
                    {
                        int cols = GetIconViewColumns();
                        if (e.Key == Key.Up)
                            idx = idx >= cols ? idx - cols : 0;
                        else
                            idx = Math.Min(idx + cols, IconListView.Items.Count - 1);
                    }
                    else
                    {
                        int delta = e.Key == Key.Right ? 1 : -1;
                        idx = Math.Clamp(idx + delta, 0, IconListView.Items.Count - 1);
                    }

                    IconListView.SelectedIndex = idx;
                    IconListView.ScrollIntoView(IconListView.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.OemComma)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    ToggleSettingsPanel();
                }
                else
                {
                    int prevFolder = currentFolder > 0 ? currentFolder - 1 : 9;
                    NavigateToFolder(prevFolder);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.OemPeriod)
            {
                int nextFolder = currentFolder < 9 ? currentFolder + 1 : 0;
                NavigateToFolder(nextFolder);
                e.Handled = true;
            }
            else if (e.Key >= Key.A && e.Key <= Key.Z)
            {
                char searchChar = (char)('a' + (e.Key - Key.A));
                SelectNextItemByChar(searchChar);
                e.Handled = true;
            }
            else if (e.Key >= Key.D0 && e.Key <= Key.D9)
            {
                // 如果焦点在文本输入框内（设置面板的别名/W/H等），则不拦截
                if (IsInputFocused()) { return; }
                int num = e.Key - Key.D0;
                NavigateToFolder(num);
                e.Handled = true;
            }
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            {
                // 如果焦点在文本输入框内（设置面板的别名/W/H等），则不拦截
                if (IsInputFocused()) { return; }
                int num = e.Key - Key.NumPad0;
                NavigateToFolder(num);
                e.Handled = true;
            }
        }

        private void SelectNextItemByChar(char c)
        {
            var items = isListView ? FileListView.Items : IconListView.Items;
            int startIndex = -1;

            if (isListView && FileListView.SelectedItem != null)
            {
                startIndex = FileListView.Items.IndexOf(FileListView.SelectedItem);
            }
            else if (!isListView && IconListView.SelectedItem != null)
            {
                startIndex = IconListView.Items.IndexOf(IconListView.SelectedItem);
            }

            for (int i = 1; i <= items.Count; i++)
            {
                int index = (startIndex + i) % items.Count;
                var item = items[index] as FileItem;
                if (item != null && char.ToLower(item.Name[0]) == c)
                {
                    if (isListView)
                    {
                        FileListView.SelectedItem = item;
                        FileListView.ScrollIntoView(item);
                    }
                    else
                    {
                        IconListView.SelectedItem = item;
                        IconListView.ScrollIntoView(item);
                    }
                    break;
                }
            }
        }

        private void OpenWith(string path)
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = $"shell32.dll,OpenAs_RunDLL {path}"
            };
            System.Diagnostics.Process.Start(psi);
        }

        private string? GetShortcutTarget(string lnkPath)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                    return null;

                object? shell = Activator.CreateInstance(shellType);
                if (shell == null)
                    return null;

                object? shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                if (shortcut == null)
                {
                    Marshal.ReleaseComObject(shell);
                    return null;
                }

                string? targetPath = shortcut.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;
                
                Marshal.ReleaseComObject(shortcut);
                Marshal.ReleaseComObject(shell);
                
                return targetPath;
            }
            catch
            {
                return null;
            }
        }

        private void OpenShortcutTargetLocation(string lnkPath)
        {
            string? targetPath = GetShortcutTarget(lnkPath);
            if (string.IsNullOrEmpty(targetPath))
                return;

            string? folderPath = Directory.Exists(targetPath) ? targetPath : Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", folderPath);
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                ForceClose();
            }
            else
            {
                Close();
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && notifyIcon != null)
            {
                Hide();
                notifyIcon.Visible = true;
                notifyIcon.ShowBalloonTip(1000, "Lightspeed", "程序已最小化到托盘", Forms.ToolTipIcon.Info);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
            if (notifyIcon != null)
            {
                notifyIcon.Visible = true;
                notifyIcon.ShowBalloonTip(500, "Lightspeed", "程序仍在后台运行", Forms.ToolTipIcon.Info);
            }
        }

        public void ForceClose()
        {
            _gamepadTimer?.Stop();
            _nativeQuickLaunch?.Dispose();
            _nativeQuickLaunch = null;
            System.Environment.Exit(0);
        }

        // ========================
        // 手柄开关
        // ========================

        private void BtnGamepadToggle_Click(object sender, RoutedEventArgs e)
        {
            _gamepadEnabled = BtnGamepadToggle.IsChecked == true;
            ShowToast(_gamepadEnabled ? "手柄控制已开启" : "手柄控制已关闭");
        }

        // ========================
        // 搜索功能 (独立 SearchWindow)
        // ========================

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            GetSearchWindow().ShowSearch();
        }

        private void AddToRecentFiles(string path)
        {
            var recent = AppSettings.Instance.RecentFiles;
            recent.Remove(path);
            recent.Insert(0, path);
            if (recent.Count > 20)
                recent.RemoveRange(20, recent.Count - 20);
            AppSettings.Instance.Save();
        }

        private int? GetParentFolderNum(string path)
        {
            string? current = Path.GetDirectoryName(path);
            while (current != null && current != basePath)
            {
                string name = Path.GetFileName(current);
                if (int.TryParse(name, out int num) && num >= 0 && num <= 9)
                    return num;
                current = Path.GetDirectoryName(current);
            }
            return null;
        }

        private void SelectItemByPath(string fullPath)
        {
            var items = isListView ? FileListView.Items : (System.Windows.Controls.ItemCollection)IconListView.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is FileItem fi && fi.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (isListView)
                    {
                        FileListView.SelectedIndex = i;
                        FileListView.ScrollIntoView(FileListView.Items[i]);
                    }
                    else
                    {
                        IconListView.SelectedIndex = i;
                        IconListView.ScrollIntoView(IconListView.Items[i]);
                    }
                    break;
                }
            }
        }
    }

    public class BoolToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (Visibility)value == Visibility.Visible;
        }
    }

    public class InverseBoolToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (Visibility)value != Visibility.Visible;
        }
    }

    public class FileItem : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private string _name = "";
        private bool _isEditing = false;
        private bool _justOpened = false;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged("Name"); }
        }

        public string FullPath { get; set; } = "";
        public bool IsDirectory { get; set; }

        private ImageSource? _icon;
        public ImageSource? Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged("Icon"); }
        }

        private double _iconSize = 48;
        public double IconSize
        {
            get => _iconSize;
            set { _iconSize = value; OnPropertyChanged("IconSize"); }
        }

        public bool JustOpened
        {
            get => _justOpened;
            set { _justOpened = value; OnPropertyChanged("JustOpened"); }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set { _isEditing = value; OnPropertyChanged("IsEditing"); }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
