using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ViewAppxPackage
{
    /// <summary>
    /// ContentDialog to use as an overlay during an async operation to show progress.
    /// Use IsOpen property to open/close
    /// </summary>
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

        bool _isShowing = false;

    }
}
