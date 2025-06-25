using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace ViewAppxPackage;

/// <summary>
/// App Actions provider that handles App Service requests for App Actions.
/// This provides the proper implementation following Microsoft's App Actions guidelines,
/// making package information queries discoverable via Windows Search and context menu.
/// 
/// The App Actions provide three main functions mirroring the MCP tools:
/// 1. ListPackageFamilyNames - Lists all MSIX/AppX package family names
/// 2. GetPackageProperties - Retrieves detailed properties for a specific package
/// 3. FindPackagesContainingProperty - Searches packages containing specific property values
/// 
/// All App Actions reuse the same logic as the corresponding MCP tools in McpServer.cs,
/// ensuring consistency between different invocation methods.
/// </summary>
public static class AppActionProvider
{
    /// <summary>
    /// Handles App Service connection requests for App Actions.
    /// </summary>
    /// <param name="args">App Service connection event arguments</param>
    public static async void OnAppServiceConnected(AppServiceTriggerDetails args)
    {
        var appServiceConnection = args.AppServiceConnection;
        
        appServiceConnection.RequestReceived += async (sender, eventArgs) =>
        {
            var requestMessage = eventArgs.Request.Message;
            var response = new ValueSet();

            try
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

                // Get action ID from request
                if (requestMessage.TryGetValue("action", out object actionObj) && actionObj is string actionId)
                {
                    // Create MCP server instance to reuse existing tool logic
                    var mcpServer = new McpServer();
                    string actionResult = "";

                    switch (actionId.ToLower())
                    {
                        case "listpackagefamilynames":
                            // App Action: List Package Family Names
                            var familyNames = mcpServer.ListPackageFamilyNames();
                            actionResult = string.Join("\n", familyNames);
                            break;

                        case "getpackageproperties":
                            // App Action: Get Package Properties
                            if (requestMessage.TryGetValue("packageFamilyName", out object packageFamilyNameObj) &&
                                packageFamilyNameObj is string packageFamilyName)
                            {
                                actionResult = mcpServer.GetPackageProperties(packageFamilyName);
                            }
                            else
                            {
                                actionResult = "Error: packageFamilyName parameter is required";
                                response["status"] = "error";
                            }
                            break;

                        case "findpackagescontainingproperty":
                            // App Action: Find Packages Containing Property
                            if (requestMessage.TryGetValue("propertyName", out object propertyNameObj) &&
                                requestMessage.TryGetValue("propertyValue", out object propertyValueObj) &&
                                propertyNameObj is string propertyName &&
                                propertyValueObj is string propertyValue)
                            {
                                actionResult = mcpServer.FindPackagesContainingProperty(propertyName, propertyValue);
                            }
                            else
                            {
                                actionResult = "Error: propertyName and propertyValue parameters are required";
                                response["status"] = "error";
                            }
                            break;

                        default:
                            actionResult = $"Unknown App Action: {actionId}";
                            response["status"] = "error";
                            break;
                    }

                    response["result"] = actionResult;
                    if (!response.ContainsKey("status"))
                    {
                        response["status"] = "success";
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
}