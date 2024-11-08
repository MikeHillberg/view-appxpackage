view-appxpackage
===

Use this app to view and manage MSIX packages on this device

The list on the left shows all the packages.
This is a live list, so it will update automatically if packages are added to or removed from the system.

There are two ways to search the list:

* Filter (Ctrl+F) to filter the list of packages, just like `get-appxpackage`
* Search (Ctrl+E) to search all properties of a package, not just the Name. (There's a delay on startup before Search is available)

Run elevated and the `All Users` toggle will become available.
Check this to see all packages on the device.

Command line examples:

```
view-appxpackage
view-appxpackage *calculator*
get-appxpackage *calculator* | view-appxpackage
```

Additional things you can do:

* Launch the package with the Launch button (if applicable for the package)
* Open a package's AppxManifest
* Remove a package
* Add a package
* Register a package (can be used to add unsigned packages)
* Open in Store to go to the Store page

