using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using WinRT;

namespace ViewAppxPackage;

/// <summary>
/// Viewer of a PackageModel
/// </summary>
public sealed partial class PackageView : UserControl
{
    public PackageView()
    {
        Instance = this;
        this.InitializeComponent();
    }

    internal static PackageView Instance;

    public PackageModel Package
    {
        get { return (PackageModel)GetValue(PackageProperty); }
        set { SetValue(PackageProperty, value); }
    }
    public static readonly DependencyProperty PackageProperty =
        DependencyProperty.Register("Package", typeof(PackageModel), typeof(PackageView),
            new PropertyMetadata(null, (d, dp) => (d as PackageView).PackageChanged()));

    private void PackageChanged()
    {
        // bugbug: this should be happening already with the x:Bind
        _root.Visibility = Package == null ? Visibility.Collapsed : Visibility.Visible;

        ReadSettingsAsync();
    }

    Visibility NotEmpty(PackageModel package)
    {
        return package == null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// The MinWidth to use for the labels (first column)
    /// </summary>
    public double MinLabelWidth
    {
        get { return (double)GetValue(MinLabelWidthProperty); }
        set { SetValue(MinLabelWidthProperty, value); }
    }
    public static readonly DependencyProperty MinLabelWidthProperty =
        DependencyProperty.Register("MinLabelWidth", typeof(double), typeof(PackageView), new PropertyMetadata(0.0));

    /// <summary>
    /// Navigate to a package
    /// </summary>
    private void GoToPackage(Hyperlink sender, HyperlinkClickEventArgs args)
    {
        var package = GetMyTag(sender) as PackageModel;
        MainWindow.Instance.SelectPackage(package);
    }

    /// <summary>
    /// Get a string with a list of the boolean properties that are set
    /// </summary>
    string GetTrueBooleans(PackageModel package)
    {
        return PackageModel.GetTrueBooleans(package);
    }

    /// <summary>
    /// Helper to hold state to make the hyperlink work
    /// </summary>
    public static object GetMyTag(DependencyObject obj)
    {
        return (object)obj.GetValue(MyTagProperty);
    }
    public static void SetMyTag(DependencyObject obj, object value)
    {
        obj.SetValue(MyTagProperty, value);
    }
    public static readonly DependencyProperty MyTagProperty =
        DependencyProperty.RegisterAttached("MyTag", typeof(object), typeof(PackageView), new PropertyMetadata(null));

    /// <summary>
    /// App data settings for the package
    /// </summary>
    public IList<PackageSettingBase> Settings
    {
        get { return (IList<PackageSettingBase>)GetValue(SettingsProperty); }
        set { SetValue(SettingsProperty, value); }
    }
    public static readonly DependencyProperty SettingsProperty =
        DependencyProperty.Register("Settings", typeof(IList<PackageSettingBase>), typeof(PackageView),
            new PropertyMetadata(null, (d, dp) => (d as PackageView).SettingsChanged()));
    void SettingsChanged()
    {
        MyThreading.PostToUI(() =>
        {
            if (Settings != null && Settings.Count > 0)
            {
                // bugbug:
                // Separate from the below issues, this is crashing too
                var rootItem = Settings[0];
                _settingsTree.SelectedItem = rootItem;

                //// bugbug: this triggers an AV in Xaml
                //var node = _settingsTree.SelectedNode;
                //if (node != null)
                //{
                //    node.IsExpanded = true;
                //}

                //// bugbug: this too
                //if (_settingsTree.RootNodes.Count > 0)
                //{
                //    foreach (var node in _settingsTree.RootNodes[0].Children)
                //    {
                //        if (node.Content is PackageSettingContainer)
                //        {
                //            node.IsExpanded = true;
                //            break;
                //        }
                //    }
                //}

                //// bugbug: AV is in the else case (null AV)
                //_Check_return_ HRESULT ModernCollectionBasePanel::CacheManager::InitCollectionsCache()
                //{
                //    HRESULT hr = S_OK;

                //    if (m_cachedIsGrouped)
                //    {
                //        ctl::ComPtr<xaml_data::ICollectionView> spCollectionView;
                //        ctl::ComPtr<wfc::IObservableVector<IInspectable*>> spCollectionGroups;
                //        IFC(m_strongHost->get_CollectionView(&spCollectionView));
                //        IFC(spCollectionView->get_CollectionGroups(&spCollectionGroups));
                //        IFC(spCollectionGroups.As<wfc::IVector<IInspectable*>>(&m_strongCollectionGroupsAsV));
                //        // Current Index is Invalid
                //        m_cachedGroupIndex = -2;
                //    }
                //    else
                //    {
                //        IFC(m_strongHost->get_View(&m_strongView));
                //    }
                //    m_isCollectionCacheValid = TRUE;


                }
            },
        Microsoft.UI.Dispatching.DispatcherQueuePriority.Low);
    }

    /// <summary>
    /// Read the app data settings into the Settings property
    /// </summary>
    async void ReadSettingsAsync()
    {
        Settings = null;

        var package = Package;

        // We might pass through a null state temporarily
        if (package == null)
        {
            DebugLog.Append($"null package in ReadSettings");
            return;
        }

        IsLoadingSettings = true;
        IsSettingsEmpty = false;

        IList<PackageSettingBase> settings = null;
        settings = await package.GetAllSettingsAsync();

        IsLoadingSettings = false;

        // Ignore the result if the Package has changed while we were away
        if (Package == package)
        {
            Settings = settings;

            if (settings == null || settings.Count == 0)
            {
                IsSettingsEmpty = true;
            }
        }
    }

    /// <summary>
    /// Get the ApplicationDataContainer parent of a PackageSetting value or container
    /// (The ADCs aren't kept open forever)
    /// </summary>
    static internal ApplicationDataContainer GetAppDataContainerForPackage(PackageModel package, PackageSettingBase settingBase)
    {
        // A setting with no parent is at the root, return the package's ApplicationData.LocalSettings
        if (settingBase.Parent == null)
        {
            ApplicationData applicationData = package.GetApplicationData();
            if (applicationData == null)
            {
                DebugLog.Append($"Coudln't get ApplicationData for {package.FullName}");
            }

            return applicationData?.LocalSettings;
        }

        // For a setting with a parent (setting is in a child Container),
        // walk up the setting's parent chain then back down getting ADCs
        else
        {
            var parentContainer = GetAppDataContainerForPackage(package, settingBase.Parent);

            return (settingBase is PackageSettingContainer)
                ? parentContainer.Containers[settingBase.Name]
                : parentContainer;
        }
    }

    public bool IsLoadingSettings
    {
        get { return (bool)GetValue(IsLoadingSettingsProperty); }
        set { SetValue(IsLoadingSettingsProperty, value); }
    }
    public static readonly DependencyProperty IsLoadingSettingsProperty =
        DependencyProperty.Register("IsLoadingSettings", typeof(bool), typeof(PackageView), new PropertyMetadata(false));


    public bool IsSettingsEmpty
    {
        get { return (bool)GetValue(IsSettingsEmptyProperty); }
        set { SetValue(IsSettingsEmptyProperty, value); }
    }
    public static readonly DependencyProperty IsSettingsEmptyProperty =
        DependencyProperty.Register("IsSettingsEmpty", typeof(bool), typeof(PackageView), new PropertyMetadata(false));

    private void DeleteSetting2_Click(object sender, RoutedEventArgs e)
    {
        var setting = _settingsTree.SelectedItem as PackageSettingBase;
        if (setting == null)
        {
            return;
        }
        DeleteSetting2(setting);
    }

    async private void DeleteSetting2(PackageSettingBase setting)
    {
        if (setting.IsRoot)
        {
            return;
        }

        ApplicationData applicationData = Package.GetApplicationData();
        if (applicationData == null)
        {
            return;
        }

        var result = await MyMessageBox.Show(
                            setting.Name,
                            title: "Remove?",
                            isOKEnabled: true,
                            closeButtonText: "Cancel");
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var parentContainer = GetAppDataContainerForPackage(this.Package, setting);
        if (setting is PackageSettingContainer)
        {
            parentContainer.DeleteContainer(setting.Name);
        }
        else
        {
            parentContainer.Values.Remove(setting.Name);
        }

        // Re-read the settings, which should now not include {name}
        ReadSettingsAsync();
    }

    private void Hyperlink_AppLaunch(Hyperlink sender, HyperlinkClickEventArgs args)
    {
        var app = Utils.GetTag(sender) as AppListEntryModel;
        app.Launch();
    }

    private void EditSetting_Click(object sender, RoutedEventArgs e)
    {
        var item = _settingsTree.SelectedItem;
        if (item == null)
        {
            return;
        }

        "foo".Equals("bar");
        string.Compare("foo", "bar", StringComparison.OrdinalIgnoreCase);

        var container = _settingsTree.ContainerFromItem(item) as TreeViewItem;
        if (container == null)
        {
            return;
        }

        // bugbug: probably a better way to dig out the SettingEditBox
        var stackPanel = container.Content as StackPanel;
        stackPanel = stackPanel.Children[0] as StackPanel;
        var editBox = stackPanel.Children[1] as SettingEditBox;
        editBox.StartEditing();
    }

    /// <summary>
    /// Usage is easier if the TreeView selection updates as focus moves
    /// </summary>
    private void TreeViewItem_GotFocus(object sender, RoutedEventArgs e)
    {
        var treeViewItem = sender as TreeViewItem;
        if (treeViewItem is null)
        {
            return;
        }
        treeViewItem.IsSelected = true;

    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        ReadSettingsAsync();
    }

    private void PackageSettingView_DeleteRequested(object sender, PackageSettingBase packageSetting)
    {
        DeleteSetting2(packageSetting);
    }

    private void DeleteSetting_Click(object sender, RoutedEventArgs e)
    {
        var packageSetting = (sender as MenuFlyoutItem).Tag as PackageSettingBase;
        DeleteSetting2(packageSetting);
    }

    public bool IsRootSelected
    {
        get { return (bool)GetValue(IsRootSelectedProperty); }
        set { SetValue(IsRootSelectedProperty, value); }
    }
    public static readonly DependencyProperty IsRootSelectedProperty =
        DependencyProperty.Register("IsRootSelected", typeof(bool), typeof(PackageView), new PropertyMetadata(false));

    private void _settingsTree_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
    }
    public object SelectedSetting
    {
        get { return (object)GetValue(SelectedSettingProperty); }
        set { SetValue(SelectedSettingProperty, value); }
    }
    public static readonly DependencyProperty SelectedSettingProperty =
        DependencyProperty.Register("SelectedSetting", typeof(object), typeof(PackageView),
            new PropertyMetadata(null, (d, dp) => (d as PackageView).SelectedSettingChanged()));
    void SelectedSettingChanged()
    {
        var selectedSetting = _settingsTree.SelectedItem as PackageSettingBase;
        IsRootSelected = selectedSetting?.IsRoot == true;
    }

