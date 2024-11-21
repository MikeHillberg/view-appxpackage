using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
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
using System.IO;
using Windows.System;
using System.IO.Compression;
using Microsoft.Win32;
using Windows.Foundation;
using Microsoft.UI.Xaml.Input;
using System.Collections.ObjectModel;



namespace ViewAppxPackage
{
    public sealed partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            ProcessCommandLineArguments();
            StartLoadPackages();

            this.InitializeComponent();

            RootElement = _root;
            _root.Loaded += OnLoaded;

            Instance = this;

            _packageCatalog = PackageCatalog.OpenForCurrentUser();

            _packageCatalog.PackageInstalling += (s, e)
                => OnCatalogUpdate(e.IsComplete, e.Package, PackageUpdateNotification.Install);

            _packageCatalog.PackageUninstalling += (s, e)
                => OnCatalogUpdate(e.IsComplete, e.Package, PackageUpdateNotification.Uninstall);

            _packageCatalog.PackageUpdating += (s, e) 
                => OnCatalogUpdate(e.IsComplete, e.TargetPackage, PackageUpdateNotification.Update);

            _packageCatalog.PackageStatusChanged += (s, e)
                => OnCatalogUpdate(true, e.Package, PackageUpdateNotification.Status);

            SetWindowIcon();
            SetWindowTitle();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SetMicaBackdrop();

            _filterBox.Focus(FocusState.Programmatic);

            if (_commandLineProvided)
            {
                _lv.SelectedIndex = 0;
            }

