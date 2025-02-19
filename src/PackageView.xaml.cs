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

            ReadSettings();
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
        public IList<PackageSetting> Settings
        {
            get { return (IList<PackageSetting>)GetValue(SettingsProperty); }
            set { SetValue(SettingsProperty, value); }
        }
        public static readonly DependencyProperty SettingsProperty =
            DependencyProperty.Register("Settings", typeof(IList<PackageSetting>), typeof(PackageView), new PropertyMetadata(null));

        /// <summary>
        /// Read the app data settings into the Settings property
        /// </summary>
        async void ReadSettings()
        {
            Settings = null;

            var package = Package;

            // We might pass through a null state temporarily
            if(package == null)
            {
                DebugLog.Append($"null package in ReadSettings");
                return;
            }

            var settings = await Task.Run(() =>
            {
                return ReadSettingsHelper(package);
            });

            // Ignore the result if the Package has changed while we were away
            if(Package == package)
            {
                Settings = settings;
            }
        }

        /// <summary>
        /// Read the app data settings async
        /// </summary>
        static private IList<PackageSetting> ReadSettingsHelper(PackageModel package)
        {
            // AplicationData.LocalSettings.Value has a tendency to throw out-of-range exceptions
            try
            {
                ApplicationData applicationData = GetApplicationDataFor(package);
                if (applicationData == null)
                {
                    return null;
                }

                var settings = from value in applicationData.LocalSettings.Values
                               orderby value.Key
                               select new PackageSetting() { Name = value.Key, Value = value.Value.ToString() };

                return settings.ToList();
            }
            catch (Exception e)
            {
                DebugLog.Append($"ApplicationData exception in ReadSettings: {e.Message}");
                return null;
            }
        }

        static ApplicationData GetApplicationDataFor(PackageModel package)
        {
            ApplicationData applicationData = null;
            try
            {
                applicationData = ApplicationDataManager.CreateForPackageFamily(package.FamilyName);
            }
            catch (Exception e)
            {
                DebugLog.Append($"ApplicationDataManager.CreateForPackageFamily({package.FamilyName}): {e.Message}");
            }

            return applicationData;
        }

        private async void DeleteSetting(object sender, RoutedEventArgs e)
        {
            ApplicationData applicationData = GetApplicationDataFor(Package);
            if (applicationData == null)
            {
                return;
            }

            var button = sender as Button;
            var name = button.Tag as string;

            var result = await MyMessageBox.Show(
                                name, 
                                title: "Remove?",
                                isOKEnabled: true,
                                closeButtonText: "Cancel");
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            var settingsValues = applicationData.LocalSettings.Values;
            settingsValues.Remove(name);

            // Re-read the settings, which should now not include {name}
            ReadSettings();
        }
    }

    public class PackageSetting
    {
        public string Name { get; set; }
        public string Value { get; set; }

    }
}