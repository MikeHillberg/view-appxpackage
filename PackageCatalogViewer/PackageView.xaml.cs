using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;

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

        public Package Package
        {
            get { return (Package)GetValue(PackageProperty); }
            set { SetValue(PackageProperty, value); }
        }
        public static readonly DependencyProperty PackageProperty =
            DependencyProperty.Register("Package", typeof(Package), typeof(PackageView), 
                new PropertyMetadata(null, (d,dp) => (d as PackageView).PackageChanged()));

        private void PackageChanged()
        {
            if(Package == null)
            {
                // bugbug: this should be happening already with the x:Bind
                _root.Visibility = Visibility.Collapsed;
            }
        }

        Visibility NotEmpty(Package package)
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

        private void OpenManifest(object sender, RoutedEventArgs e)
        {
            if(Package == null)
            {
                Debug.Assert(false);
                return;
            }

            var path = Package.InstalledPath;
            path = Path.Combine(path, "AppxManifest.xml");
            if (Path.Exists(path))
            {
                ProcessStartInfo psi = new(path);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }

        }
    }

    public class PackageModel
    {
        private Package _package;

        public PackageModel(Package package)
        {
            _package = package;
        }

        public string DisplayName
        {
            get
            {
                try
                {
                    return _package.DisplayName;
                }
                catch (Exception)
                {
                    return Path.GetFileName(_package.InstalledPath);
                }
            }
        }

        internal static IEnumerable<PackageModel> Wrap(IEnumerable<Package> packages)
        {
            return from p in packages
                   select new PackageModel(p);
        }
    }
}
