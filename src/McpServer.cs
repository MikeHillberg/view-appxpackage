using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

namespace ViewAppxPackage;

/// <summary>
/// MCP (Model Context Protocol) Server service that provides tools to interact with AppX package data.
/// This service exposes package information through MCP tools, allowing external systems to query
/// package family names and other package information.
/// 
/// Usage:
/// To start the application in MCP server mode, use the command line argument:
/// view-appxpackage.exe -mcp
/// 
/// In this mode, the application will:
/// 1. Load all package data without showing the UI
/// 2. Start an MCP server that exposes tools for querying package information
/// 3. Wait for package loading to complete before serving requests
/// 4. Provide the 'list_package_family_names' tool to retrieve all package family names
/// 
/// The server will continue running until terminated and can serve multiple MCP tool requests.
/// 
/// https://modelcontextprotocol.io/quickstart/user
/// </summary>
[McpServerToolType]
public class McpServer
{
    public McpServer()
    {
    }

    /// <summary>
    /// MCP Tool: Lists all Package Family Names from the loaded packages.
    /// This tool provides access to the package family names of all installed packages
    /// that have been loaded by the application.
    /// </summary>
    /// <returns>A list of package family names</returns>
    [McpServerTool(Name = "list_package_family_names")]
    [Description("Lists all Package Family Names from loaded MSIX (AppX) packages")]
    public IEnumerable<string> ListPackageFamilyNames()
    {
        if (!MyThreading.CurrentIsWorkerThread)
        {
            // Forward the call from the console input thread to the worker thread
            IEnumerable<string> names = null;
            MyThreading.RunOnWorkerAsync(() =>
            {
                names = ListPackageFamilyNames();
            }).Wait();

            return names;
        }

        try
        {
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

    /// <summary>
    /// MCP Tool: Gets properties for a specific package family name.
    /// Doesn't get all properties, to prevent overwhelming the prompt.
    /// Todo: more tools to get more properties (such as the Apps list)
    /// </summary>
    /// <param name="packageFamilyName"></param>
    /// <returns></returns>
    [McpServerTool(Name = "get_package_properties")]
    [Description("Gets the following properties for a Package Family Name: " +
        "Name, DisplayName, InstalledDate, Size, PublisherName, PublisherID, Version, FullName, Capabilities, Architecture, PublisherDisplayName, SignatureKind, Status. " +
        "The properties are returned in the form (Property=Value), with each property on a new line")]
    public string GetPackageProperties(string packageFamilyName)
    {
        if (!MyThreading.CurrentIsWorkerThread)
        {
            string properties = null;
            MyThreading.RunOnWorkerAsync(() =>
            {
                properties = GetPackageProperties(packageFamilyName);
            }).Wait();

            return properties;
        }

        try
        {
            // Get the packages from the catalog model
            var packages = PackageCatalogModel.Instance?.Packages;
            if (packages == null)
            {
                DebugLog.Append("Warning: Package catalog is null when getting package manifest");
                return string.Empty;
            }

            // Find the package
            var package = packages
                .Where(p => p.FamilyName == packageFamilyName)
                .FirstOrDefault();
            if (package == null)
            {
                DebugLog.Append("No package found");
                return string.Empty;
            }

            return $"(Name={package.Name})\n" +
                   $"(DisplayName={package.DisplayName})\n" +
                   $"(InstalledDate={package.InstalledDate})\n" +
                   $"(Size={package.Size})\n" +
                   $"(PublisherDisplayName={package.PublisherDisplayName})\n" +
                   $"(PublisherID={package.PublisherId})\n" +
                   $"(Version={package.Version})\n" +
                   $"(FullName={package.FullName})\n" +
                   $"(Capabilities={string.Join(", ", package.Capabilities)})\n" +
                   $"(Architecture={package.Architecture})\n" +
                   $"(Publisher={package.Publisher})\n" +
                   $"(SignatureKind={package.SignatureKind})\n" +
                   $"(Status={package.Status})";
        }
        catch (Exception ex)
        {
            DebugLog.Append(ex, $"Error in GetManifestForPackageFamilyName");
            return string.Empty;
        }
    }

    /// <summary>
    /// MCP tool to find a package with a property value.
    /// (Does a case-insensitive "contains" check.)
    /// Returns a string with a list of family names.
    /// </summary>
    [McpServerTool(Name = "find_packages_containing_property")]
    [Description("Gets a list of MSIX package family names for packages on this  machine that have a property containing a value" +
        "The allowable property names are: " +
        "Name, DisplayName, InstalledDate, Size, PublisherName, PublisherID, Version, FullName, Capabilities, Architecture, PublisherDisplayName, SignatureKind, Status. " +
        "The case of the property value doesn't matter." +
        "The return value will be a string of package family names, each name on a new line.")]
    public string FindPackagesContainingProperty(string propertyName, string propertyValue)
    {
        if (!MyThreading.CurrentIsWorkerThread)
        {
            string pfns = null;
            MyThreading.RunOnWorkerAsync(() =>
            {
                pfns = FindPackagesContainingProperty(propertyName, propertyValue);
            }).Wait();

            return pfns;
        }

        StringBuilder packageFamilyNames = new();
        var packages = PackageCatalogModel.Instance?.Packages;
        PackageModel.EnsurePackageModelPropertyInfos();

        foreach (var package in packages)
        {
            foreach (var propInfo in PackageModel.PackageModelPropertyInfos)
            {
                if (propertyName != propInfo.Name)
                {
                    continue;
                }

                var propValue = propInfo.GetValue(package);
                var propValueString = propValue?.ToString();
                if(propValueString != null && propValueString.Contains(propertyValue, StringComparison.OrdinalIgnoreCase))
                {
                    packageFamilyNames.AppendLine(package.FamilyName);
                }
            }
        }

        return packageFamilyNames.ToString();
    }
}