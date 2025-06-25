# Testing App Actions Implementation

## Overview

This document describes how to test the App Actions implementation in the ViewAppxPackage application. The App Actions follow Microsoft's official App Actions guidelines using IActionProvider interface and proper manifest declarations.

## App Actions Available

The application provides three App Actions that mirror the functionality of the existing MCP tools:

1. **ListPackageFamilyNames** - Lists all MSIX/AppX package family names
2. **GetPackageProperties** - Gets properties for a specific package family name
3. **FindPackagesContainingProperty** - Finds packages containing specific property values

## Implementation Details

### Proper Architecture

The implementation follows Microsoft's App Actions best practices:

- **App Service**: Uses `windows.appService` extension in manifest
- **App Actions Declaration**: Uses `windows.appActions` extension with proper action IDs
- **JSON Action Definitions**: Each action has a JSON file defining parameters and examples
- **Background Activation**: Handles App Service requests via `OnBackgroundActivated`

### Key Files

- `src/AppActionProvider.cs` - App Service handler for App Actions
- `src/AppActions/ListPackageFamilyNames.json` - Action definition for list action
- `src/AppActions/GetPackageProperties.json` - Action definition for properties action
- `src/AppActions/FindPackagesContainingProperty.json` - Action definition for find action
- `Package/Package.appxmanifest` - Manifest with App Service and App Actions declarations

## Testing Steps

### 1. Deploy the Application

First, deploy the application to test App Actions integration:

```cmd
# Build and deploy the package
# App Actions will be registered with the system after deployment
```

### 2. Windows Search Integration

After deployment, App Actions should be discoverable via Windows Search:

```
1. Press Windows key and type "List Package" - should show the App Action
2. Press Windows key and type "Get Package Properties" - should show the App Action  
3. Press Windows key and type "Find Packages" - should show the App Action
4. Click on any action to execute it
```

### 3. App Service Testing

App Actions communicate via App Service. Test this by:

```
1. Actions executed from Windows Search should invoke the App Service
2. Results should be displayed in the system's action result UI
3. Check Windows Event Viewer for any App Service errors
```

### 4. Verify Logic Reuse

All App Actions should produce the same results as the MCP tools:

```cmd
# Compare MCP tool output with App Action output
# They should be identical since they use the same McpServer methods
```

## Expected Behavior

### List Package Family Names Action
- **Input**: No parameters required
- **Output**: Newline-separated list of all package family names
- **Search Terms**: "List Package", "Package Family Names", "Show Packages"

### Get Package Properties Action  
- **Input**: `packageFamilyName` parameter
- **Output**: Detailed properties of the specified package
- **Search Terms**: "Get Package Properties", "Package Details", "Show Package Info"

### Find Packages by Property Action
- **Input**: `propertyName` and `propertyValue` parameters  
- **Output**: List of packages matching the property criteria
- **Search Terms**: "Find Packages", "Search Packages", "Package Property"

## Troubleshooting

### App Actions Not Appearing in Search
1. Ensure application is properly deployed and registered
2. Check that manifest declarations are valid
3. Verify JSON action definition files are included in package
4. Restart Windows Search service if needed

### App Service Errors  
1. Check Windows Event Viewer for App Service activation errors
2. Verify App Service name matches manifest declaration
3. Ensure background activation is handled correctly

### Incorrect Results
1. Verify App Actions use the same McpServer methods as MCP tools
2. Check that parameter parsing is working correctly
3. Compare output with direct MCP tool execution

## Validation Checklist

- [ ] App Actions appear in Windows Search results
- [ ] App Actions execute without errors  
- [ ] Results match corresponding MCP tool output
- [ ] Parameters are passed correctly to actions
- [ ] Error handling works for invalid parameters
- [ ] App Service activation works correctly
- [ ] JSON action definitions are valid
- [ ] Manifest declarations are correct