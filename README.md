view-appxpackage
===

Use this app to view and manage MSIX packages on this device

Sample screen:

![Sample](./src/Screenshots/1.png)


Command line examples:

```
view-appxpackage
view-appxpackage *calculator*
get-appxpackage | ? {$_.IsDevelopmentMode} | view-appxpackage
```

[Full help info](./src/Assets/Help.md)
