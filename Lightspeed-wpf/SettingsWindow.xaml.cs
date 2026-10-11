using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfButton = System.Windows.Controls.Button;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfMessageBox = System.Windows.MessageBox;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using MediaColor = System.Windows.Media.Color;

namespace Lightspeed_wpf
{
    public partial class SettingsWindow : Window
    {
        private readonly MainWindow _main;
        private bool _loading;
        private bool _iconSizeDirty;
        private bool _capturingHotkey;
        private bool _capturingSearchHotkey;

        public SettingsWindow(MainWindow main)
        {
            _main = main;
            InitializeComponent();
            Loaded += (_, _) => Populate();
            Closed += (_, _) =>
            {
                if (_iconSizeDirty)
                    _main.ApplyIconSizeChangesFromSettings();
                _main.OnSettingsWindowClosed();
            };
        }

        public void Populate()
        {
            _loading = true;

            int mode = _main.GetQuickLaunchMode();
            RbQuickLaunchOff.IsChecked = mode == 0;
            RbQuickLaunchAHK.IsChecked = mode == 1;
            RbQuickLaunchNative.IsChecked = mode == 2;
            UpdateQuickLaunchPanels(mode);

            ChkAutoStartWithWindows.IsChecked = AppSettings.Instance.AutoStartWithWindows;
            ChkTouchMode.IsChecked = AppSettings.Instance.TouchMode;
            ChkHideDesktopIni.IsChecked = AppSettings.Instance.HideDesktopIni;
            ChkHideExtensions.IsChecked = AppSettings.Instance.HideExtensions;
            ChkSingleClickOpen.IsChecked = AppSettings.Instance.SingleClickOpen;
            ChkDisableInFullscreen.IsChecked = AppSettings.Instance.DisableHotkeyInFullscreen;

            if (AppSettings.Instance.TrayClickAction == 1)
                RbTraySearch.IsChecked = true;
            else
                RbTrayMain.IsChecked = true;

            ApplyToastPositionRadio(AppSettings.Instance.ToastPosition);

            int listSize = AppSettings.Instance.ListIconSize;
            int iconSize = AppSettings.Instance.IconIconSize;
            SliderIconSize.Value = listSize;
            SliderIconSizeIcon.Value = iconSize;
            TxtIconSize.Text = listSize.ToString();
            TxtIconSizeIcon.Text = iconSize.ToString();

            double listFont = AppSettings.Instance.ListLabelFontSize > 0 ? AppSettings.Instance.ListLabelFontSize : 14;
            double gridFont = AppSettings.Instance.GridLabelFontSize > 0 ? AppSettings.Instance.GridLabelFontSize : 13;
            SliderListFontSize.Value = listFont;
            SliderGridFontSize.Value = gridFont;
            TxtListFontSize.Text = ((int)Math.Round(listFont)).ToString();
            TxtGridFontSize.Text = ((int)Math.Round(gridFont)).ToString();

            int windowMode = AppSettings.Instance.WindowSizeMode;
            if (windowMode == 0) RbWindowMode0.IsChecked = true;
            else if (windowMode == 1) RbWindowMode1.IsChecked = true;
            else RbWindowMode2.IsChecked = true;
            PanelCustomSize.Visibility = windowMode == 2 ? Visibility.Visible : Visibility.Collapsed;
            TxtCustomWidth.Text = AppSettings.Instance.CustomWindowWidth.ToString();
            TxtCustomHeight.Text = AppSettings.Instance.CustomWindowHeight.ToString();

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

            TxtHotkey.Text = _main.GetHotkeyDisplayText();
            TxtSearchHotkey.Text = _main.GetSearchHotkeyDisplayText();
            TxtGamepadHotkey.Text = _main.GetGamepadHotkeyDisplayText();

            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            TxtVersion.Text = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "-";
            TxtUpdateStatus.Text = "";

            _iconSizeDirty = false;
            _loading = false;
        }

        public void SetHotkeyText(string text) => TxtHotkey.Text = text;
        public void SetSearchHotkeyText(string text) => TxtSearchHotkey.Text = text;
        public void SetGamepadHotkeyText(string text) => TxtGamepadHotkey.Text = text;

        public void SyncIconSizeControls(int listSize, int iconSize)
        {
            if (!IsLoaded) return;
            _loading = true;
            SliderIconSize.Value = listSize;
            SliderIconSizeIcon.Value = iconSize;
            TxtIconSize.Text = listSize.ToString();
            TxtIconSizeIcon.Text = iconSize.ToString();
            _loading = false;
        }

        private void UpdateQuickLaunchPanels(int mode)
        {
            PanelAhkControls.Visibility = mode == 1 ? Visibility.Visible : Visibility.Collapsed;
            PanelNativeControls.Visibility = mode == 2 ? Visibility.Visible : Visibility.Collapsed;
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

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try { DragMove(); } catch { }
            }
        }

