using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ViewAppxPackage
{
    /// <summary>
    /// Use this in a tool tip content to show a tip with a title and a description
    /// </summary>
    public sealed partial class ComplexToolTip : UserControl
    {
        public ComplexToolTip()
        {
            this.InitializeComponent();
        }
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(ComplexToolTip), new PropertyMetadata(null));



        public string Subtitle
        {
            get { return (string)GetValue(SubtitleProperty); }
            set { SetValue(SubtitleProperty, value); }
        }
        public static readonly DependencyProperty SubtitleProperty =
            DependencyProperty.Register("Subtitle", typeof(string), typeof(ComplexToolTip), new PropertyMetadata(null));
    }
}