            if (Help.ShowHelpOnStartup)
            {
                ShowHelp();
            }
        }


        void ShowHelp()
        {
            Help help = new()
            {
                XamlRoot = _lv.XamlRoot,
                CloseButtonText = "Close"
            };
            _ = help.ShowAsync();
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

        public bool IsElevated => App.IsProcessElevated();


        /// <summary>
        /// Indicates if in all-users mode (vs current user only mode)
        /// </summary>
        internal bool IsAllUsers
        {
            get => _isAllUsers;
            set
            {
                _isAllUsers = value;
                RaisePropertyChanged();

                // Need to re-load packages
                StartLoadPackages();

                // Show what mode we're in in the window title
                SetWindowTitle();
            }
        }
        bool _isAllUsers = false;


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

            // Read input from the console, which will have content if this was launched
            // from a PowerShell pipe.
            // The pipe input comes in just like the output of get-appxpackage, so we're looking for e.g.
            //
            //   PackageFamilyName : view-appxpackage_9exbdrchsqpwm

            using (StreamReader reader = new(Console.OpenStandardInput()))
            {
                List<string> names = new();
                string line;
                var found = false;
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split(':');
                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    if (parts[0].Trim() == "PackageFullName")
                    {
                        found = true;
                        names.Add(parts[1].Trim());
                    }
                }

                if (found)
                {
                    PipeInputPackages = names.ToArray();

                    // Set the filter box to "$input" indicating we should use this list of names
                    _filter = PipeInputFilterString;
                }
            }
        }

        /// <summary>
        /// List of package names that we got from stdin
        /// </summary>
        static public string[] PipeInputPackages = null;

        /// <summary>
        /// Magic value for the filter string that means we're using the input from the stdin
        /// </summary>
        static public string PipeInputFilterString = "$input";

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

                    DebugLog.Append("CurrentItem is null");

                    // Ignore the two-way binding where a resource package is selected,
                    // so the SelectedItem goes to null
                    return;
                }

                if (_currentItem != value)
                {
                    DebugLog.Append($"CurrentItem is {value.Id.Name}");

                    _currentItem = value;
                    RaisePropertyChanged();
                }
            }
        }

        enum PackageUpdateNotification
        {
            Install,
            Uninstall,
            Update,
            Status
        }


        /// <summary>
        /// Update after receiving a notification that a package has been added or removed
        /// </summary>
        void OnCatalogUpdate(bool isComplete, Package wamPackage, PackageUpdateNotification updateKind)
        {
            // bugbug: there's a race condition where packages are being installed/removed
            // while we're in the middle of package enumeration

            if (!isComplete)
            {
                return;
            }

            if(wamPackage.IsBundle)
            {
                // I don't understand bundles yet. When a package is added, two install notifications
                // come in. One is a normal package that will show up again on enumeration.
                // The other is a bundle that doesn't, and that throws a lot of exceptions
                return;
            }

            DebugLog.Append($"Catalog update: {updateKind}, {wamPackage.Id.Name}, {wamPackage.InstalledPath}");

            if (Packages == null)
            {
                // bugbug: seems like a race condition where the package catalog is updated
                // while we're enumerating it
                return;
            }

            this.DispatcherQueue.TryEnqueue(() =>
            {
                var package = PackageModel.FromWamPackage(wamPackage);

                if (updateKind == PackageUpdateNotification.Install)
                {
                    // Shouldn't be necessary but playing it safe
                    RemoveFromCache(package);

                    _originalpackages.Add(package);
                    _originalpackages = new(_originalpackages.OrderBy((p) => p.Id.Name).ToList());
                }
                else if(updateKind == PackageUpdateNotification.Uninstall)
                {
                    if (CurrentItem == package)
                    {
                        CurrentItem = null;
                    }

                    RemoveFromCache(package);
                }
                else if(updateKind == PackageUpdateNotification.Update)
                {
                    RemoveFromCache(package);

                    // Get a new model wrapper
                    package = PackageModel.FromWamPackage(wamPackage);

                    _originalpackages.Add(package);
                    _originalpackages = new(_originalpackages.OrderBy((p) => p.Id.Name).ToList());
                }
                else if(updateKind == PackageUpdateNotification.Status)
                {
                    package.UpdateStatus();
                }
                else
                {
                    Debug.Assert(false);
                }

                FilterAndSearchPackages();
                RaisePropertyChanged(nameof(NoPackagesFound));
            });
        }


        void RemoveFromCache(PackageModel package)
        {
            var existing = _originalpackages.FirstOrDefault(p => p.Id.FullName == package.Id.FullName);
            if(existing != null)
            {
                _originalpackages.Remove(existing);
                DebugLog.Append($"Removed {package.Id.Name} from cache");
            }
            else
            {
                DebugLog.Append($"Didn't remove {package.Id.Name} from cache");
            }

            PackageModel.ClearCache(package);
        }


        PackageCatalog _packageCatalog;

        ObservableCollection<PackageModel> _originalpackages;
        static ObservableCollection<PackageModel> _packages;
        public ObservableCollection<PackageModel> Packages
        {
            get { return _packages; }
            private set
            {
                _packages = value;

                RaisePropertyChanged();
                RaisePropertyChanged(nameof(NoPackagesFound));

                // This has to be after raising property changes so that the ListView is updated
                EnsureItemSelected();
            }
        }

        bool NoPackagesFound => Packages == null || Packages.Count == 0;



        /// <summary>
        /// True if SelectedItems.Count > 1
        /// </summary>
        public bool IsMultiSelect
        {
            get { return _isMultiSelect; }
            set
            {
                _isMultiSelect = value;
                RaisePropertyChanged();
            }
        }
        bool _isMultiSelect = false;

        /// <summary>
        /// True if the current package is in the Store
        /// </summary>
        private bool CanOpenStore(bool isMultiSelect)
        {
            if (isMultiSelect)
            {
                return false;
            }

            return CurrentItem != null && CurrentItem.SignatureKind == PackageSignatureKind.Store;
        }

        /// <summary>
        /// Open the Store to this package
        /// </summary>
        private void OpenStore()
        {
            if (!CanOpenStore(IsMultiSelect))
            {
                Debug.Assert(false);
                return;
            }

            _ = Launcher.LaunchUriAsync(new System.Uri($"ms-windows-store://pdp/?PFN={CurrentItem.Id.FamilyName}"));
        }

        private void OpenManifest()
        {
            Debug.Assert(CanOpenManifest(IsMultiSelect));

            var path = CurrentItem.InstalledPath;

            // bugbug: use https://learn.microsoft.com/windows/win32/api/appxpackaging/nf-appxpackaging-iappxpackagereader-getmanifest
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

            if( CurrentItem == null)
            {
                return false;
            }

            return !string.IsNullOrEmpty(CurrentItem.Aumids);
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
            IsLoading = true;

            // After loading is complete we need some time to get search cached
            IsSearchEnabled = false;

            var isAllUsers = App.IsProcessElevated() && IsAllUsers;
            IEnumerable<PackageModel> packageModels = null;

            try
            {
                await Task.Run(() =>
                {
                    IEnumerable<Package> packages = null;
                    if (isAllUsers)
                    {
                        packages = PackageManager.FindPackages();

                    }
                    else
                    {
                        packages = PackageManager.FindPackagesForUser(string.Empty);
                    }

                    var sorted = from p in packages
                                 where !p.IsResourcePackage
                                 let name = p.Id.Name // DisplayName throws a lot
                                 orderby name
                                 select PackageModel.FromWamPackage(p);
                    packageModels = sorted.ToList();

                    _originalpackages = new(packageModels);
                });
            }
            finally
            {
                IsLoading = false;
            }

            Packages = _originalpackages;

            // Process Packages with the filter & search text boxes
            FilterAndSearchPackages();

            PackageModel.StartCalculateDependents(_originalpackages);

            // Call Find on a background thread to warm the caches
            await Task.Run(() =>
            {
                // Make a copy in case packages are added/deleted during the Find
                List<PackageModel> packagesCopy = new(_originalpackages);
                _ = PackageModel.FindPackages("hello world", packagesCopy);

                Debug.WriteLine("Done initializing cache");
            });

            // After that FindPackages, search will be fast now
            IsSearchEnabled = true;

            return;
        }


        void EnsureItemSelected()
        {
            if (_lv.Items.Count == 0)
            {
                return;
            }

            // Pick an item at random from the packages (after filtering and searching)
            Random random = new();
            var index = random.Next(0, _lv.Items.Count - 1);
            SelectPackage(_lv.Items[index] as PackageModel);
        }

        static internal PackageManager PackageManager = new PackageManager();

        public event PropertyChangedEventHandler PropertyChanged;

        void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// MinWidth for the list column, based on calculated width of the widest item
        /// </summary>
        double MinListWidth
        {
            get => _minListWidth;
            set
            {
                _minListWidth = value;
                RaisePropertyChanged();
            }
        }
        double _minListWidth = 200;

        /// <summary>
        /// Remove an appx package
        /// </summary>
        async private void RemovePackage(object sender, RoutedEventArgs e)
        {
            var package = _lv.SelectedItem as PackageModel;
            if (package == null)
            {
                Debug.Assert(false);
                return;
            }

            // Get confirmation
            var result = await MyMessageBox.Show(
                                package.DisplayName, title: "Remove?", 
                                isOKEnabled: true, 
                                closeButtonText: "Cancel");
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress> removing;
                if (IsAllUsers)
                {
                    removing = PackageManager.RemovePackageAsync(package.Id.FullName, RemovalOptions.RemoveForAllUsers);
                }
                else
                {
                    removing = PackageManager.RemovePackageAsync(package.Id.FullName);
                }

                // bugbug: is there something to check in the return DeploymentResult?
                // bugbug: not seeing progress notifications
                _ = await CallWithProgress(removing, "Removing ...");
            }
            catch (Exception e2)
            {
                Debug.WriteLine(e2.Message);
                _ = MyMessageBox.Show(e2.Message, "Failed to remove package", isOKEnabled: true);
            }
        }


        /// <summary>
        /// Package name filter, e.g. *App*
        /// </summary>
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
        string _filter;


        /// <summary>
        ///  Search text, regex form
        /// </summary>
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
        string _searchText;


        /// <summary>
        /// Use the filter and search text to reduce the _originalPackages list into Packages
        /// </summary>
        void FilterAndSearchPackages()
        {
            if (Packages == null)
            {
                return;
            }

            IEnumerable<PackageModel> packages = _originalpackages;

            if (!string.IsNullOrEmpty(_filter))
            {
                // Convert wildcard syntax into regex
                var updatedFilter = $"^{_filter.Trim()}$";
                updatedFilter = updatedFilter.Replace("?", ".");
                updatedFilter = updatedFilter.Replace("*", ".*");

                Regex filterRegex = new(updatedFilter, RegexOptions.IgnoreCase);

                List<PackageModel> filteredPackages = new();

                // If the filter is "$input", then we're using the list of names from stdin, which is stored in PipeInputPackages
                var isInput = _filter.Trim() == MainWindow.PipeInputFilterString;

                foreach (var p in packages)
                {
                    if (isInput)
                    {
                        if (PipeInputPackages.Contains(p.Id.FullName))
                        {
                            filteredPackages.Add(p);
                        }
                    }
                    else
                    {
                        var matches = filterRegex.Matches(p.Id.Name);
                        if (matches.Count > 0)
                        {
                            filteredPackages.Add(p);
                        }
                    }
                }

                packages = filteredPackages;
            }

            if (!string.IsNullOrEmpty(_searchText))
            {
                packages = PackageModel.FindPackages(_searchText, packages);
            }

            Packages = new(packages);
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

        async private void AddPackage(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);

            // bugbug: handle bundles
            picker.FileTypeFilter.Add(".appx");
            picker.FileTypeFilter.Add(".msix");
            var storageFile = await picker.PickSingleFileAsync();
            if (storageFile == null)
            {
                return;
            }

            var fileUri = new Uri(storageFile.Path);

            try
            {
                var adding = PackageManager.AddPackageAsync(fileUri, null, DeploymentOptions.None);
                _ = await CallWithProgress(adding, "Adding ...");

            }
            catch (Exception e2)
            {
                var message = e2.Message;

                if(e2 is COMException comException && (uint)comException.HResult == 0x800b0100)
                {
                    message += "\n\nNote: you can use the Register button to add an unsigned package";
                }

                _ = MyMessageBox.Show(message, "Failed to add package");
            }
        }


        private void ListSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Keep track of the max width of the List we've seen.
            // This is then set as the MinWidth so that the list doesn't wiggle
            if (_lv.ActualWidth > MinListWidth)
            {
                MinListWidth = _lv.ActualWidth;
            }
        }

        /// <summary>
        /// Launch an app from its package
        /// </summary>
        private void LaunchPackage(object sender, RoutedEventArgs e)
        {
            var package = _lv.SelectedItem as PackageModel;
            if (package == null)
            {
                Debug.Assert(false);
                return;
            }

            // bugbug: handle the case of a package with multiple Applications (multiple Aumids)
            var firstAumid = package.Aumids.Split(' ').FirstOrDefault().Trim();

            ProcessStartInfo si = new();
            si.FileName = $@"shell:AppsFolder\{firstAumid}";
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

        internal void SelectPackage(int index)
        {
            var package = _lv.Items[index] as PackageModel;
            _lv.ScrollIntoView(index); // bugbug: why isn't this automatic?
            CurrentItem = package;
        }



        private void GoToFilter(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            _filterBox.Focus(FocusState.Keyboard);
        }

        private void GoToSearch(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            _searchBox.Focus(FocusState.Keyboard);
        }

        /// <summary>
        /// Register a package, either from an appxmanifest.xml or from an appx/msix
        /// </summary>
        private async void RegisterPackage(object sender, RoutedEventArgs e)
        {
            if (!IsDeveloperModeEnabled())
            {
                _ = await MyMessageBox.Show(
                                 "Developer Mode must be enabled in Settings to add an unsigned package",
                                 "Developer Mode required",
                                 isOKEnabled: true,
                                 isCancelEnabled: false);
                // bugbug: link to ms-settings:developers

                return;
            }

            var picker = new FileOpenPicker();
            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);

            picker.FileTypeFilter.Add(".appx");
            picker.FileTypeFilter.Add(".msix");
            picker.FileTypeFilter.Add(".xml");

            var storageFile = await picker.PickSingleFileAsync();
            if (storageFile == null)
            {
                // Canceled
                return;
            }
            var path = storageFile.Path;
            string manifestPath = null;

            if (Path.GetExtension(path) == ".xml")
            {
                manifestPath = path;
            }
            else
            {
                // Expand the package zip file to a directory
                // If the package is p.msix, expand to a directory named p, p1, p2, ... whatever's available

                var directoryPath = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
                var directoryPathCheck = directoryPath;

                // Loop with a counter until we find a directory name not in use
                int i = 1;
                while (Path.Exists(directoryPathCheck))
                {
                    directoryPathCheck = $"{directoryPath}.{i++}";
                }
                directoryPath = directoryPathCheck;

                // Get confirmation
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

                manifestPath = Path.Combine(directoryPath, "AppxManifest.xml");
            }

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

            try
            {
                // bugbug: update the docs
                // Requires <rescap:Capability Name="packageManagement"/>
                // or results in
                // "The filename, directory name, or volume label syntax is incorrect."
                var registering = PackageManager.RegisterPackageByUriAsync(manifestUri, options);

                var addResult = await CallWithProgress(registering, "Adding ...");
                if (!addResult.IsRegistered)
                {
                    _ = MyMessageBox.Show($"{addResult.ErrorText}", "Failed to add package", isOKEnabled: true);
                }
            }
            catch (Exception e2)
            {
                _ = MyMessageBox.Show(e2.Message, "Failed to add package", isOKEnabled: true);
            }
        }

        /// <summary>
        /// Handle progress notifications on an IAsyncWithProgress
        /// </summary>
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

        /// <summary>
        /// Setting this causes a progress overlay message to show to the user
        /// </summary>
        string BusyMessage
        {
            get => _busyMessage;
            set
            {
                _busyMessage = value;
                RaisePropertyChanged();
            }
        }
        string _busyMessage;


        /// <summary>
        /// Drives a progress bar
        /// </summary>
        double ProgressPercentage
        {
            get => _progressPercentage;
            set
            {
                _progressPercentage = value;
                RaisePropertyChanged();
            }
        }
        double _progressPercentage = 0;

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

        private void ShowHelpClick(object sender, RoutedEventArgs e)
        {
            ShowHelp();
        }

        private void ShowDebugLog(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            DebugLogViewer.Show();
        }
    }
    
}
