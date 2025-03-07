﻿using Microsoft.UI.Xaml;
using Microsoft.UI;
using System.Threading.Tasks;
using WinRT.Interop;
using System.IO;
using WinRT;
using System.Runtime.InteropServices;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using System;

namespace ViewAppxPackage
{
    // Partial class of MainWindow with the Mica and Window chrome handling

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
        internal static async void SetWindowIcon(Window window)
        {
            // This call is really slow, so don't wait on it
            var installedPath = await Task.Run<string>(() => Windows.ApplicationModel.Package.Current.InstalledLocation.Path);

            // Get the AppWindow
            var hwnd = WindowNative.GetWindowHandle(window);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            // Set the icon
            // Used https://icoconvert.com
            appWindow.SetIcon(Path.Combine(installedPath, "Assets/Icon.ico"));
        }

        /// <summary>
        /// Set Window.Title, including if we're showing current vs all users
        /// </summary>
        void SetWindowTitle()
        {
            var title = "view-appxpackage ";
            title += IsAllUsers ? "(all users)" : "(current user)";
            Title = title;
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

        /// <summary>
        /// Set a number as a badge on the app icon in the task bar, indicating the number of new packages
        /// </summary>
        public void SetBadgeNumber(int count)
        {
            XmlDocument badgeXml;
            try
            {
                // Seeing in telemetry that this is failing sometimes,
                // but haven't gotten enough data to figure out why

                // Get the blank badge XML payload for a badge number
                badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
            }
            catch (Exception ex)
            {
                DebugLog.Append($"Exception on BadgeUpdateManager.GetTempalteContent: {ex.Message}");
                return;
            }


            // Set the value of the badge in the XML to the count of new packages
            XmlElement badgeElement = badgeXml.SelectSingleNode("/badge") as XmlElement;
            badgeElement.SetAttribute("value", count.ToString());

            // Create the badge notification
            BadgeNotification badge = new BadgeNotification(badgeXml);

            // Create the badge updater for the application
            var badgeUpdater = BadgeUpdateManager.CreateBadgeUpdaterForApplication();

            // And update the badge
            badgeUpdater.Update(badge);
        }

    }


    /// <summary>
    /// Helper for use with SetMicaBackrop
    /// </summary>
    /// 
    class WindowsSystemDispatcherQueueHelper
    {
        // This class opied from WinUI Gallery
        // https://github.com/microsoft/WinUI-Gallery/blob/260cb720ef83b3d134bc4805cffcfac9461dce33/WinUIGallery/SamplePages/SampleSystemBackdropsWindow.xaml.cs


        [StructLayout(LayoutKind.Sequential)]
        struct DispatcherQueueOptions
        {
            internal int dwSize;
            internal int threadType;
            internal int apartmentType;
        }

        [DllImport("CoreMessaging.dll")]
        private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object dispatcherQueueController);

        object m_dispatcherQueueController = null;
        public void EnsureWindowsSystemDispatcherQueueController()
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {
                // one already exists, so we'll just use it.
                return;
            }

            if (m_dispatcherQueueController == null)
            {
                DispatcherQueueOptions options;
                options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
                options.threadType = 2;    // DQTYPE_THREAD_CURRENT
                options.apartmentType = 2; // DQTAT_COM_STA

                CreateDispatcherQueueController(options, ref m_dispatcherQueueController);
            }
        }



    }



}
