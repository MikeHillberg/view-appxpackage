using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ViewAppxPackage
{
    public sealed partial class DebugLogViewer : Window
    {
        public DebugLogViewer()
        {
            this.InitializeComponent();

            MainWindow.SetWindowIcon(this);

            Text = DebugLog.GetLog();
        }

        string Text { get; set; }

        static public void Show()
        {
            var viewer = new DebugLogViewer();
            viewer.Activate();
        }
    }
}
