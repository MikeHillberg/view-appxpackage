using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinRT;
using System.IO;




// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PackageCatalogViewer
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {

            StartGetAllPackagesForUser();

            this.InitializeComponent();
            RootElement = this._root;

            RootElement.Loaded += (s, e) =>
            {
                _filterBox.Focus(FocusState.Programmatic);
            };

            _packageCatalog = PackageCatalog.OpenForCurrentUser();
            _packageCatalog.PackageInstalling += (s, e) => UpdateDeployment(e.IsComplete, e.Package, true);
            _packageCatalog.PackageUninstalling += (s, e) => UpdateDeployment(e.IsComplete, e.Package, false);

            _root.Loaded += (_, __) => SetMicaBackdrop();
            SetWindowIcon();
        }

        internal static FrameworkElement RootElement { get; private set; }

        void UpdateDeployment(bool isComplete, Package package, bool isInstalling)
        {
            if (!isComplete)
            {
                return;
            }

            if (Packages == null)
            {
                // bugbug: race
                return;
            }

            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (isInstalling)
                {
                    _originalpackages.Add(package);
                    _originalpackages = new ObservableCollection<Package>(_originalpackages.OrderBy((p) => p.Id.Name).ToList());
                    FilterPackages();
                }
                else
                {
                    var existing = _originalpackages.FirstOrDefault(p => p.Id.FullName == package.Id.FullName);
                    _originalpackages.Remove(existing);
                    FilterPackages();
                }
            });
        }


        PackageCatalog _packageCatalog;
        ObservableCollection<Package> _originalpackages;
        static ObservableCollection<Package> _packages;
        public ObservableCollection<Package> Packages
        {
            get { return _packages; }
            private set { _packages = value; RaisePropertyChanged(); }
        }



        async void StartGetAllPackagesForUser()
        {
            IEnumerable<Package> packages = null;
            await Task.Run(() =>
            {
                packages = _packageManager.FindPackagesForUser(string.Empty);
                //packages = packages.OrderBy((p) => p.DisplayName).ToList();
                var sorted = from p in packages
                             where !p.IsResourcePackage
                             let name = p.Id.Name // DisplayName throws a lot
                             orderby string.IsNullOrEmpty(name) ? "zzz" : name
                             select p;
                packages = sorted.ToList();
            });

            Debug.WriteLine($"{packages.ToArray()[0].DisplayName}");
            _originalpackages = new ObservableCollection<Package>(packages);
            Packages = _originalpackages;
        }

        PackageManager _packageManager = new PackageManager();

        public event PropertyChangedEventHandler PropertyChanged;

        void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        double _minListWidth = 200;
        double MinListWidth
        {
            get => _minListWidth;
            set
            {
                _minListWidth = value;
                RaisePropertyChanged();
            }
        }

        async private void Remove(object sender, RoutedEventArgs e)
        {
            var package = _lv.SelectedItem as Package;
            if (package == null)
            {
                return;
            }

            var result = await MyMessageBox.Show(package.DisplayName, title: "Remove?", isOKEnabled: true, closeButtonText: "Cancel");
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            _ = _packageManager.RemovePackageAsync(package.Id.FullName);
        }



        string _filter;
        public string Filter
        {
            get { return _filter; }
            set
            {
                _filter = value;
                RaisePropertyChanged();
                FilterPackages();
            }
        }

        void FilterPackages()
        {
            if (Packages == null)
            {
                // bugbug: race
                return;
            }

            if (string.IsNullOrEmpty(_filter))
            {
                Packages = _originalpackages;
                return;
            }

            var updatedFilter = $"^{_filter}$";
            updatedFilter = updatedFilter.Replace("?", ".");
            updatedFilter = updatedFilter.Replace("*", ".*");


            Regex regex = new(updatedFilter, RegexOptions.IgnoreCase);

            var filter = _filter.ToLower();
            var packages = from p in _originalpackages
                               //where p.DisplayName.ToLower().Contains(filter)
                           let matches = regex.Matches(p.Id.Name)
                           where matches.Count > 0
                           select p;
            Packages = new ObservableCollection<Package>(packages);
        }

        private void TestClick(object sender, RoutedEventArgs e)
        {

        }

        private void SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
        {
            if (_lv.SelectedItem == null)
            {
                // bugbug: why isn't the x:Bind doing this?
                _detail.Package = null;
            }
        }

        async private void AddClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);


            picker.FileTypeFilter.Add(".appx");
            picker.FileTypeFilter.Add(".msix");
            var storageFile = await picker.PickSingleFileAsync();
            if (storageFile == null)
            {
                return;
            }

            var fileUri = new Uri(storageFile.Path);

            _ = _packageManager.AddPackageAsync(fileUri, null, DeploymentOptions.None);
        }

        private void ListSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_lv.ActualWidth > MinListWidth)
            {
                MinListWidth = _lv.ActualWidth;
            }
        }

        private void LaunchClick(object sender, RoutedEventArgs e)
        {
            var package = _lv.SelectedItem as Package;
            if (package == null)
            {
                return;
            }

            // shell:AppsFolder\$($(get-appxpackage $app | select PackageFamilyName).PackageFamilyName)!app

            ProcessStartInfo si = new();
            si.FileName = $@"shell:AppsFolder\{package.Id.FamilyName}!app";
            si.UseShellExecute = true;

            try
            {
                Process.Start(si);
            }
            catch (Exception ex)
            {
            }
        }





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
