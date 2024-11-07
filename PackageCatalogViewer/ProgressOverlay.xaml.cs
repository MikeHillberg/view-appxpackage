using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PackageCatalogViewer
{
    public sealed partial class ProgressOverlay : ContentDialog
    {
        public ProgressOverlay()
        {
            this.InitializeComponent();
        }



        public bool IsOpen
        {
            get { return (bool)GetValue(IsOpenProperty); }
            set { SetValue(IsOpenProperty, value); }
        }
        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(
                "IsOpen", typeof(bool), 
                typeof(ProgressOverlay), 
                new PropertyMetadata(false, (d,dp) => (d as ProgressOverlay).IsOpenChanged()));

        bool _isShowing = false;
        void IsOpenChanged()
        {
            if(IsOpen && !_isShowing)
            {
                _ = this.ShowAsync(ContentDialogPlacement.InPlace);
                _isShowing = true;
            }
            else if (!IsOpen && _isShowing)
            {
                _isShowing = false;
                this.Hide();
            }
        }

    }
}
