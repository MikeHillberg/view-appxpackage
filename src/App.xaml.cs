using Microsoft.UI.Xaml;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ViewAppxPackage
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        // Test/debug flag
        internal static bool LazyPreload = false;

        // In MCP mode there's no Window, and we watch console input for MCP requests
        internal static bool IsMcpServerMode = false;

        // Some filter came in on the command-lline
        internal static bool CommandLineFilterProvided = false;

        /// <summary>
        /// Runs the application in MCP server mode
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Application exit code</returns>
        private static void StartMcpServer()
        {
            try
            {
                var builder = Host.CreateApplicationBuilder();

                //builder.Services.AddSingleton<App>();

                // Register MCP server services
                builder.Services
                    .AddMcpServer()
                    .WithStdioServerTransport()
                    .WithToolsFromAssembly();

                // Run async so it doesn't block and prevent the call to App.Start()
                var host = builder.Build();
                host.RunAsync();
            }
            catch (Exception ex)
            {
                DebugLog.Append(ex, $"Couldn't start MCP server");
                Console.WriteLine($"Error starting MCP server: {ex.Message}");
            }
        }

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
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args2)
        {
            ProcessCommandLine();

            // Two threads, a UI thread and a worker thread
            // Everything we do with actual Package APIs is on the single worker thread
            MyThreading.SetCurrentAsUIThread();
            _ = MyThreading.CreateWorkerThreadAsync();

            if (!IsMcpServerMode)
            {
                // In MCP mode, MCP owns standard input.
                // Otherwise, output could have been piped in, which will come in on stdin
                ProcessPipeInput();
            }

            // Queue to the worker thread to initialize the Catalog Model
            var catalogModel = PackageCatalogModel.Instance;
            _ = MyThreading.RunOnWorkerAsync( () =>
            {
                catalogModel.Initialize(
                    isAllUsers: false,
                    preloadFullName: catalogModel.Filter == PipeInputFilterString,
                    useSettings: true);
            });

            if (IsMcpServerMode)
            {
                // If running as an MCP server, don't create a window, 
                // but run the MCP server (listening on the console input for requests)
                // Don't start the MCP server though until the catalog has basic initialization done
                catalogModel.MinimallyLoaded += (s, e) =>
                {
                    StartMcpServer();
                };
            }

            else
            {
                // Create the window
                m_window = new MainWindow();
                m_window.Activate();
            }
        }

        /// <summary>
        /// Process pipe input (the output of get-appxpackage)
        /// </summary>
        private static void ProcessPipeInput()
        {
            var catalogModel = PackageCatalogModel.Instance;

            // Read pipe input from the console,
            // which will have content if this was launched from a PowerShell pipe.
            // e.g. get-appxpackage *foo* | view-appxpackage
            //
            // The pipe input comes in just like the output of get-appxpackage, so we're looking for e.g.
            //
            //   PackageFamilyName : view-appxpackage_9exbdrchsqpwm

            using (StreamReader reader = new(Console.OpenStandardInput()))
            {
                List<string> names = new();
                string line;
                var found = false;
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split(':');
                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    if (parts[0].Trim() == "PackageFullName")
                    {
                        found = true;
                        names.Add(parts[1].Trim());
                    }
                }

                if (found)
                {
                    catalogModel.PipeInputPackages = names.ToArray();

                    // Set the filter box to "$input" indicating we should use this list of names
                    catalogModel.Filter = PipeInputFilterString;
                }
            }
        }

        private static void ProcessCommandLine()
        {
            var args = Environment.GetCommandLineArgs();

            if (args != null && args.Length > 1)
            {
                // For some reason when launching from vscode, the args aren't getting split up
                args = args[2..].Union(args[1].Split(' ')).ToArray();

                for (int i = 0; i < args.Length; i++)
                {
                    var arg = args[i].ToLower().Trim();
                    if (arg == "-mcp")
                    {
                        IsMcpServerMode = true;
                        continue;
                    }

                    else if (arg == "-lazy")
                    {
                        LazyPreload = true;
                        continue;
                    }

                    // Use first non-flag argument as filter
                    CommandLineFilterProvided = true;
                    PackageCatalogModel.Instance.Filter = arg;

                }
            }
        }

        // If we got input on the command line (piped from get-appxpackage),
        // set the filter to this magic value saying to use that
        static readonly internal string PipeInputFilterString = "$input";

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
