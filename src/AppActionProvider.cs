using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace ViewAppxPackage;

/// <summary>
/// App Actions provider following Microsoft's App Actions guidelines for Windows App SDK,
/// implementing the IActionProvider interface to make package information queries 
/// discoverable via Windows Search and context menu.
/// 
/// The App Actions provide three main functions mirroring the MCP tools:
/// 1. ListPackageFamilyNames - Lists all MSIX/AppX package family names
/// 2. GetPackageProperties - Retrieves detailed properties for a specific package
/// 3. FindPackagesContainingProperty - Searches packages containing specific property values
/// 
/// All App Actions reuse the same logic as the corresponding MCP tools in McpServer.cs,
/// ensuring consistency between different invocation methods.
/// </summary>
public class AppActionProvider : IActionProvider
{
    /// <summary>
    /// Handles execution of App Actions following Microsoft's IActionProvider interface.
    /// </summary>
    /// <param name="actionId">The identifier of the action to execute</param>
    /// <param name="parameters">Parameters for the action</param>
    /// <returns>Result of the action execution</returns>
    public async Task<ActionResult> HandleActionAsync(string actionId, IDictionary<string, object> parameters)
    {
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
                    if (parameters.TryGetValue("packageFamilyName", out object packageFamilyNameObj) &&
                        packageFamilyNameObj is string packageFamilyName)
                    {
                        actionResult = mcpServer.GetPackageProperties(packageFamilyName);
                    }
                    else
                    {
                        return ActionResult.CreateError("Missing required parameter: packageFamilyName");
                    }
                    break;

                case "findpackagescontainingproperty":
                    // App Action: Find Packages Containing Property
                    if (parameters.TryGetValue("propertyName", out object propertyNameObj) &&
                        parameters.TryGetValue("propertyValue", out object propertyValueObj) &&
                        propertyNameObj is string propertyName &&
                        propertyValueObj is string propertyValue)
                    {
                        actionResult = mcpServer.FindPackagesContainingProperty(propertyName, propertyValue);
                    }
                    else
                    {
                        return ActionResult.CreateError("Missing required parameters: propertyName and/or propertyValue");
                    }
                    break;

                default:
                    return ActionResult.CreateError($"Unknown action: {actionId}");
            }

            return ActionResult.CreateSuccess(actionResult);
        }
        catch (Exception ex)
        {
            DebugLog.Append(ex, "App Action execution error");
            return ActionResult.CreateError($"Error executing App Action: {ex.Message}");
        }
    }

    /// <summary>
    /// Legacy method for backwards compatibility with Dictionary parameters
    /// </summary>
    /// <param name="actionId">The identifier of the action to execute</param>
    /// <param name="parameters">Parameters for the action as string dictionary</param>
    /// <returns>Result tuple for backwards compatibility</returns>
    public async Task<(bool success, string result, string errorMessage)> HandleActionAsync(string actionId, Dictionary<string, string> parameters)
    {
        // Convert string dictionary to object dictionary for IActionProvider interface
        var objectParams = new Dictionary<string, object>();
        foreach (var param in parameters)
        {
            objectParams[param.Key] = param.Value;
        }

        var result = await HandleActionAsync(actionId, objectParams);
        return (result.IsSuccess, result.Content, result.ErrorMessage);
    }
}