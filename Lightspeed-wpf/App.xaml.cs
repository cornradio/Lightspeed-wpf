using System.Text;

namespace Lightspeed_wpf
{
    public partial class App : System.Windows.Application
    {
        static App()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
    }
}
