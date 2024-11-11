using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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



        public string Value
        {
            get { return (string)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(string), typeof(PackageViewRow), new PropertyMetadata(null));

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
    }
}
