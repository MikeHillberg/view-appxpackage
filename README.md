view-appxpackage
===

Use this app to view and manage MSIX packages on this device

[Full help info](./Package/Assets/Help.md)

# Install

[Microsoft Store](https://www.microsoft.com/store/productId/9MZ8SSBDQG3F?ocid=pdpshare)

_or_

`winget install view-appxpackage --source msstore`


# Sample screen

![Sample](./src/Screenshots/1.png)


# Command line examples

```
view-appxpackage
view-appxpackage *calculator*
get-appxpackage | ? {$_.IsDevelopmentMode} | view-appxpackage
```