    private void NewSettingValue_Click(object sender, RoutedEventArgs e)
    {
        var setting = (sender as FrameworkElement).Tag as PackageSettingBase;

        _ = AddNewSetting(setting);
    }

    private async Task AddNewSetting(PackageSettingBase referenceSetting)
    {
        if (referenceSetting == null)
        {
            return;
        }

        var targetContainer = GetAppDataContainerForPackage(this.Package, referenceSetting);

        var newSettingDialog = new NewPackageSettingValue(targetContainer);

        // bugbug: without this it crashes the native debugger, doesn't crash into the managed debugger
        newSettingDialog.XamlRoot = this.XamlRoot;

        var result = await newSettingDialog.ShowAsync();
        if (result == ContentDialogResult.Primary || newSettingDialog.IsSubmittedProgramatically)
        {
            if (newSettingDialog.NewValue == null)
            {
                // Don't think this case can happen, but if it does treat it as a no op;
                // setting to null is actually a delete
                return;
            }

            var newValueString = PackageSettingBase.ConvertSettingValueToString(newSettingDialog.NewValue);
            targetContainer.Values[newSettingDialog.SettingName] = newSettingDialog.NewValue;
            ReadSettingsAsync();
        }
    }

    private void PackageSettingView_NewRequested(object sender, PackageSettingBase e)
    {
        _ = AddNewSetting(e);
    }

    private void NewContainer_Click(object sender, RoutedEventArgs e)
    {
        var setting = (sender as FrameworkElement).Tag as PackageSettingBase;
        _ = AddNewContainer(setting);
    }

    private async Task AddNewContainer(PackageSettingBase referenceSetting)
    {
        if (referenceSetting == null)
        {
            return;
        }

        var targetContainer = GetAppDataContainerForPackage(this.Package, referenceSetting);

        var newSettingDialog = new NewPackageSettingContainer(targetContainer);
        newSettingDialog.XamlRoot = this.XamlRoot;

        var result = await newSettingDialog.ShowAsync();
        if (result == ContentDialogResult.Primary || newSettingDialog.IsSubmittedProgramatically)
        {
            if (newSettingDialog.ContainerName == null)
            {
                return;
            }

            targetContainer.CreateContainer(newSettingDialog.ContainerName, ApplicationDataCreateDisposition.Always);
            ReadSettingsAsync();
        }
    }

    private void PackageSettingView_NewContainerRequested(object sender, PackageSettingBase e)
    {
        _ = AddNewContainer(e);
    }

}
