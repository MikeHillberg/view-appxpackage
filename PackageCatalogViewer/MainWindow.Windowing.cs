﻿using Microsoft.UI.Xaml;
using Microsoft.UI;
using System.Threading.Tasks;
using WinRT.Interop;
using System.IO;
using WinRT;

namespace PackageCatalogViewer
{
    public partial class MainWindow
    {
        // Helpers for SetMicaBackrop
        WindowsSystemDispatcherQueueHelper m_wsdqHelper;
        Microsoft.UI.Composition.SystemBackdrops.MicaController m_micaController;
        Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration m_configurationSource;

        bool _isMicaSet = false;


        /// <summary>
        /// Set an ICO to the AppWindow
        /// </summary>
        async void SetWindowIcon()
        {
            // This call is really slow, so don't wait on it
            var installedPath = await Task.Run<string>(() => Windows.ApplicationModel.Package.Current.InstalledLocation.Path);

            // Get the AppWindow
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            // Set the icon
            // Used https://icoconvert.com
            appWindow.SetIcon(Path.Combine(installedPath, "Assets/Icon.ico"));
        }

        /// <summary>
        /// Set Mica as the Window backdrop, if possible
        /// </summary>
        internal void SetMicaBackdrop()
        {
            // With this set, portion of the Window content that isn't opaque will see
            // Mica. So the search results pane is transparent, allowing this to show through.
            // On Win10 this isn't supported, so the background will just be the default backstop

            // Gets called by Loaded, running twice isn't good
            if (_isMicaSet)
            {
                return;
            }
            _isMicaSet = true;

            // Not supported on Win10
            if (!Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                return;
            }


            // Rest of the code is copied from WinUI Gallery
            // https://github.com/microsoft/WinUI-Gallery/blob/260cb720ef83b3d134bc4805cffcfac9461dce33/WinUIGallery/SamplePages/SampleSystemBackdropsWindow.xaml.cs


            m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
            m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

            // Hooking up the policy object
            m_configurationSource = new Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration();
            this.Activated += Window_Activated;
            this.Closed += Window_Closed;
            ((FrameworkElement)this.Content).ActualThemeChanged += Window_ThemeChanged;

            // Initial configuration state.
            m_configurationSource.IsInputActive = true;
            SetConfigurationSourceTheme();

            m_micaController = new Microsoft.UI.Composition.SystemBackdrops.MicaController();

            // Enable the system backdrop.
            m_micaController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
            m_micaController.SetSystemBackdropConfiguration(m_configurationSource);
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Make sure any Mica/Acrylic controller is disposed so it doesn't try to
            // use this closed window.
            if (m_micaController != null)
            {
                m_micaController.Dispose();
                m_micaController = null;
            }
            this.Activated -= Window_Activated;
            m_configurationSource = null;
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (m_configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            switch (((FrameworkElement)this.Content).ActualTheme)
            {
                case ElementTheme.Dark: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Dark; break;
                case ElementTheme.Light: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Light; break;
                case ElementTheme.Default: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Default; break;
            }
        }

        void SetWindowTitle()
        {
            var title = "view-appxpackage";

            title += App.IsProcessElevated() ? " (all users)" : " (current user)";

            Title = title;
        }
    }
}
