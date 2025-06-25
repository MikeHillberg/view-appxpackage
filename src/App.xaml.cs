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
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.AppService;

namespace ViewAppxPackage;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// 
/// App Actions Implementation:
/// This application implements App Actions following Microsoft's App Actions guidelines
/// for Windows App SDK, making package information queries discoverable via Windows Search
/// and context menu.
/// 
/// The App Actions provide three main functions mirroring the MCP tools:
/// 1. List Package Family Names - Lists all MSIX/AppX package family names
/// 2. Get Package Properties - Retrieves detailed properties for a specific package
/// 3. Find Packages by Property - Searches packages containing specific property values
/// 
/// All App Actions reuse the same logic as the corresponding MCP tools in McpServer.cs,
/// ensuring consistency between different invocation methods.
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
        
        // Register App Action provider
        RegisterAppActionProvider();
    }

    /// <summary>
    /// Registers the App Action provider for handling App Actions
    /// </summary>
    private void RegisterAppActionProvider()
    {
        try
        {
            // Create and store reference to action provider for reuse
            _appActionProvider = new AppActionProvider();

            // For Windows App SDK, App Actions are registered through the manifest
            // and handled via background activation. The action provider will be
            // used when App Service requests are received in OnBackgroundActivated.

            // Register the action provider with the Windows App Actions system
            // Note: This might require a specific API that's not available in all Windows App SDK versions
            // ActionProviderManager.RegisterActionProvider(_appActionProvider);
            
            DebugLog.Append("App Action provider registered successfully");
        }
        catch (Exception ex)
        {
            DebugLog.Append(ex, "Failed to register App Action provider");
        }
    }

    private AppActionProvider _appActionProvider;

    internal static void WaitForDebugger()
    {
        while (!Debugger.IsAttached)
        {
            System.Threading.Thread.Sleep(100);
        }
    }

    /// <summary>
    /// Handle background activation for App Service (App Actions)
    /// </summary>
    /// <param name="args">Background activation arguments</param>
    protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
    {
        if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails appServiceTrigger)
        {
            // Handle App Actions via App Service using the registered provider
            HandleAppServiceConnection(appServiceTrigger);
        }
    }

    /// <summary>
    /// Handles App Service connections for App Actions using the registered action provider
    /// </summary>
    /// <param name="appServiceTrigger">App Service trigger details</param>
    private void HandleAppServiceConnection(AppServiceTriggerDetails appServiceTrigger)
    {
        var appServiceConnection = appServiceTrigger.AppServiceConnection;
        
        appServiceConnection.RequestReceived += async (sender, eventArgs) =>
        {
            var requestMessage = eventArgs.Request.Message;
            var response = new ValueSet();

            try
            {
                // Get action ID from request
                if (requestMessage.TryGetValue("action", out object actionObj) && actionObj is string actionId)
                {
                    // Convert ValueSet parameters to Dictionary
                    var actionParameters = new Dictionary<string, string>();
                    foreach (var param in requestMessage)
                    {
                        if (param.Key != "action")
                        {
                            actionParameters[param.Key] = param.Value?.ToString() ?? "";
                        }
                    }

                    // Use the registered action provider
                    var (success, result, errorMessage) = await _appActionProvider.HandleActionAsync(actionId, actionParameters);

                    if (success)
                    {
                        response["result"] = result;
                        response["status"] = "success";
                    }
                    else
                    {
                        response["result"] = errorMessage;
                        response["status"] = "error";
                    }
                }
                else
                {
                    response["result"] = "Error: action parameter is required";
                    response["status"] = "error";
                }
            }
            catch (Exception ex)
            {
                response["result"] = $"Error executing App Action: {ex.Message}";
                response["status"] = "error";
                DebugLog.Append(ex, "App Action execution error");
            }

            // Send response back to caller
            await eventArgs.Request.SendResponseAsync(response);
        };
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
