using Microsoft.UI.Xaml;
using System.Diagnostics;
using System.Security.Principal;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ViewAppxPackage
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        internal static void WaitForDebugger()
        {
            while (!Debugger.IsAttached)
            {
                System.Threading.Thread.Sleep(100);
            }
        }


        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
        }

        private Window m_window;

        static bool? _isProcessElevated = null;
        public static bool IsProcessElevated()
        {
            if (_isProcessElevated == null)
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                _isProcessElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            return _isProcessElevated.Value;
        }
    }
}