        private void BtnCloseSettings_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_PreviewKeyDown(object sender, WpfKeyEventArgs e)
        {
            if (_capturingHotkey)
            {
                _main.CaptureHotkeyFromSettings(e, status => TxtHotkey.Text = status, () =>
                {
                    _capturingHotkey = false;
                    TxtHotkey.Text = _main.GetHotkeyDisplayText();
                });
                e.Handled = true;
                return;
            }

            if (_capturingSearchHotkey)
            {
                _main.CaptureSearchHotkeyFromSettings(e, status => TxtSearchHotkey.Text = status, () =>
                {
                    _capturingSearchHotkey = false;
                    TxtSearchHotkey.Text = _main.GetSearchHotkeyDisplayText();
                });
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.R && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                _main.RefreshCurrentFolder();
                e.Handled = true;
            }
        }

        private void ChkAutoStartWithWindows_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _main.SetAutoStartWithWindows(ChkAutoStartWithWindows.IsChecked ?? false);
        }

        private void ChkTouchMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            AppSettings.Instance.TouchMode = ChkTouchMode.IsChecked ?? false;
            AppSettings.Instance.Save();
            _main.ApplyTouchMode();
        }

        private void RbTrayClick_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if (sender is WpfRadioButton rb && rb.IsChecked == true && rb.Tag != null)
            {
                AppSettings.Instance.TrayClickAction = int.Parse(rb.Tag.ToString()!);
                AppSettings.Instance.Save();
            }
        }

        private void BtnCaptureHotkey_Click(object sender, RoutedEventArgs e)
        {
            _capturingHotkey = true;
            _capturingSearchHotkey = false;
            TxtHotkey.Text = "请按任意键...";
            Activate();
            Focus();
        }

        private void BtnCaptureSearchHotkey_Click(object sender, RoutedEventArgs e)
        {
            _capturingSearchHotkey = true;
            _capturingHotkey = false;
            TxtSearchHotkey.Text = "请按任意键...";
            Activate();
            Focus();
        }

        private void ChkDisableInFullscreen_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            AppSettings.Instance.DisableHotkeyInFullscreen = ChkDisableInFullscreen.IsChecked ?? true;
            AppSettings.Instance.Save();
        }

        private void BtnCaptureGamepadHotkey_Click(object sender, RoutedEventArgs e)
        {
            TxtGamepadHotkey.Text = "按下组合键 (至少3键)...";
            _main.BeginCaptureGamepadHotkey();
        }

        private void BtnClearGamepadHotkey_Click(object sender, RoutedEventArgs e)
        {
            _main.ClearGamepadHotkey();
        }

        private void RbQuickLaunchMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if (sender is not WpfRadioButton rb || rb.IsChecked != true) return;
            if (!int.TryParse(rb.Tag?.ToString(), out int mode)) return;
            UpdateQuickLaunchPanels(mode);
            _main.SetQuickLaunchMode(mode);
        }

        private void BtnCreateFolders_Click(object sender, RoutedEventArgs e) => _main.CreateLightspeedFolders();
        private void BtnGenerateAHK_Click(object sender, RoutedEventArgs e) => _main.GenerateAHKScript();
        private void BtnStartAHK_Click(object sender, RoutedEventArgs e) => _main.StartAHKScript();
        private void BtnGenerateAndStart_Click(object sender, RoutedEventArgs e)
        {
            _main.GenerateAHKScript();
            _main.StartAHKScript();
        }

        private void BtnReloadNativeBindings_Click(object sender, RoutedEventArgs e) => _main.ReloadNativeBindings();

        private void ChkHideDesktopIni_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            AppSettings.Instance.HideDesktopIni = ChkHideDesktopIni.IsChecked ?? false;
            AppSettings.Instance.Save();
            _main.RefreshCurrentFolder();
        }

        private void ChkHideExtensions_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            AppSettings.Instance.HideExtensions = ChkHideExtensions.IsChecked ?? false;
            AppSettings.Instance.Save();
            _main.RefreshCurrentFolder();
        }

        private void ChkSingleClickOpen_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            AppSettings.Instance.SingleClickOpen = ChkSingleClickOpen.IsChecked ?? false;
            AppSettings.Instance.Save();
        }

        private void RbToastPosition_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if (sender is not WpfRadioButton rb || rb.IsChecked != true) return;
            if (!int.TryParse(rb.Tag?.ToString(), out int pos)) return;
            AppSettings.Instance.ToastPosition = pos;
            AppSettings.Instance.Save();
            _main.ApplyToastHostPosition();
            _main.ShowToast("提示位置已更新");
        }

        private void SliderIconSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading || TxtIconSize == null) return;
            TxtIconSize.Text = ((int)SliderIconSize.Value).ToString();
            _iconSizeDirty = true;
            _main.PreviewIconSize((int)SliderIconSize.Value, (int)SliderIconSizeIcon.Value);
        }

        private void SliderIconSizeIcon_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading || TxtIconSizeIcon == null) return;
            TxtIconSizeIcon.Text = ((int)SliderIconSizeIcon.Value).ToString();
            _iconSizeDirty = true;
            _main.PreviewIconSize((int)SliderIconSize.Value, (int)SliderIconSizeIcon.Value);
        }

        private void TxtIconSize_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtIconSize.Text, out int val))
            {
                val = Math.Clamp(val, (int)SliderIconSize.Minimum, (int)SliderIconSize.Maximum);
                SliderIconSize.Value = val;
                _iconSizeDirty = true;
            }
            else
            {
                TxtIconSize.Text = ((int)SliderIconSize.Value).ToString();
            }
        }

        private void TxtIconSizeIcon_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtIconSizeIcon.Text, out int val))
            {
                val = Math.Clamp(val, (int)SliderIconSizeIcon.Minimum, (int)SliderIconSizeIcon.Maximum);
                SliderIconSizeIcon.Value = val;
                _iconSizeDirty = true;
            }
            else
            {
                TxtIconSizeIcon.Text = ((int)SliderIconSizeIcon.Value).ToString();
            }
        }

        private void TxtIconSize_KeyDown(object sender, WpfKeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TxtIconSize_LostFocus(sender, e);
                e.Handled = true;
            }
        }

        private void TxtIconSizeIcon_KeyDown(object sender, WpfKeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TxtIconSizeIcon_LostFocus(sender, e);
                e.Handled = true;
            }
        }

        private void ApplyFontSizeFromUi()
        {
            if (_loading) return;
            _main.PreviewLabelFontSize(SliderListFontSize.Value, SliderGridFontSize.Value);
        }

        private void SliderListFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading || TxtListFontSize == null) return;
            TxtListFontSize.Text = ((int)Math.Round(SliderListFontSize.Value)).ToString();
            ApplyFontSizeFromUi();
        }

        private void SliderGridFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading || TxtGridFontSize == null) return;
            TxtGridFontSize.Text = ((int)Math.Round(SliderGridFontSize.Value)).ToString();
            ApplyFontSizeFromUi();
        }

        private void TxtListFontSize_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtListFontSize.Text, out int val))
            {
                val = Math.Clamp(val, (int)SliderListFontSize.Minimum, (int)SliderListFontSize.Maximum);
                SliderListFontSize.Value = val;
            }
            else
            {
                TxtListFontSize.Text = ((int)Math.Round(SliderListFontSize.Value)).ToString();
            }
        }

        private void TxtGridFontSize_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtGridFontSize.Text, out int val))
            {
                val = Math.Clamp(val, (int)SliderGridFontSize.Minimum, (int)SliderGridFontSize.Maximum);
                SliderGridFontSize.Value = val;
            }
            else
            {
                TxtGridFontSize.Text = ((int)Math.Round(SliderGridFontSize.Value)).ToString();
            }
        }

        private void TxtListFontSize_KeyDown(object sender, WpfKeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TxtListFontSize_LostFocus(sender, e);
                e.Handled = true;
            }
        }

        private void TxtGridFontSize_KeyDown(object sender, WpfKeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TxtGridFontSize_LostFocus(sender, e);
                e.Handled = true;
            }
        }

        private void RbWindowMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if (sender is WpfRadioButton rb && rb.IsChecked == true && rb.Tag != null)
            {
                int mode = int.Parse(rb.Tag.ToString()!);
                AppSettings.Instance.WindowSizeMode = mode;
                AppSettings.Instance.Save();
                PanelCustomSize.Visibility = mode == 2 ? Visibility.Visible : Visibility.Collapsed;
                _main.ApplyWindowSize();
            }
        }

        private void TxtCustomSize_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtCustomWidth.Text, out int w) && w > 0)
                AppSettings.Instance.CustomWindowWidth = w;
            if (int.TryParse(TxtCustomHeight.Text, out int h) && h > 0)
                AppSettings.Instance.CustomWindowHeight = h;
            AppSettings.Instance.Save();
            _main.ApplyWindowSize();
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
                _main.UpdateFolderNameDisplay();
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });
            e.Handled = true;
        }

        private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn) btn.IsEnabled = false;
            TxtUpdateStatus.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x88, 0x88, 0x88));
            TxtUpdateStatus.Text = "检查中...";

            try
            {
                var (ok, message, isNewer, tag) = await _main.CheckForUpdateAsync();
                if (!ok)
                {
                    TxtUpdateStatus.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xE8, 0x11, 0x23));
                    TxtUpdateStatus.Text = message;
                    return;
                }

                if (isNewer)
                {
                    TxtUpdateStatus.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x6B, 0x9F, 0xFF));
                    TxtUpdateStatus.Text = $"发现新版本 {tag}";
                    var result = WpfMessageBox.Show(
                        $"{message}\n是否打开下载页面？",
                        "检查更新",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);
                    if (result == MessageBoxResult.Yes)
                        _main.OpenReleasesPage();
                }
                else
                {
                    TxtUpdateStatus.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x4C, 0xAF, 0x50));
                    TxtUpdateStatus.Text = message;
                }
            }
            catch (Exception ex)
            {
                TxtUpdateStatus.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xE8, 0x11, 0x23));
                TxtUpdateStatus.Text = "检查失败: " + ex.Message;
            }
            finally
            {
                if (sender is WpfButton btn2) btn2.IsEnabled = true;
            }
        }
    }
}
