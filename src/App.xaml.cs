using Microsoft.UI.Xaml;
using ModelContextProtocol;
using ModelContextProtocol.Handlers;
using ModelContextProtocol.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading.Tasks;
using Windows.Management.Deployment;

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
            // Check for MCP command-line arguments before initializing UI
            var cmdArgs = Environment.GetCommandLineArgs();
            if (cmdArgs != null && cmdArgs.Length > 1)
            {
                if (cmdArgs[1].ToLower() == "--mcp-list-packages")
                {
                    OutputPackagesAsJson();
                    Environment.Exit(0);
                    return;
                }
            }

            // Normal application flow
            m_window = new MainWindow();
            m_window.Activate();
        }

        private void OutputPackagesAsJson()
        {
            // Use the same package loading logic as the main app
            var packageManager = new PackageManager();
            var packages = packageManager.FindPackagesForUser(string.Empty);
            
            // Create a list of package data for MCP serialization
            var packageItems = new List<Dictionary<string, object>>();
            
            foreach (var package in packages)
            {
                if (!package.IsResourcePackage)
                {
                    // Create a dictionary to hold package properties
                    var packageData = new Dictionary<string, object>
                    {
                        { "name", package.Id.Name },
                        { "fullName", package.Id.FullName },
                        { "familyName", package.Id.FamilyName },
                        { "publisher", package.Id.Publisher },
                        { "publisherDisplayName", package.PublisherDisplayName },
                        { "installedDate", package.InstalledDate },
                        { "architecture", package.Id.Architecture.ToString() },
                        { "version", $"{package.Id.Version.Major}.{package.Id.Version.Minor}.{package.Id.Version.Build}.{package.Id.Version.Revision}" },
                        { "isFramework", package.IsFramework },
                        { "isResourcePackage", package.IsResourcePackage },
                        { "isBundle", package.IsBundle },
                        { "isDevelopmentMode", package.IsDevelopmentMode },
                        { "installedPath", package.InstalledPath }
                    };
                    
                    packageItems.Add(packageData);
                }
            }
            
            // Create an MCP context with the package data
            var modelContext = new ModelContext
            {
                Items = packageItems
            };
            
            // Serialize using MCP serializer
            var jsonHandler = new JsonModelContextHandler();
            var json = jsonHandler.GetFormattedResponse(modelContext);
            
            // Output the JSON
            Console.WriteLine(json);
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
