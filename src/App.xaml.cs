using Microsoft.UI.Xaml;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System;

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
        /// The main entry point for the application
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Application exit code</returns>
        [STAThread]
        public static int Main(string[] args)
        {
            // Check if we're running in MCP server mode
            bool isMcpServer = false;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].ToLower() == "-mcpserver")
                    {
                        isMcpServer = true;
                        break;
                    }
                }
            }

            if (isMcpServer)
            {
                // Run in MCP server mode with dependency injection
                return RunMcpServerMode(args);
            }
            else
            {
                // Run in normal UI mode
                WinRT.ComWrappersSupport.InitializeComWrappers();
                global::Microsoft.UI.Xaml.Application.Start((p) => {
                    var context = new global::Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                        global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                    global::System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                    new App();
                });
                return 0;
            }
        }

        /// <summary>
        /// Runs the application in MCP server mode using dependency injection
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Application exit code</returns>
        private static int RunMcpServerMode(string[] args)
        {
            try
            {
                var builder = Host.CreateApplicationBuilder(args);
                
                // Register MCP server services
                builder.Services.AddScoped<McpServerService>();
                // TODO: Use WithToolsFromAssembly method here once we determine the correct syntax
                
                var host = builder.Build();
                
                // Start the UI thread for package loading (still needed to load package data)
                WinRT.ComWrappersSupport.InitializeComWrappers();
                global::Microsoft.UI.Xaml.Application.Start((p) => {
                    var context = new global::Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                        global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                    global::System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                    new App();
                });
                
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting MCP server: {ex.Message}");
                return 1;
            }
        }
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
