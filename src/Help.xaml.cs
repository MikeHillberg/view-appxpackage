using CommunityToolkit.WinUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ViewAppxPackage
{
    public sealed partial class Help : ContentDialog
    {
        public Help()
        {
            this.InitializeComponent();

            //_markdown.ImageResolving += _markdown_ImageResolving;

            LoadMarkdownAsync();

            Loaded += (s, e) =>
            {
                // XamlRoot is required, but not available until loaded
                XamlRoot = MainWindow.RootElement.XamlRoot;

                // Make the built-in close button go away
                CloseButtonText = "";
            };

        }

        static Help()
        {
            // See if we're supposed to show the Help dialog on startup

            _localSettings = ApplicationData.Current.LocalSettings;
            if (_localSettings.Values.TryGetValue(nameof(ShowHelpOnStartup), out var showHelpOnStartup))
            {
                ShowHelpOnStartup = (bool)showHelpOnStartup;
            }
        }

        static ApplicationDataContainer _localSettings;

        async void LoadMarkdownAsync()
        {
            // bugbug (doc): Doesn't get copied to the output by default, also has a BOM by default
            var uri = new Uri("ms-appx:///Assets/Help.md");

            var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            var openFile = await file.OpenReadAsync();
            var markdown = await openFile.ReadTextAsync(); // Toolkit extension

            _markdown.Text = markdown;
        }

        private void CloseClick(object sender, RoutedEventArgs e)
        {
            this.Hide();
            _localSettings.Values[nameof(ShowHelpOnStartup)] = ShowHelpOnStartup;
        }

        public static bool ShowHelpOnStartup = true;
    }
}
