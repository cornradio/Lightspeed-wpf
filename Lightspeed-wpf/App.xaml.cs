using System;
using System.Text;
using System.Threading;

namespace Lightspeed_wpf
{
    public partial class App : System.Windows.Application
    {
        static App()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            bool createdNew;
            Mutex mutex = new Mutex(true, "Lightspeed_wpf_SingleInstance_Mutex", out createdNew);

            if (!createdNew)
            {
                System.Windows.MessageBox.Show("Lightspeed 已在运行中！", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                mutex.Dispose();
                Environment.Exit(0);
                return;
            }

            base.OnStartup(e);
        }
    }
}