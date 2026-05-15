using System.Drawing;
using System.IO;
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
        private Dictionary<string, ImageSource> iconCache = new Dictionary<string, ImageSource>();
        private Dictionary<int, DateTime> folderCacheTime = new Dictionary<int, DateTime>();

        private uint currentModifiers = 0x0001;
        private uint currentKey = 0x53;
        private bool isCapturingKey = false;
        private FileItem? editingItem = null;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, IntPtr[] apidl, uint dwFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("imm32.dll")]
        private static extern bool ImmDisableIME(IntPtr hkl);

        public MainWindow()
        {
            InitializeComponent();
            InitializeFolderButtons();
            InitializeTrayIcon();
            Loaded += MainWindow_Loaded;
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
                btn.PreviewMouseWheel += FolderButton_MouseWheel;
            }
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
            string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            if (System.IO.File.Exists(iconPath))
            {
                notifyIcon.Icon = new System.Drawing.Icon(iconPath);
            }
            else
            {
                notifyIcon.Icon = SystemIcons.Application;
            }
            notifyIcon.Text = "Lightspeed";
            notifyIcon.Visible = true;
            notifyIcon.Click += (s, e) => ShowFromTray();
            notifyIcon.DoubleClick += (s, e) => ShowFromTray();

            var contextMenu = new Forms.ContextMenuStrip();
            var showItem = new Forms.ToolStripMenuItem("显示窗口");
            showItem.Click += (s, e) => ShowFromTray();
            contextMenu.Items.Add(showItem);

            var hotkeyItem = new Forms.ToolStripMenuItem($"快捷键: Alt+S");
            hotkeyItem.Enabled = false;
            contextMenu.Items.Add(hotkeyItem);

            contextMenu.Items.Add(new Forms.ToolStripSeparator());

            var quitItem = new Forms.ToolStripMenuItem("退出");
            quitItem.Click += (s, e) => ForceClose();
            contextMenu.Items.Add(quitItem);

            notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
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
        }

        private void LoadSettings()
        {
            ChkAutoStartAHK.IsChecked = AppSettings.Instance.AutoStartAHK;
            ChkHideDesktopIni.IsChecked = AppSettings.Instance.HideDesktopIni;
            ChkHideExtensions.IsChecked = AppSettings.Instance.HideExtensions;
            ChkDisableInFullscreen.IsChecked = AppSettings.Instance.DisableHotkeyInFullscreen;
            
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

            if (AppSettings.Instance.AutoStartAHK)
            {
                CreateAHK(basePath);
                string ahkPath = Path.Combine(basePath, "lightspeed.ahk");
                if (File.Exists(ahkPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ahkPath,
                        UseShellExecute = true
                    });
                }
            }
        }

        private void UpdateHotkeyDisplay()
        {
            string modifiers = "";
            if ((currentModifiers & 0x0001) != 0) modifiers += "Alt+";
            if ((currentModifiers & 0x0002) != 0) modifiers += "Ctrl+";
            if ((currentModifiers & 0x0004) != 0) modifiers += "Shift+";
            
            string keyName = GetKeyDisplayName((Key)currentKey);
            TxtHotkey.Text = modifiers + keyName;
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
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                if (AppSettings.Instance.DisableHotkeyInFullscreen && IsFullscreenAppRunning())
                {
                    handled = true;
                    return IntPtr.Zero;
                }
                ToggleWindowVisibility();
                handled = true;
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
                    notifyIcon.ShowBalloonTip(500, "Lightspeed", "已隐藏到托盘，按 Alt+S 显示", Forms.ToolTipIcon.Info);
                }
            }
            else
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            }
        }

        private void NavigateToFolder(int folderNum)
        {
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
                Icon = GetIcon(path, isDirectory, (int)scaledSize)
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

        private ImageSource GetIcon(string path, bool isDirectory, int size)
        {
            string cacheKey = $"{path}_{size}";
            if (iconCache.ContainsKey(cacheKey))
            {
                return iconCache[cacheKey];
            }

            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_LARGEICON;
            uint attributes = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;

            IntPtr hImg = SHGetFileInfo(path, attributes, ref shfi, (uint)Marshal.SizeOf(shfi), flags);

            if (shfi.hIcon != IntPtr.Zero)
            {
                try
                {
                    using (var bitmap = new Bitmap(size, size))
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        graphics.Clear(System.Drawing.Color.Transparent);
                        IntPtr iconHandle = shfi.hIcon;
                        using (var icon = System.Drawing.Icon.FromHandle(iconHandle))
                        {
                            int iconSize = Math.Min(icon.Width, icon.Height);
                            int drawSize = Math.Min(size, Math.Max(iconSize, 16));
                            int x = (size - drawSize) / 2;
                            int y = (size - drawSize) / 2;
                            graphics.DrawIcon(icon, new Rectangle(x, y, drawSize, drawSize));
                        }
                        var hBitmap = bitmap.GetHbitmap(System.Drawing.Color.FromArgb(0, 0, 0, 0));
                        try
                        {
                            ImageSource imageSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                hBitmap,
                                IntPtr.Zero,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            DeleteObject(hBitmap);
                            DestroyIcon(shfi.hIcon);
                            iconCache[cacheKey] = imageSource;
                            return imageSource;
                        }
                        finally
                        {
                            DeleteObject(hBitmap);
                        }
                    }
                }
                catch
                {
                    return CreateDefaultIcon(isDirectory, size);
                }
            }
            return CreateDefaultIcon(isDirectory, size);
        }

        private ImageSource CreateDefaultIcon(bool isFolder, int size)
        {
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext dc = drawingVisual.RenderOpen())
            {
                if (isFolder)
                {
                    dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7)), null, new Rect(2, 6, 20, 16));
                    dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 160, 0)), null, new Rect(2, 2, 10, 6));
                }
                else
                {
                    dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(144, 202, 249)), null, new Rect(2, 2, 20, 20));
                }
            }

            RenderTargetBitmap renderBitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(drawingVisual);
            return renderBitmap;
        }

        private void UpdateFolderButtonSelection(int selectedFolder)
        {
            for (int i = 0; i < folderButtons.Count; i++)
            {
                folderButtons[i].Background = (i == selectedFolder)
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 128, 128))
                    : new SolidColorBrush(System.Windows.Media.Color.FromRgb(61, 61, 61));
                folderButtons[i].Foreground = (i == selectedFolder)
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Colors.White);
            }
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
            
            foreach (var icon in iconCache.Values)
            {
                if (icon is System.Windows.Media.Imaging.BitmapSource bitmapSource)
                {
                    bitmapSource.Freeze();
                }
            }
            iconCache.Clear();
        }

        private void FolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn)
            {
                int folderNum = folderButtons.IndexOf(btn);
                if (folderNum >= 0)
                {
                    NavigateToFolder(folderNum);
                }
            }
        }

        private void FileListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FileListView.SelectedItem is FileItem item)
            {
                OpenItem(item);
            }
        }

        private void IconListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (IconListView.SelectedItem is FileItem item)
            {
                OpenItem(item);
            }
        }

        private void OpenItem(FileItem item)
        {
            try
            {
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
            ClearFolderCache(currentFolder);
            NavigateToFolder(currentFolder);
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
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
            }
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
            CreateAHK(basePath);
            string ahkPath = Path.Combine(basePath, "lightspeed.ahk");
            if (File.Exists(ahkPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ahkPath,
                    UseShellExecute = true
                });
            }
        }

        private void ChkAutoStartAHK_Changed(object sender, RoutedEventArgs e)
        {
            AppSettings.Instance.AutoStartAHK = ChkAutoStartAHK.IsChecked ?? false;
            AppSettings.Instance.Save();
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
                        
                        if (File.Exists(destPath))
                        {
                            File.Delete(destPath);
                        }
                        
                        File.Move(file, destPath);
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

        private void BtnCaptureHotkey_Click(object sender, RoutedEventArgs e)
        {
            isCapturingKey = true;
            TxtHotkey.Text = "请按任意键...";
            PreviewKeyDown += CaptureHotkey_KeyDown;
        }

        private void CaptureHotkey_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;
            PreviewKeyDown -= CaptureHotkey_KeyDown;
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
                TxtHotkey.Text = "请按一个非修饰键";
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    isCapturingKey = true;
                    PreviewKeyDown += CaptureHotkey_KeyDown;
                }), System.Windows.Threading.DispatcherPriority.Input);
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
            
            currentModifiers = modifiers;
            currentKey = (uint)key;
            
            AppSettings.Instance.HotkeyModifiers = (int)modifiers;
            AppSettings.Instance.HotkeyKey = (int)key;
            AppSettings.Instance.Save();
            
            UnregisterHotKey(windowHandle, HOTKEY_ID);
            RegisterHotKey(windowHandle, HOTKEY_ID, currentModifiers, currentKey);
            
            UpdateHotkeyDisplay();
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
            string currentPath = Path.Combine(basePath, currentFolder.ToString());
            if (Directory.Exists(currentPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", currentPath);
            }
        }

        private void SliderIconSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtIconSize == null) return;
            
            listIconSize = (int)SliderIconSize.Value;
            TxtIconSize.Text = ((int)listIconSize).ToString();
            AppSettings.Instance.ListIconSize = (int)listIconSize;
            AppSettings.Instance.Save();
            ClearAllCache();
            NavigateToFolder(currentFolder);
        }

        private void SliderIconSizeIcon_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtIconSizeIcon == null) return;
            
            iconIconSize = (int)SliderIconSizeIcon.Value;
            TxtIconSizeIcon.Text = ((int)iconIconSize).ToString();
            AppSettings.Instance.IconIconSize = (int)iconIconSize;
            AppSettings.Instance.Save();
            ClearAllCache();
            NavigateToFolder(currentFolder);
        }

        private void BtnListView_Click(object sender, RoutedEventArgs e)
        {
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

        private void ShowContextMenu(FileItem item, FrameworkElement target)
        {
            ContextMenu menu = new ContextMenu();
            menu.PlacementTarget = target;
            menu.Placement = PlacementMode.Mouse;

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

            menu.IsOpen = true;
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

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (isCapturingKey)
            {
                e.Handled = true;
                return;
            }

            if (editingItem != null)
            {
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
                if (isListView && FileListView.Items.Count > 0)
                {
                    if (FileListView.SelectedItem == null || !FileListView.Items.Contains(FileListView.SelectedItem))
                    {
                        FileListView.SelectedItem = FileListView.Items[0];
                        FileListView.ScrollIntoView(FileListView.SelectedItem);
                    }
                }
                else if (!isListView && IconListView.Items.Count > 0)
                {
                    if (IconListView.SelectedItem == null || !IconListView.Items.Contains(IconListView.SelectedItem))
                    {
                        IconListView.SelectedItem = IconListView.Items[0];
                        IconListView.ScrollIntoView(IconListView.SelectedItem);
                    }
                }
            }
            else if (e.Key == Key.OemComma)
            {
                int prevFolder = currentFolder > 0 ? currentFolder - 1 : 9;
                NavigateToFolder(prevFolder);
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
                int num = e.Key - Key.D0;
                NavigateToFolder(num);
                e.Handled = true;
            }
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            {
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
            System.Environment.Exit(0);
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
        public ImageSource? Icon { get; set; }
        public double IconSize { get; set; } = 48;

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
