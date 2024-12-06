param($message)

$Host.UI.RawUI.WindowTitle = "RunAsPackage"
write-host $message

[Windows.Storage.ApplicationData, Windows.Storage.ApplicationData, ContentType=WindowsRuntime] | out-null
[Windows.Storage.ApplicationData]::Current.LocalFolder.Path | cd

write-host ""
write-host ""
write-host "** Windows.ApplicationModel.Package.Current **"
[Windows.ApplicationModel.Package,Windows.ApplicationModel.Package,ContentType=WindowsRuntime] | out-null
[Windows.ApplicationModel.Package]::Current

