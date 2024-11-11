using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.ApplicationModel;


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

        //Visibility CollapseIfEmpty(object value)
        //{
        //    if(value == null)
        //    {
        //        return Visibility.Collapsed;
        //    }

        //    if(value is IEnumerable<object> enumerable)
        //    {
        //        if (!enumerable.Any())
        //        {
        //            return Visibility.Collapsed;
        //        }
        //    }

        //    return Visibility.Visible;
        //}

        //static string SafeDisplayName(object value)
        //{
        //    var package = value as Package;
        //    try
        //    {
        //        return package.DisplayName;
        //    }
        //    catch(Exception)
        //    {
        //        return package.InstalledPath;
        //    }
        //}


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

    }

}
