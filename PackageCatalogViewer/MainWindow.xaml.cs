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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinRT;
using System.IO;
using Windows.System;
using System.IO.Compression;
using Microsoft.Win32;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using System.Security.Principal;
using System.Threading;



namespace PackageCatalogViewer
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {

            ProcessCommandLineArguments();
            StartLoadPackages();

            this.InitializeComponent();

            RootElement = this._root;
            Instance = this;

            RootElement.Loaded += (s, e) =>
            {
                _filterBox.Focus(FocusState.Programmatic);
            };

            _packageCatalog = PackageCatalog.OpenForCurrentUser();

            _packageCatalog.PackageInstalling += (s, e)
                => UpdateDeployment(e.IsComplete, e.Package, true);

            _packageCatalog.PackageUninstalling += (s, e)
                => UpdateDeployment(e.IsComplete, e.Package, false);

            _root.Loaded += (_, __) =>
            {
                SetMicaBackdrop();

                if (_commandLineProvided)
                {
                    _lv.SelectedIndex = 0;
                }
                else
                {
                    Random random = new();
                    _lv.SelectedIndex = random.Next(0, _lv.Items.Count - 1);
                }
            };

            //var ctrlF = new KeyboardAccelerator()
            //{
            //    Key = Windows.System.VirtualKey.F,
            //    Modifiers = Windows.System.VirtualKeyModifiers.Control,
            //};
            //ctrlF.Invoked += GoToFilter;
            //_root.KeyboardAccelerators.Add(ctrlF);

            SetWindowIcon();
            SetWindowTitle();
        }

        bool _isSearchEnabled = false;
        public bool IsSearchEnabled
        {
            get => _isSearchEnabled;
            set
            {
                _isSearchEnabled = value;
                RaisePropertyChanged();
            }
        }

        string SearchPlaceholderText(bool isSearchEnabled)
        {
            return isSearchEnabled
                ? "Search with regex, e.g. myapp or my.*app (Ctrl+E)"
                : "Initializing search ...";
        }

        string FilterPlaceholderText = "Filter with wildcards, e.g. *App* (Ctrl+F)";

        bool? _isElevated = null;
        public bool IsElevated
        {
            get
            {
                if (_isElevated == null)
                {
                    _isElevated = App.IsProcessElevated();
                }

                return _isElevated.Value;
            }
        }

        bool _isAllUsers = false;
        internal bool IsAllUsers
        {
            get => _isAllUsers;
            set
            {
                _isAllUsers = value;
                RaisePropertyChanged();
                StartLoadPackages();
            }
        }


        bool _commandLineProvided = false;
        void ProcessCommandLineArguments()
        {
            var args = Environment.GetCommandLineArgs();
            if (args != null && args.Length > 1)
            {
                _commandLineProvided = true;
                Filter = args[1];
                return;
            }

            if (!Console.IsInputRedirected)
            {
                return;
            }

            using (StreamReader reader = new (Console.OpenStandardInput()))
            {
                List<string> names = new();
                string line;
                while((line = reader.ReadLine()) != null )
                {
                    var parts = line.Split(':');
                    if(parts.Length != 2)
                    {
                        continue;
                    }

                    if (parts[0].Trim() == "PackageFullName")
                    {
                        names.Add(parts[1].Trim());
                    }
                }

                PipeInputPackages = names.ToArray();
            }
        }

        static public string[] PipeInputPackages;

        internal static FrameworkElement RootElement { get; private set; }

        internal static MainWindow Instance;

        PackageModel _currentItem;
        internal PackageModel CurrentItem
        {
            get => _currentItem;
            set
            {
                if (value == null)
                {
                    _currentItem = null;

                    // Ignore the two-way binding where a resource package is selected,
                    // so the SelectedItem goes to null
                    return;
                }

                if (_currentItem != value)
                {
                    _currentItem = value;
                    RaisePropertyChanged();
                }
            }
        }


        void UpdateDeployment(bool isComplete, PackageModel package, bool isInstalling)
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
                    _originalpackages = new ObservableCollection<PackageModel>(_originalpackages.OrderBy((p) => p.Id.Name).ToList());
                    FilterAndSearchPackages();
                }
                else
                {
                    if (CurrentItem == package)
                    {
                        CurrentItem = null;
                    }

                    var existing = _originalpackages.FirstOrDefault(p => p.Id.FullName == package.Id.FullName);
                    _originalpackages.Remove(existing);
                    FilterAndSearchPackages();
                }
            });
        }


        PackageCatalog _packageCatalog;
        ObservableCollection<PackageModel> _originalpackages;
        static ObservableCollection<PackageModel> _packages;
        public ObservableCollection<PackageModel> Packages
        {
            get { return _packages; }
            private set { _packages = value; RaisePropertyChanged(); }
        }



        bool _isMultiSelect = false;
        public bool IsMultiSelect
        {
            get { return _isMultiSelect; }
            set
            {
                _isMultiSelect = value;
                RaisePropertyChanged();
            }
        }


        private bool CanOpenStore(bool isMultiSelect)
        {
            if (isMultiSelect)
            {
                return false;
            }

            return CurrentItem != null && CurrentItem.SignatureKind == PackageSignatureKind.Store;
        }

        private void OpenStore()
        {
            if (IsMultiSelect || CurrentItem == null)
            {
                return;
            }

            var package = CurrentItem;
            if (package.SignatureKind != PackageSignatureKind.Store)
            {
                return;
            }

            _ = Launcher.LaunchUriAsync(new System.Uri($"ms-windows-store://pdp/?PFN={package.Id.FamilyName}"));
        }

        private void OpenManifest()
        {
            if (IsMultiSelect || CurrentItem == null)
            {
                return;
            }

            var path = CurrentItem.InstalledPath;

            // bugbug: I think there's a Win32 API to get this out?
            path = Path.Combine(path, "AppxManifest.xml");
            if (Path.Exists(path))
            {
                ProcessStartInfo psi = new(path);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
        }

        bool CanOpenManifest(bool isMultiSelect)
        {
            if (isMultiSelect)
            {
                return false;
            }

            return true;
        }

        bool CanLaunch(bool isMultiSelect)
        {
            if (isMultiSelect)
            {
                return false;
            }

            return CurrentItem != null
                && !CurrentItem.IsResourcePackage
                && !CurrentItem.IsFramework;
        }

        bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                RaisePropertyChanged();
            }
        }

        async void StartLoadPackages()
        {
            //App.WaitForDebugger();

            IsLoading = true;
            IsSearchEnabled = false;

            var isElevated = App.IsProcessElevated() && IsAllUsers;
            IEnumerable<PackageModel> packageModels = null;

            try
            {
                await Task.Run(() =>
                {
                    Debug.WriteLine("Inside Run");

                    IEnumerable<Package> packages = null;
                    if (isElevated)
                    {
                        packages = _packageManager.FindPackages();

                    }
                    else
                    {
                        packages = _packageManager.FindPackagesForUser(string.Empty);
                    }

                    var sorted = from p in packages
                                 where !p.IsResourcePackage
                                 let name = p.Id.Name // DisplayName throws a lot
                                 orderby string.IsNullOrEmpty(name) ? "zzz" : name
                                 select (PackageModel)p;
                    packageModels = sorted.ToList();

                    _originalpackages = new ObservableCollection<PackageModel>(packageModels);
                });
            }
            finally
            {
                IsLoading = false;
            }

            Packages = _originalpackages;


            if (!string.IsNullOrEmpty(_filter))
            {
                FilterAndSearchPackages();
            }

            PackageModel.StartCalculateDependents(_originalpackages);

            await Task.Run(() =>
            {
                _ = PackageModel.Find("hello world", _originalpackages);
                Debug.WriteLine("Done initializing cache");
            });
            IsSearchEnabled = true;

            return;
        }


        // bugbug
        static internal PackageManager _packageManager = new PackageManager();

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

        async private void RemovePackage(object sender, RoutedEventArgs e)
        {
            var package = _lv.SelectedItem as PackageModel;
            if (package == null)
            {
                return;
            }

            var result = await MyMessageBox.Show(package.DisplayName, title: "Remove?", isOKEnabled: true, closeButtonText: "Cancel");
            if (result != ContentDialogResult.Primary)
            {
                return;
            }


            try
            {
                var removing = _packageManager.RemovePackageAsync(package.Id.FullName);

                // bugbug: is there something to check in the return DeploymentResult?
                _ = await CallWithProgress(removing, "Removing ...");
            }
            catch (Exception e2)
            {
                Debug.WriteLine(e2.Message);
                _ = MyMessageBox.Show(e2.Message, "Failed to remove package", isOKEnabled: true);
            }
        }



        string _filter;
        public string Filter
        {
            get { return _filter; }
            set
            {
                _filter = value;
                RaisePropertyChanged();
                FilterAndSearchPackages();
            }
        }

        string _searchText;
        public string SearchText
        {
            get { return _searchText; }
            set
            {
                _searchText = value;
                RaisePropertyChanged();
                FilterAndSearchPackages();
            }
        }


        void FilterAndSearchPackages()
        {
            if (Packages == null)
            {
                // bugbug: race
                return;
            }

            IEnumerable<PackageModel> packages = _originalpackages;

            if (!string.IsNullOrEmpty(_filter))
            {
                var updatedFilter = $"^{_filter.Trim()}$";
                updatedFilter = updatedFilter.Replace("?", ".");
                updatedFilter = updatedFilter.Replace("*", ".*");


                Regex regex = new(updatedFilter, RegexOptions.IgnoreCase);

                packages = from p in packages
                           let matches = regex.Matches(p.Id.Name)
                           where matches.Count > 0
                           select (PackageModel)p;
            }

            if (!string.IsNullOrEmpty(_searchText))
            {
                packages = PackageModel.Find(_searchText, packages);
            }

            Packages = new ObservableCollection<PackageModel>(packages);
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

            IsMultiSelect = _lv.SelectedItems != null && _lv.SelectedItems.Count > 1;
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
            var package = _lv.SelectedItem as PackageModel;
            if (package == null)
            {
                return;
            }

            // shell:AppsFolder\$($(get-appxpackage $app | select PackageFamilyName).PackageFamilyName)!app

            ProcessStartInfo si = new();
            si.FileName = $@"shell:AppsFolder\{package.FamilyName}!app";
            si.UseShellExecute = true;

            try
            {
                Process.Start(si);
            }
            catch (Exception)
            {
            }
        }

        internal void SelectPackage(PackageModel package)
        {

            var matchingPackage = Packages.FirstOrDefault(p => p.Id.FullName == package.Id.FullName);
            if (matchingPackage != null)
            {
                _lv.ScrollIntoView(package); // bugbug: why isn't this automatic?
            }
            else
            {
                _lv.SelectedIndex = -1;
            }

            CurrentItem = package;
        }




        private void GoToFilter(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            _filterBox.Focus(FocusState.Keyboard);
        }
        private void GoToSearch(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            _searchBox.Focus(FocusState.Keyboard);
        }

        private async void AddUnsigned(object sender, RoutedEventArgs e)
        {
            if (!IsDeveloperModeEnabled())
            {
                _ = await MyMessageBox.Show(
                                 "Developer mode must be enabled in Settings to add an unsigned package",
                                 "Developer mode required",
                                 isOKEnabled: true,
                                 isCancelEnabled: false);
                return;
            }

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
            var path = storageFile.Path;
            var directoryPath = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
            var directoryPathT = directoryPath;
            int i = 1;
            while (Path.Exists(directoryPathT))
            {
                directoryPathT = $"{directoryPath}.{i++}";
            }
            directoryPath = directoryPathT;

            var result = await MyMessageBox.Show(
                                "Package will be expanded to the following directory\n\n" +
                                "Note that this directory will remain even after the package is removed\n\n" +
                                $"{directoryPath}",
                                "Expanding package",
                                isOKEnabled: true,
                                closeButtonText: "Cancel");
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            ZipFile.ExtractToDirectory(path, directoryPath);

            var manifestPath = Path.Combine(directoryPath, "AppxManifest.xml");
            if (!Path.Exists(manifestPath))
            {
                _ = await MyMessageBox.Show(
                                 "No AppxManifest.xml found in the package",
                                 "Invalid package",
                                 isOKEnabled: true);
                return;
            }

            var manifestUri = new Uri(manifestPath);
            RegisterPackageOptions options = new()
            {
                DeveloperMode = true
            };

            // bugbug: update the docs
            // Requires <rescap:Capability Name="packageManagement"/>
            // or results in
            // "The filename, directory name, or volume label syntax is incorrect."
            var registering = _packageManager.RegisterPackageByUriAsync(manifestUri, options);

            var addResult = await CallWithProgress(registering, "Adding ...");
            if (!addResult.IsRegistered)
            {
                _ = MyMessageBox.Show($"{addResult.ErrorText}", "Failed to add package", isOKEnabled: true);
            }
        }

        private async Task<DeploymentResult> CallWithProgress(
            IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress> operation,
            string message)
        {
            BusyMessage = message;
            try
            {
                operation.Progress = (s, e) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        // bugbug: throws a non-sensical exception on RangeBase.Value if you set this from the wrong thread
                        ProgressPercentage = (int)e.percentage;
                    });
                };

                return await operation;
            }
            finally
            {
                // bugbug: setting to null doesn't trigger the OneWay binding
                BusyMessage = "";
                ProgressPercentage = 0;
            }
        }

        string _busyMessage;
        string BusyMessage
        {
            get => _busyMessage;
            set
            {
                _busyMessage = value;
                RaisePropertyChanged();
            }
        }


        double _progressPercentage = 0;
        double ProgressPercentage
        {
            get => _progressPercentage;
            set
            {
                _progressPercentage = value;
                RaisePropertyChanged();
            }
        }

        Visibility CalculateVisibility(string s)
        {
            return string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;
        }

        private bool IsDeveloperModeEnabled()
        {
            string developerModeKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock";
            string developerModeValueName = "AllowDevelopmentWithoutDevLicense";
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(developerModeKeyPath))
                {
                    if (key != null)
                    {
                        object devModeValue = key.GetValue(developerModeValueName);
                        if (devModeValue != null && (int)devModeValue == 1)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return false;


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
