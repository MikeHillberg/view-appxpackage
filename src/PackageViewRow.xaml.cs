using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace ViewAppxPackage
{
    /// <summary>
    /// A row in the PackageView
    /// </summary>
    public sealed partial class PackageViewRow : UserControl
    {
        public PackageViewRow()
        {
            this.InitializeComponent();
        }

        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("Label", typeof(string), typeof(PackageViewRow), new PropertyMetadata(null));

        /// <summary>
        /// The value to disply in the second column. This or LinkValue should be set but not both.
        /// </summary>
        public string Value
        {
            get { return (string)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(string), typeof(PackageViewRow), new PropertyMetadata(null));

        /// <summary>
        /// The value to display in the second column as a hyperlink. This or Value should be set but not both.
        /// </summary>
        public string LinkValue
        {
            get { return (string)GetValue(LinkValueProperty); }
            set { SetValue(LinkValueProperty, value); }
        }
        public static readonly DependencyProperty LinkValueProperty =
            DependencyProperty.Register("LinkValue", typeof(string), typeof(PackageViewRow), new PropertyMetadata(null));

        /// <summary>
        /// MinWidth to use for the label column. This keeps the first column all the same width
        /// </summary>
        public double MinLabelWidth
        {
            get { return (double)GetValue(MinLabelWidthProperty); }
            set { SetValue(MinLabelWidthProperty, value); }
        }
        public static readonly DependencyProperty MinLabelWidthProperty =
            DependencyProperty.Register("MinLabelWidth", typeof(double), typeof(PackageViewRow), new PropertyMetadata(0));

        /// <summary>
        /// Keep the global MinLabelWidth updated when any label width changes
        /// </summary>
        private void LabelSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if(_label.ActualWidth > PackageView.Instance.MinLabelWidth)
            {
                PackageView.Instance.MinLabelWidth = _label.ActualWidth;
            }
        }

        /// <summary>
        /// The visibility of the root element; collapses if both Value and LinkValue are empty
        /// </summary>
        Visibility RootVisibility(string value, string linkValue)
        {
            return !string.IsNullOrEmpty(value) || !string.IsNullOrEmpty(linkValue)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Launch Explorer to the folder in LinkValue
        /// </summary>
        private void LinkClick(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            DebugLog.Append($"Opening folder {LinkValue}");
            _ = Launcher.LaunchFolderPathAsync(LinkValue);
        }

        /// <summary>
        /// Copy the LinkValue property to the clipboard
        /// </summary>
        private void CopyLinkValue(object sender, RoutedEventArgs e)
        {
            DataPackage dataPackage = new DataPackage();
            dataPackage.SetText(LinkValue);
            Clipboard.SetContent(dataPackage);

            _copyLinkFlyout.ShowAt(_copyLinkButton);

            DispatcherTimer timer = new();
            timer.Interval = TimeSpan.FromMilliseconds(1500);
            timer.Tick += (s, e) =>
            {
                _copyLinkFlyout.Hide();
                timer.Stop();
            };
            timer.Start();
        }
    }
}
