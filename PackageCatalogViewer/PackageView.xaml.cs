using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Windows.ApplicationModel;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PackageCatalogViewer
{
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
                new PropertyMetadata(null, (d,dp) => (d as PackageView).PackageChanged()));

        private void PackageChanged()
        {
            if(Package == null)
            {
                // bugbug: this should be happening already with the x:Bind
                _root.Visibility = Visibility.Collapsed;
            }
        }

        Visibility NotEmpty(PackageModel package)
        {
            return package == null ? Visibility.Collapsed : Visibility.Visible;
        }



        public double MinLabelWidth
        {
            get { return (double)GetValue(MinLabelWidthProperty); }
            set { SetValue(MinLabelWidthProperty, value); }
        }
        public static readonly DependencyProperty MinLabelWidthProperty =
            DependencyProperty.Register("MinLabelWidth", typeof(double), typeof(PackageView), new PropertyMetadata(0.0));

        Visibility CollapseIfEmpty(object value)
        {
            if(value == null)
            {
                return Visibility.Collapsed;
            }

            if(value is IEnumerable<object> enumerable)
            {
                if (!enumerable.Any())
                {
                    return Visibility.Collapsed;
                }
            }

            return Visibility.Visible;
        }

        static string SafeDisplayName(object value)
        {
            var package = value as Package;
            try
            {
                return package.DisplayName;
            }
            catch(Exception)
            {
                return package.InstalledPath;
            }
        }


        private void DependencyClicked(PackageModel package)
        {

        }

        private void GoToPackage(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            var package = GetMyTag(sender) as PackageModel;
            MainWindow.Instance.SelectPackage(package);
        }

        string GetTrueBooleans(PackageModel package)
        {
            return PackageModel.GetTrueBooleans(package);
        }


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

        //private void OpenStore(object sender, RoutedEventArgs e)
        //{
        //    if (Package == null)
        //    {
        //        Debug.Assert(false);
        //        return;
        //    }

        //    Launcher.LaunchUriAsync(new System.Uri($"ms-windows-store://pdp/?PFN={Package.Id.FamilyName}"));
        //}
    }

}
