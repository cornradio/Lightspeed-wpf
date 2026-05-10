using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Lightspeed_wpf
{
    public partial class MainWindow : Window
    {
        private List<Button> folderButtons = new List<Button>();
        private int currentFolder = 0;
        private string basePath = @"C:\lightspeed";
        private double iconSize = 32;
        private double rowSpacing = 0;

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

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }
            NavigateToFolder(0);
        }

        private void NavigateToFolder(int folderNum)
        {
            currentFolder = folderNum;
            string path = Path.Combine(basePath, folderNum.ToString());

            FileListView.Items.Clear();

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
                    var item = new FileItem
                    {
                        Name = Path.GetFileName(dir),
                        FullPath = dir,
                        IsDirectory = true,
                        IconSize = iconSize,
                        RowMargin = new Thickness(0, (int)rowSpacing / 2, 0, (int)rowSpacing / 2)
                    };
                    item.Icon = GetIcon(dir, true);
                    FileListView.Items.Add(item);
                }

                foreach (var file in files)
                {
                    var item = new FileItem
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file,
                        IsDirectory = false,
                        IconSize = iconSize,
                        RowMargin = new Thickness(0, (int)rowSpacing / 2, 0, (int)rowSpacing / 2)
                    };
                    item.Icon = GetIcon(file, false);
                    FileListView.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法读取文件夹: {ex.Message}");
            }

            UpdateFolderButtonSelection(folderNum);
        }

        private ImageSource GetIcon(string path, bool isDirectory)
        {
            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_SMALLICON;
            uint attributes = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;

            IntPtr hImg = SHGetFileInfo(path, attributes, ref shfi, (uint)Marshal.SizeOf(shfi), flags);

            if (shfi.hIcon != IntPtr.Zero)
            {
                try
                {
                    using (var bitmap = new Bitmap(32, 32))
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
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
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 64))
                    : new SolidColorBrush(System.Windows.Media.Color.FromRgb(61, 61, 61));
                folderButtons[i].Foreground = (i == selectedFolder)
                    ? new SolidColorBrush(Colors.Black)
                    : new SolidColorBrush(Colors.White);
            }
        }

        private void FolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
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
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = item.FullPath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开: {ex.Message}");
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

            MessageBox.Show("文件夹创建完成！");
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
                MessageBox.Show("AHK 文件不存在，请先点击「生成 AHK」", "提示");
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
            MessageBox.Show($"AHK 已生成: {ahkFilePath}");
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
            
            iconSize = (int)SliderIconSize.Value;
            TxtIconSize.Text = ((int)iconSize).ToString();
            NavigateToFolder(currentFolder);
        }

        private void SliderRowSpacing_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtRowSpacing == null) return;
            
            rowSpacing = SliderRowSpacing.Value;
            TxtRowSpacing.Text = ((int)rowSpacing).ToString();
            NavigateToFolder(currentFolder);
        }

        private void FileListView_MouseRightClick(object sender, MouseButtonEventArgs e)
        {
            if (FileListView.SelectedItem is FileItem item)
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
                copyItem.Click += (s, args) => Clipboard.SetText(item.FullPath);
                menu.Items.Add(copyItem);

                menu.IsOpen = true;
            }
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
    }

    public class FileItem
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsDirectory { get; set; }
        public ImageSource? Icon { get; set; }
        public double IconSize { get; set; } = 32;
        public Thickness RowMargin { get; set; } = new Thickness(0);
    }
}
