view-appxpackage
===

Use this app to view and manage MSIX packages on this device

The list on the left shows all the packages.
It's a live list, so it will update automatically if packages are added to or removed from the system.


# Search and Filter

* **Filter** (Ctrl+F) to filter the list of packages, just like `get-appxpackage`
* **Search** (Ctrl+E) to search all properties of a package, not just the Name.

You can search for any package property, including _Name_, _Publisher_, and _Version_. 
Boolean properties like _IsFramework_ and _IsDevelopmentMode_ are also searchable. 
If you get unexpected matches, check the AppxManifest as they might be hidden there. 
Note: there’s a slight delay on startup before the search functionality becomes available.

Run elevated and the `All Users` toggle will become available.
Check this to see all packages on the device.


# Command line examples

```
view-appxpackage
view-appxpackage *calculator*
get-appxpackage | ? {$_.IsDevelopmentMode} | view-appxpackage
```


# Additional features

* **Launch** the package with the Launch button (if applicable for the package)
* **Open** a package's AppxManifest
* **Remove** a package
* **Add** a package
* **Sideload** a package (requires Developer Mode, can be used to add **unsigned** packages)
* View the **appx log** of system MSIX activity (same as get-appxlog)
* **Open in Store** to go to the Store page
* Run **PowerShell** with package identity. Note that it runs Medium IL; it doesn't run in AppContainer for an AppContainer package. See [`Invoke-CommandInDesktopPackage`](https://learn.microsoft.com/powershell/module/appx/invoke-commandindesktoppackage) for more info.

# MCP

This app is an MCP server, which can be accessed by running `view-appxpackage -mcp`.
Tools available are:
* Get all package family names
* Get package properties
* Search for packages containing a property value

To use from Claude desktop app, add to `claude_desktop_config.json`:

```
{
  "mcpServers": {
    "view-appxpackage": {
      "command": "view-appxpackage",
      "args": [
        "-mcp"
      ]
    }
  }
}
```

Example prompts:
* "What Paint MSIX package do I have on my machine"
* What version is the package
