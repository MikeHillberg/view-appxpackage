using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ViewAppxPackage;

/// <summary>
/// TreeViewItem that's a viewer for a PackageSettingValue
/// </summary>
public sealed partial class PackageSettingView : TreeViewItem
{
    public PackageSettingView()
    {
        this.InitializeComponent();
    }

    public PackageSettingBase PackageSettingValue
    {
        get { return (PackageSettingBase)GetValue(PackageSettingValueProperty); }
        set { SetValue(PackageSettingValueProperty, value); }
    }
    public static readonly DependencyProperty PackageSettingValueProperty =
        DependencyProperty.Register("PackageSettingValue", typeof(PackageSettingBase), typeof(PackageSettingView), 
            new PropertyMetadata(null,(d,dp) => (d as PackageSettingView).PackageSettingChanged()));

    void PackageSettingChanged()
    {
        IsValue = PackageSettingValue is PackageSettingValue;
    }

    /// <summary>
    /// True if this is a setting value, false if it's a container
    /// </summary>
    public bool IsValue
    {
        get { return (bool)GetValue(IsValueProperty); }
        set { SetValue(IsValueProperty, value); }
    }
    public static readonly DependencyProperty IsValueProperty =
        DependencyProperty.Register("IsValue", typeof(bool), typeof(PackageSettingView), new PropertyMetadata(false));

    public SettingEditBox EditBox => _editBox;

    // Context menu handler
    private void DeleteSettingClick(object sender, RoutedEventArgs e)
    {
        DeleteRequested?.Invoke(this, this.PackageSettingValue);
    }

    // Edit a setting value
    private void EditSettingClick(object sender, RoutedEventArgs e)
    {
        // No ops if already editing
        _editBox.StartEditing();
    }

    // Create a new setting value
    private void NewSettingValue_Click(object sender, RoutedEventArgs e)
    {
        NewSettingValueRequested?.Invoke(this, this.PackageSettingValue);
    }

    // Create a new setting container
    private void NewSettingContainer_Click(object sender, RoutedEventArgs e)
    {
        NewContainerRequested?.Invoke(this, this.PackageSettingValue);
    }

    // Raise these event when the context menu item is clicked.
    // Host does the actual work (we can't do it from here)
    public event EventHandler<PackageSettingBase> DeleteRequested;
    public event EventHandler<PackageSettingBase> NewContainerRequested;
    public event EventHandler<PackageSettingBase> NewSettingValueRequested;
}
