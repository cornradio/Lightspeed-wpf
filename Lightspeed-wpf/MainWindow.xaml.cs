using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
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

namespace Lightspeed_wpf
{
    public partial class MainWindow : Window
    {
        private const int HOTKEY_ID = 9000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_S = 0x53;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private List<WpfButton> folderButtons = new List<WpfButton>();
        private int currentFolder = 0;
        private string basePath = @"C:\lightspeed";
        private double listIconSize = 32;
        private double iconIconSize = 64;
        private Forms.NotifyIcon? notifyIcon;
        private bool isListView = true;
        private IntPtr windowHandle;
        private HwndSource? source;

        private uint currentModifiers = MOD_ALT;
        private uint currentKey = VK_S;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

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
            notifyIcon.Visible = false;
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
            if (notifyIcon != null) notifyIcon.Visible = false;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            windowHandle = new WindowInteropHelper(this).Handle;
            source = HwndSource.FromHwnd(windowHandle);
            source?.AddHook(HwndHook);

            RegisterHotKey(windowHandle, HOTKEY_ID, currentModifiers, currentKey);

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }
            NavigateToFolder(0);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
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
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = false;
                }
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
                var dirs = Directory.GetDirectories(path);
                var files = Directory.GetFiles(path);

                foreach (var dir in dirs)
                {
                    var item = CreateFileItem(dir, true);
                    FileListView.Items.Add(item);
                    IconListView.Items.Add(item);
                }

                foreach (var file in files)
                {
                    var item = CreateFileItem(file, false);
                    FileListView.Items.Add(item);
                    IconListView.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"无法读取文件夹: {ex.Message}");
            }

            UpdateFolderButtonSelection(folderNum);
        }

        private FileItem CreateFileItem(string path, bool isDirectory)
        {
            double size = isListView ? listIconSize : iconIconSize;
            return new FileItem
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                IsDirectory = isDirectory,
                IconSize = size,
                Icon = GetIcon(path, isDirectory)
            };
        }

        private ImageSource GetIcon(string path, bool isDirectory)
        {
            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_LARGEICON;
            uint attributes = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;

            IntPtr hImg = SHGetFileInfo(path, attributes, ref shfi, (uint)Marshal.SizeOf(shfi), flags);

            if (shfi.hIcon != IntPtr.Zero)
            {
                try
                {
                    int iconSize = 64;
                    using (var bitmap = new Bitmap(iconSize, iconSize))
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        graphics.DrawIcon(System.Drawing.Icon.FromHandle(shfi.hIcon), 0, 0);
                        var hBitmap = bitmap.GetHbitmap();
                        try
                        {
                            ImageSource imageSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                hBitmap,
                                IntPtr.Zero,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            DeleteObject(hBitmap);
                            DestroyIcon(shfi.hIcon);
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
                    return CreateDefaultIcon(isDirectory);
                }
            }
            return CreateDefaultIcon(isDirectory);
        }

        private ImageSource CreateDefaultIcon(bool isFolder)
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

            RenderTargetBitmap renderBitmap = new RenderTargetBitmap(24, 24, 96, 96, PixelFormats.Pbgra32);
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
                    int folderNum;
                    if (int.TryParse(Path.GetFileName(item.FullPath), out folderNum))
                    {
                        NavigateToFolder(folderNum);
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
            NavigateToFolder(currentFolder);
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsPanel.Visibility == Visibility.Collapsed)
            {
                SettingsPanel.Visibility = Visibility.Visible;
            }
            else
            {
                SettingsPanel.Visibility = Visibility.Collapsed;
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
            CreateAHK();
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
            CreateAHK();
            BtnStartAHK_Click(sender, e);
        }

        private void CreateAHK()
        {
            string ahkFilePath = Path.Combine(basePath, "lightspeed.ahk");

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("#SingleInstance, Force");
            sb.AppendLine("Menu, Tray, Icon, netshell.dll, 30");
            sb.AppendLine("#If WinActive(\"ahk_exe Lightspeed-wpf.exe\")");
            sb.AppendLine("XButton1::Send, {Alt down}{Left}{Alt up}");
            sb.AppendLine("XButton2::Send, {Alt down}{Right}{Alt up}");
            sb.AppendLine("#If");

            for (int i = 0; i < 10; i++)
            {
                string folder = Path.Combine(basePath, i.ToString());
                sb.AppendLine($"!{i}::");
                sb.AppendLine($"Run, \"{folder}\"");
                sb.AppendLine("return");
            }

            File.WriteAllText(ahkFilePath, sb.ToString());
            WpfMessageBox.Show($"AHK 已生成: {ahkFilePath}");
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
            NavigateToFolder(currentFolder);
        }

        private void SliderIconSizeIcon_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtIconSizeIcon == null) return;
            
            iconIconSize = (int)SliderIconSizeIcon.Value;
            TxtIconSizeIcon.Text = ((int)iconIconSize).ToString();
            NavigateToFolder(currentFolder);
        }

        private void BtnListView_Click(object sender, RoutedEventArgs e)
        {
            isListView = true;
            FileListView.Visibility = Visibility.Visible;
            IconListView.Visibility = Visibility.Collapsed;
            BtnListView.IsChecked = true;
            BtnIconView.IsChecked = false;
        }

        private void BtnIconView_Click(object sender, RoutedEventArgs e)
        {
            isListView = false;
            FileListView.Visibility = Visibility.Collapsed;
            IconListView.Visibility = Visibility.Visible;
            BtnListView.IsChecked = false;
            BtnIconView.IsChecked = true;
        }

        private void FileListView_MouseRightClick(object sender, MouseButtonEventArgs e)
        {
            if (FileListView.SelectedItem is FileItem item)
            {
                ShowContextMenu(item);
            }
        }

        private void ShowContextMenu(FileItem item)
        {
            ContextMenu menu = new ContextMenu();

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
            }

            menu.Items.Add(new Separator());

            MenuItem copyItem = new MenuItem { Header = "复制路径" };
            copyItem.Click += (s, args) => WpfClipboard.SetText(item.FullPath);
            menu.Items.Add(copyItem);

            menu.IsOpen = true;
        }

        private void OpenInExplorer(string path)
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
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
            Close();
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
            source?.RemoveHook(HwndHook);
            UnregisterHotKey(windowHandle, HOTKEY_ID);
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
            System.Windows.Application.Current.Shutdown();
        }
    }

    public class FileItem
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsDirectory { get; set; }
        public ImageSource? Icon { get; set; }
        public double IconSize { get; set; } = 48;
        public bool JustOpened { get; set; }
    }
}
