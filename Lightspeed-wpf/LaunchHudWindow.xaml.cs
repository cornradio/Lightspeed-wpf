using System.Windows;
using System.Windows.Media;

namespace Lightspeed_wpf
{
    public partial class LaunchHudWindow : Window
    {
        private static LaunchHudWindow? _instance;
        private CancellationTokenSource? _cts;

        public LaunchHudWindow()
        {
            InitializeComponent();
        }

        public static void ShowLaunch(string title, ImageSource? icon, int durationMs = 800)
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            void Show()
            {
                if (_instance == null)
                {
                    _instance = new LaunchHudWindow();
                }

                var hud = _instance;
                hud.HudTitle.Text = title;
                hud.HudIcon.Source = icon;
                hud.HudIcon.Visibility = icon != null ? Visibility.Visible : Visibility.Collapsed;

                double screenW = SystemParameters.PrimaryScreenWidth;
                hud.Left = (screenW - hud.Width) / 2;
                hud.Top = SystemParameters.WorkArea.Top + 36;

                if (!hud.IsVisible)
                    hud.Show();

                hud.PlayAsync(durationMs);
            }

            if (app.Dispatcher.CheckAccess())
                Show();
            else
                app.Dispatcher.BeginInvoke(Show);
        }

        private async void PlayAsync(int durationMs)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            HudRoot.Opacity = 0;
            try
            {
                for (int i = 1; i <= 8; i++)
                {
                    if (token.IsCancellationRequested) return;
                    HudRoot.Opacity = i / 8.0;
                    await Task.Delay(16, token);
                }
                HudRoot.Opacity = 1;
                await Task.Delay(durationMs, token);
                for (int i = 8; i >= 0; i--)
                {
                    if (token.IsCancellationRequested) return;
                    HudRoot.Opacity = i / 8.0;
                    await Task.Delay(20, token);
                }
                if (!token.IsCancellationRequested)
                    Hide();
            }
            catch (TaskCanceledException) { }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_instance == this) _instance = null;
            base.OnClosed(e);
        }
    }
}
