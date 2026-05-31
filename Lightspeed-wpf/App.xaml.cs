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

        private const string MutexName = "Lightspeed_wpf_SingleInstance_Mutex";
        private const string ShowEventName = "Lightspeed_wpf_SingleInstance_ShowEvent";

        private static Mutex? _singleInstanceMutex;
        private static EventWaitHandle? _showEvent;
        private static RegisteredWaitHandle? _showWaitHandle;

        public static event Action? SecondInstanceLaunched;

        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                try
                {
                    if (EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
                    {
                        existing.Set();
                        existing.Dispose();
                    }
                }
                catch { }
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                Shutdown(0);
                return;
            }

            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            _showWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                _showEvent,
                (_, _) => Dispatcher.BeginInvoke(new Action(() => SecondInstanceLaunched?.Invoke())),
                null,
                Timeout.Infinite,
                executeOnlyOnce: false);

            base.OnStartup(e);
        }
    }
}
