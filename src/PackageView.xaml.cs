using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Management.Core;
using Windows.Storage;


namespace ViewAppxPackage
{
    /// <summary>
    /// Viewre of a PackageModel
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
            DependencyProperty.Register("Settings", typeof(IList<PackageSettingBase>), typeof(PackageView), new PropertyMetadata(null));

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

            if (settings == null || settings.Count == 0)
            {
                IsSettingsEmpty = true;
            }

            // Ignore the result if the Package has changed while we were away
            if (Package == package)
            {
                Settings = settings;
            }
        }

        private async void DeleteSetting(object sender, RoutedEventArgs e)
        {
            ApplicationData applicationData = Package.GetApplicationData();
            if (applicationData == null)
            {
                return;
            }

            var button = sender as Button;
            var setting = button.Tag as PackageSettingBase;

            var result = await MyMessageBox.Show(
                                setting.Name,
                                title: "Remove?",
                                isOKEnabled: true,
                                closeButtonText: "Cancel");
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            var parentContainer = GetParentContainerFor(setting);
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

        ApplicationDataContainer GetParentContainerFor(PackageSettingBase settingBase)
        {
            if (settingBase.Parent == null)
            {
                ApplicationData applicationData = Package.GetApplicationData();
                if (applicationData == null)
                {
                    DebugLog.Append($"Coudln't get ApplicationData for {this.Package.FullName}");
                }

                return applicationData?.LocalSettings;
            }

            var parentContainer = GetParentContainerFor(settingBase.Parent);
            return parentContainer.Containers[settingBase.Parent.Name];
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

        private void AppBarButton_Click(object sender, RoutedEventArgs e)
        {

        }

        async private void DeleteSetting2(object sender, RoutedEventArgs e)
        {
            var setting = _settingsTree.SelectedItem as PackageSettingBase;
            if (setting == null)
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

            var parentContainer = GetParentContainerFor(setting);
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
    }

    // bugbug
    // Name property should be in PackageSettingBase as it is,
    // but Children should be in PackageSettingContainer and Value should be in PackageSetting.
    // And the templates should be x:DataType to PackageSettingContainer and PackageSetting.
    // But there appears to be a bug in ItemsControl where it caches the
    // IDataTemplateComponent (the generated Bindings code) on the templateRoot,
    // and it's getting the type wrong. (See XamlBindingHelperFactory::GetDataTemplateComponentStatic)
    // So it calls the generated binding code and asks it to set the item and gets a type cast exception.
    // As a workaround, both templates are x:DataType the base type, so no type cast exception.
    // That still means it's hitting the bug, but at least it's not crashing.
    // (Happens on YourPhone app, might requiring setting IsExpanded=true in the template)
    public class PackageSettingBase
    {
        public PackageSettingBase()
        {
            This = this;
        }

        public string Name { get; set; }
        public PackageSettingBase Parent;
        public string Value { get; set; }
        public IList<PackageSettingBase> Children;

        public PackageSettingBase This { get; private set; }
    }

    public class PackageSetting : PackageSettingBase
    {
    }

    public class PackageSettingContainer : PackageSettingBase
    {
    }


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

}