using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

namespace ViewAppxPackage;

public class SettingsItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate ContainerTemplate { get; set; }
    public DataTemplate SettingTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        Debug.Assert(item != null);
        var setting = item as PackageSettingBase;
        var template = (item is PackageSettingContainer) ? ContainerTemplate : SettingTemplate;

        return template;
    }
}
