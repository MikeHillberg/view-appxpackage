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

namespace ViewAppxPackage;

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
    /// Invoked when the application is activated via protocol (App Actions).
    /// </summary>
    /// <param name="args">Details about the protocol activation.</param>
    protected override void OnActivated(Microsoft.UI.Xaml.ActivatedEventArgs args)
    {
        if (args.Kind == Microsoft.UI.Xaml.ActivationKind.Protocol)
        {
            var protocolArgs = args as Microsoft.UI.Xaml.ProtocolActivatedEventArgs;
            if (protocolArgs != null)
            {
                HandleAppActionProtocol(protocolArgs.Uri);
                return;
            }
        }

        // Fall back to default activation handling
        base.OnActivated(args);
    }

    /// <summary>
    /// Handles App Action protocol activations by parsing the URI and executing the appropriate action.
    /// App Actions mirror the functionality of MCP tools, making them discoverable via Windows Search.
    /// </summary>
    /// <param name="uri">The protocol URI that activated the app</param>
    private async void HandleAppActionProtocol(Uri uri)
    {
        // Initialize threading model for App Actions (same as MCP mode)
        MyThreading.SetCurrentAsUIThread();
        _ = MyThreading.EnsureWorkerThreadAsync();

        // Initialize the catalog model for App Actions
        var catalogModel = PackageCatalogModel.Instance;
        await MyThreading.RunOnWorkerAsync(() =>
        {
            catalogModel.Initialize(
                isAllUsers: false,
                preloadFullName: false,
                useSettings: true);
        });

        // Wait for packages to load before processing App Action
        while (!catalogModel.IsLoaded)
        {
            await Task.Delay(100);
        }

        // Create MCP server instance to reuse existing tool logic
        var mcpServer = new McpServer();
        string result = "";

        try
        {
            // Parse protocol URI and execute corresponding App Action
            switch (uri.Scheme.ToLower())
            {
                case "view-appxpackage-list":
                    // App Action: List Package Family Names
                    var familyNames = mcpServer.ListPackageFamilyNames();
                    result = string.Join("\n", familyNames);
                    break;

                case "view-appxpackage-properties":
                    // App Action: Get Package Properties
                    // Extract package family name from query parameters
                    var packageFamilyName = GetQueryParameter(uri, "packageFamilyName");
                    if (!string.IsNullOrEmpty(packageFamilyName))
                    {
                        result = mcpServer.GetPackageProperties(packageFamilyName);
                    }
                    else
                    {
                        result = "Error: packageFamilyName parameter is required";
                    }
                    break;

                case "view-appxpackage-find":
                    // App Action: Find Packages Containing Property
                    // Extract property name and value from query parameters
                    var propertyName = GetQueryParameter(uri, "propertyName");
                    var propertyValue = GetQueryParameter(uri, "propertyValue");
                    if (!string.IsNullOrEmpty(propertyName) && !string.IsNullOrEmpty(propertyValue))
                    {
                        result = mcpServer.FindPackagesContainingProperty(propertyName, propertyValue);
                    }
                    else
                    {
                        result = "Error: propertyName and propertyValue parameters are required";
                    }
                    break;

                default:
                    result = $"Unknown App Action protocol: {uri.Scheme}";
                    break;
            }

            // Output result to console for command-line usage
            Console.WriteLine(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing App Action: {ex.Message}");
            DebugLog.Append(ex, "App Action execution error");
        }

        // Exit after processing App Action (no UI needed)
        Environment.Exit(0);
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
        _ = MyThreading.EnsureWorkerThreadAsync();

        if (!IsMcpServerMode)
        {
            // In MCP mode, MCP owns standard input.
            // Otherwise, output could have been piped in, which will come in on stdin
            ProcessPipeInput();
        }

        // Queue to the worker thread to initialize the Catalog Model
        var catalogModel = PackageCatalogModel.Instance;
        _ = MyThreading.RunOnWorkerAsync(() =>
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
        else if (IsAppActionMode)
        {
            // If running in App Action mode, execute the command line App Action
            catalogModel.MinimallyLoaded += (s, e) =>
            {
                ExecuteAppActionFromCommandLine();
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
    /// Executes App Actions invoked from command line (alternative to protocol activation).
    /// This allows App Actions to be used from command line: view-appxpackage.exe -action list
    /// </summary>
    private static async void ExecuteAppActionFromCommandLine()
    {
        try
        {
            // Create MCP server instance to reuse existing tool logic
            var mcpServer = new McpServer();
            string result = "";

            switch (AppActionType)
            {
                case "list":
                    // Command line App Action: List Package Family Names
                    // Usage: view-appxpackage.exe -action list
                    var familyNames = mcpServer.ListPackageFamilyNames();
                    result = string.Join("\n", familyNames);
                    break;

                case "properties":
                    // Command line App Action: Get Package Properties
                    // Usage: view-appxpackage.exe -action properties -param:packageFamilyName=SomePackage_123abc
                    if (AppActionParameters.TryGetValue("packageFamilyName", out var packageFamilyName))
                    {
                        result = mcpServer.GetPackageProperties(packageFamilyName);
                    }
                    else
                    {
                        result = "Error: packageFamilyName parameter is required. Use -param:packageFamilyName=YourPackageFamilyName";
                    }
                    break;

                case "find":
                    // Command line App Action: Find Packages Containing Property
                    // Usage: view-appxpackage.exe -action find -param:propertyName=Name -param:propertyValue=Calculator
                    if (AppActionParameters.TryGetValue("propertyName", out var propertyName) &&
                        AppActionParameters.TryGetValue("propertyValue", out var propertyValue))
                    {
                        result = mcpServer.FindPackagesContainingProperty(propertyName, propertyValue);
                    }
                    else
                    {
                        result = "Error: propertyName and propertyValue parameters are required. Use -param:propertyName=Name -param:propertyValue=Calculator";
                    }
                    break;

                default:
                    result = $"Unknown App Action: {AppActionType}\nAvailable actions: list, properties, find";
                    break;
            }

            // Output result to console
            Console.WriteLine(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing App Action: {ex.Message}");
            DebugLog.Append(ex, "Command line App Action execution error");
        }

        // Exit after processing command line App Action
        Environment.Exit(0);
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

    // App Action mode flags - allow command line invocation of App Actions
    internal static bool IsAppActionMode = false;
    internal static string AppActionType = "";
    internal static Dictionary<string, string> AppActionParameters = new();

    private static void ProcessCommandLine()
    {
        var args = Environment.GetCommandLineArgs();

        if (args != null && args.Length > 0)
        {
            // Check if launched via App Action alias commands
            var executableName = System.IO.Path.GetFileNameWithoutExtension(args[0]).ToLower();
            if (executableName == "view-appx-list")
            {
                IsAppActionMode = true;
                AppActionType = "list";
            }
            else if (executableName == "view-appx-properties")
            {
                IsAppActionMode = true;
                AppActionType = "properties";
                // First argument should be package family name
                if (args.Length > 1)
                {
                    AppActionParameters["packageFamilyName"] = args[1];
                }
            }
            else if (executableName == "view-appx-find")
            {
                IsAppActionMode = true;
                AppActionType = "find";
                // First two arguments should be property name and value
                if (args.Length > 2)
                {
                    AppActionParameters["propertyName"] = args[1];
                    AppActionParameters["propertyValue"] = args[2];
                }
            }
        }

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

                // App Action command line support - allows direct invocation without protocol
                else if (arg == "-action")
                {
                    IsAppActionMode = true;
                    if (i + 1 < args.Length)
                    {
                        AppActionType = args[++i].ToLower().Trim();
                    }
                    continue;
                }

                else if (arg.StartsWith("-param:"))
                {
                    // Parse parameter: -param:key=value
                    var paramPart = arg.Substring(7); // Remove "-param:"
                    var equalIndex = paramPart.IndexOf('=');
                    if (equalIndex > 0)
                    {
                        var key = paramPart.Substring(0, equalIndex);
                        var value = paramPart.Substring(equalIndex + 1);
                        AppActionParameters[key] = value;
                    }
                    continue;
                }

                // Use first non-flag argument as filter (existing behavior)
                if (!IsAppActionMode)
                {
                    CommandLineFilterProvided = true;
                    PackageCatalogModel.Instance.Filter = arg;
                }
            }
        }
    }

    /// <summary>
    /// Helper method to extract query parameters from URI without System.Web dependency.
    /// </summary>
    /// <param name="uri">The URI to parse</param>
    /// <param name="parameterName">The parameter name to extract</param>
    /// <returns>The parameter value, or null if not found</returns>
    private static string GetQueryParameter(Uri uri, string parameterName)
    {
        if (string.IsNullOrEmpty(uri.Query))
            return null;

        var query = uri.Query.TrimStart('?');
        var parameters = query.Split('&');
        
        foreach (var parameter in parameters)
        {
            var keyValue = parameter.Split('=');
            if (keyValue.Length == 2 && keyValue[0].Equals(parameterName, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(keyValue[1]);
            }
        }
        
        return null;
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
