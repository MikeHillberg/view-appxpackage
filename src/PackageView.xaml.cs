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
                // bugbug(d5aecdc)
                // This was crashing with an AV in Xaml up until commit d5aecdc when it was removed
                // var rootItem = Settings[0];
                // _settingsTree.SelectedItem = rootItem;

                // bugbug(d5aecdc)
                // This was crashing with an AV in Xaml up until commit d5aecdc when it was removed
                // (in addition to above case)
                //var node = _settingsTree.SelectedNode;
                //if (node != null)
                //{
                //    node.IsExpanded = true;
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

        var parentContainer = this.Package.GetAppDataContainerForSetting(setting);
        if (setting is PackageSettingContainer)
        {
            parentContainer.DeleteContainer(setting.Name);
        }
        else
        {
            parentContainer.Values.Remove(setting.Name);
        }

        //applicationData.SignalDataChanged();

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

        var container = _settingsTree.ContainerFromItem(item) as TreeViewItem;
        if (container == null)
        {
            return;
        }

        PackageSettingView settingView = container as PackageSettingView;
        settingView.EditBox.StartEditing();
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

        IsSettingValueSelected = selectedSetting is PackageSettingValue;
        IsSettingContainerSelected = selectedSetting is PackageSettingContainer;
    }

    /// <summary>
    /// True if a setting value is selected (rather than a container or null)
    /// </summary>
    public bool IsSettingValueSelected
    {
        get { return (bool)GetValue(IsSettingValueSelectedProperty); }
        set { SetValue(IsSettingValueSelectedProperty, value); }
    }
    public static readonly DependencyProperty IsSettingValueSelectedProperty =
        DependencyProperty.Register("IsSettingValueSelected", typeof(bool), typeof(PackageView), new PropertyMetadata(false));

    /// <summary>
    /// True if a setting container is selected (rather than a value or null)
    /// </summary>
    public bool IsSettingContainerSelected
    {
        get { return (bool)GetValue(IsSettingContainerSelectedProperty); }
        set { SetValue(IsSettingContainerSelectedProperty, value); }
    }
    public static readonly DependencyProperty IsSettingContainerSelectedProperty =
        DependencyProperty.Register("IsSettingContainerSelected", typeof(bool), typeof(PackageView), new PropertyMetadata(false));

    private void NewSettingValue_Click(object sender, RoutedEventArgs e)
    {
        var setting = (sender as FrameworkElement).Tag as PackageSettingBase;

        _ = AddNewSetting(setting);
    }

    private async Task AddNewSetting(PackageSettingBase referenceSetting)
    {
        var targetContainer = this.Package.GetAppDataContainerForSetting(referenceSetting);

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
            
            //var applicationData = Package.GetApplicationData();
            //if (applicationData != null)
            //{
            //    applicationData.SignalDataChanged();
            //}
            
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
        var targetContainer = this.Package.GetAppDataContainerForSetting(referenceSetting);

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
            
            //var applicationData = Package.GetApplicationData();
            //if (applicationData != null)
            //{
            //    applicationData.SignalDataChanged();
            //}
            
            ReadSettingsAsync();
        }
    }

    private void PackageSettingView_NewContainerRequested(object sender, PackageSettingBase e)
    {
        _ = AddNewContainer(e);
    }

}
