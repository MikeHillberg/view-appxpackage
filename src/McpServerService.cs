using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Attributes;
using ModelContextProtocol.Server;

namespace ViewAppxPackage
{
    /// <summary>
    /// MCP (Model Context Protocol) Server service that provides tools to interact with AppX package data.
    /// This service exposes package information through MCP tools, allowing external systems to query
    /// package family names and other package information.
    /// 
    /// Usage:
    /// To start the application in MCP server mode, use the command line argument:
    /// view-appxpackage.exe -mcpserver
    /// 
    /// In this mode, the application will:
    /// 1. Load all package data without showing the UI
    /// 2. Start an MCP server that exposes tools for querying package information
    /// 3. Wait for package loading to complete before serving requests
    /// 4. Provide the 'list_package_family_names' tool to retrieve all package family names
    /// 
    /// The server will continue running until terminated and can serve multiple MCP tool requests.
    /// </summary>
    public class McpServerService
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ManualResetEventSlim _packageLoadedEvent;
        private bool _isPackageDataReady = false;
        private McpServer _server;

        /// <summary>
        /// Initializes a new instance of the McpServerService.
        /// </summary>
        public McpServerService()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _packageLoadedEvent = new ManualResetEventSlim(false);
        }

        /// <summary>
        /// Starts the MCP server and waits for package data to be loaded.
        /// This method should be called after the application has started loading packages.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the server operation</param>
        /// <returns>A task representing the server operation</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Wait for the main window to finish loading packages
                await WaitForPackageDataAsync(cancellationToken);

                // Create and start the MCP server
                _server = new McpServer("view-appxpackage-mcp", "1.0.0");
                
                // Register this service instance to handle tool calls
                _server.AddToolProvider(this);

                // Start the server - this will block until cancellation is requested
                await _server.RunAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                // Log any unexpected errors
                DebugLog.Append($"MCP Server error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Stops the MCP server gracefully.
        /// </summary>
        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _server?.Dispose();
            _packageLoadedEvent.Dispose();
        }

        /// <summary>
        /// Waits for the package data to be loaded and ready for serving.
        /// This ensures we don't serve stale or incomplete data.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the wait operation</param>
        /// <returns>A task that completes when package data is ready</returns>
        private async Task WaitForPackageDataAsync(CancellationToken cancellationToken)
        {
            // Check if MainWindow instance exists and IsLoading is false
            while (MainWindow.Instance?.IsLoading != false)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                // Wait a short period before checking again
                await Task.Delay(100, cancellationToken);
            }

            // Additional safety check - ensure packages are actually loaded
            while (PackageCatalogModel.Instance?.Packages == null || 
                   PackageCatalogModel.Instance.Packages.Count == 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                await Task.Delay(100, cancellationToken);
            }

            _isPackageDataReady = true;
            _packageLoadedEvent.Set();
        }

        /// <summary>
        /// MCP Tool: Lists all Package Family Names from the loaded packages.
        /// This tool provides access to the package family names of all installed packages
        /// that have been loaded by the application.
        /// </summary>
        /// <returns>A list of package family names</returns>
        [McpTool("list_package_family_names", "Lists all Package Family Names from loaded AppX packages")]
        public IEnumerable<string> ListPackageFamilyNames()
        {
            try
            {
                // Ensure package data is ready before serving
                if (!_isPackageDataReady)
                {
                    // If not ready, wait for package loading to complete
                    _packageLoadedEvent.Wait();
                }

                // Get the packages from the catalog model
                var packages = PackageCatalogModel.Instance?.Packages;
                if (packages == null)
                {
                    DebugLog.Append("Warning: Package catalog is null when listing family names");
                    return Enumerable.Empty<string>();
                }

                // Extract family names from all packages
                var familyNames = packages
                    .Where(package => !string.IsNullOrEmpty(package.FamilyName))
                    .Select(package => package.FamilyName)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList();

                DebugLog.Append($"MCP Tool: Returned {familyNames.Count} package family names");
                return familyNames;
            }
            catch (Exception ex)
            {
                DebugLog.Append($"Error in ListPackageFamilyNames: {ex.Message}");
                return Enumerable.Empty<string>();
            }
        }
    }
}