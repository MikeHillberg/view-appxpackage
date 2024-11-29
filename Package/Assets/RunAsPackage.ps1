param($message)

$Host.UI.RawUI.WindowTitle = "RunAsPackage"
write-host $message

write-host ""
write-host "** Windows.ApplicationModel.Package.Current **"
[Windows.ApplicationModel.Package,Windows.ApplicationModel.Package,ContentType=WindowsRuntime] | out-null
[Windows.ApplicationModel.Package]::Current

