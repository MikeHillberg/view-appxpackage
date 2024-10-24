using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace PackageCatalogViewer
{
    // Simple wrapper to show a traditional message box
    internal class MyMessageBox
    {
        internal static async Task<ContentDialogResult> Show(
            string message,
            string title = null,
            string closeButtonText = null,
            bool isOKEnabled = false,
            bool isCancelEnabled = false)
        {
            var contentDialog = new ContentDialog()
            {
                XamlRoot = MainWindow.RootElement.XamlRoot,
                Content = new StackPanel()
                {
                    Children =
                    {
                        new TextBlock() { Text = message }
                    }
                },
                Title = title
            };

            if (isOKEnabled)
            {
                contentDialog.PrimaryButtonText = "OK";
            }

            if (isCancelEnabled)
            {
                contentDialog.SecondaryButtonText = "Cancel";
            }

            if (closeButtonText == null)
            {
                contentDialog.CloseButtonText = "Close";
            }
            else
            {
                contentDialog.CloseButtonText = closeButtonText;
            }

            try
            {
                var result = await contentDialog.ShowAsync();
                return result;
            }
            catch (Exception)
            {
                // Ignore the COM exception we get when trying to open two ContentDialogs at the same time
                return ContentDialogResult.None;
            }
        }
    }
}
