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
using System.IO.Compression;
using Microsoft.Win32;
using Windows.Foundation;
using Microsoft.UI.Xaml.Input;
using System.Collections.ObjectModel;
using Windows.Storage;
using System.Threading;
using Microsoft.UI.Dispatching;


namespace ViewAppxPackage
{
    public sealed partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            Instance = this;
            MyThreading.SetUIThread(Thread.CurrentThread);

            ProcessCommandLineArguments();

            // Need this before we start loading so that we know how/what to load
            LoadSettings();

            // bugbug: there's a race where we load the initial set here but
            // haven't set up the change event listeners yet
            StartLoadPackages();

            this.InitializeComponent();

            _xClassMapper.XClass = this;

            RootElement = _root;
            _root.Loaded += OnLoaded;

            SetWindowIcon(this);
            SetWindowTitle();
            SetUpBadging();

            // Track if we're shutting the thread down so that we don't post new work to it
            DispatcherQueue.ShutdownStarting += (s, e) =>
            {
                IsShuttingDown = true;
            };

            Closed += (s,e) =>
            {
                _logViewerWindow?.Close();
            };

            CreateSettings();
        }

        // Dialog that shows until we have the bare minimum loaded
        ContentDialog _loadingDialog;

        // Goes true on DispatcherQueue.ShutdownStarting
        static internal bool IsShuttingDown = false;


        /// <summary>
        /// Hook up event listeners to PackageCatalog
        /// </summary>
        private void InitializePackageCatalogEvents()
        {
            Debug.Assert(MyThreading.CurrentIsWorkerThread);

            _packageCatalog = PackageCatalog.OpenForCurrentUser();

            _packageCatalog.PackageInstalling += (s, e)
                => OnCatalogUpdate(e.IsComplete, e.Package, null, PackageUpdateNotification.Install);

            _packageCatalog.PackageUninstalling += (s, e)
                => OnCatalogUpdate(e.IsComplete, e.Package, null, PackageUpdateNotification.Uninstall);

            _packageCatalog.PackageUpdating += (s, e)
                => OnCatalogUpdate(e.IsComplete, e.TargetPackage, e.SourcePackage, PackageUpdateNotification.Update);

            _packageCatalog.PackageStatusChanged += (s, e)
                => OnCatalogUpdate(true, e.Package, null, PackageUpdateNotification.Status);
        }

        private void SetUpBadging()
        {
            // On close, remove the badge; it's only useful when the app is open
            this.Closed += (s, e) =>
            {
                SetBadgeNumber(0);
            };

            // On start, clear the badge number too, just in case the last session didn't cleanly close
            SetBadgeNumber(0);

            this.Activated += (s, e) =>
            {
                // Clear badge when switching to or away from the window
                SetBadgeNumber(0);
                // DebugLog.Append($"Activated: {e.WindowActivationState}");
            };
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

            // Scrolling workaround
            PostScrollSelectedItemIntoView();

            // Show a "Loading ..." dialog until we have the bare minimum loaded
            if (IsLoading)
            {
                _loadingDialog = new ContentDialog()
                {
                    XamlRoot = _root.XamlRoot,
                    Content = new TextBlock()
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Text = "Loading"
                    }
                };
                _ = _loadingDialog.ShowAsync();
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

        /// <summary>
        /// Toggle all users mode on and off. (Use this rather than setting IsAllUsers property)
        /// </summary>
        void ToggleAllUsers()
        {
            // This is a workaround for Xaml not allowing tool tips on a disabled button.
            // Rather than disabling the AllUsers button when the app's not running elevated,
            // leave it enabled but show a message box if it's clicked
            if(!MainWindow.Instance.IsElevated)
            {
                _ = MyMessageBox.Show(
                    message: "To enable All Users mode, run this app elevated",
                    title: "Requires elevation");

                // The toggle button will go into a checked state,
                // even though the IsAllUsers property isn't changing.
                // Raise this to keep the UI in sync
                RaisePropertyChanged(nameof(IsAllUsers));
                return;
            }

            IsAllUsers = !IsAllUsers;
        }

        // Test/debug flag
        internal static bool LazyPreload = false;

        bool _commandLineProvided = false;

        void ProcessCommandLineArguments()
        {
            var args = Environment.GetCommandLineArgs();
            if (args != null && args.Length > 1)
            {
                if (args[1] == "-lazy")
                {
                    LazyPreload = true;
                    return;
                }

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
                if (_currentItem != value)
                {
                    if (value == null)
                    {
                        DebugLog.Append("CurrentItem is null");
                    }
                    else
                    {
                        DebugLog.Append($"CurrentItem is {value.Name}");
                    }

                    if (value != null)
                    {
                        // Once a package has been viewed, don't show it bold'd anymore
                        value.IsNewCleared = true;
                    }

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
        void OnCatalogUpdate(
            bool isComplete,
            Package wamPackage,
            Package wamPackage2, // For updates
            PackageUpdateNotification updateKind)
        {
            // This gets raised on an MTA thread. Forward to the worker thread
            if(!MyThreading.CurrentIsWorkerThread)
            {
                _ = MyThreading.RunOnWorkerAsync(() =>
                {
                    OnCatalogUpdate(isComplete, wamPackage, wamPackage2, updateKind);
                });
                return;
            }

            // Now we're on the worker (not UI) thread

            // bugbug: there's a race condition where packages are being installed/removed
            // while we're in the middle of package enumeration

            if (!isComplete)
            {
                return;
            }

            if (wamPackage.IsBundle)
            {
                // I don't understand bundles yet. When a package is added, two install notifications
                // come in. One is a normal package that will show up again on enumeration.
                // The other is a bundle that doesn't, and that throws a lot of exceptions
                return;
            }

            DebugLog.Append($"Catalog update: {updateKind}, {wamPackage.Id.FullName}, {wamPackage.InstalledPath}");

            if (Packages == null)
            {
                // bugbug: seems like a race condition where the package catalog is updated
                // while we're enumerating it
                return;
            }

            bool doBadgeUpdate = false;

            var package = PackageModel.FromWamPackage(wamPackage);

            if (updateKind == PackageUpdateNotification.Install)
            {
                // We don't track resource pakcages in the package list
                // (FindPackages and get-appxpackage don't return these either)
                if (!wamPackage.IsResourcePackage)
                {
                    // Shouldn't be necessary but playing it safe
                    RemoveFromCache(package);

                    _originalPackages.Value.Add(package);
                    _originalPackages.Value = SortPackages(_originalPackages.Value);

                    doBadgeUpdate = true;
                }
            }
            else if (updateKind == PackageUpdateNotification.Uninstall)
            {
                // The underlying package is coming as a different instance,
                // so check the PFullName
                MyThreading.RunOnUI(() =>
                {
                    if (CurrentItem.FullName == package.FullName)
                    {
                        CurrentItem = null;
                    }
                });

                RemoveFromCache(package);
                doBadgeUpdate = true;
            }
            else if (updateKind == PackageUpdateNotification.Update)
            {
                if (!wamPackage.IsResourcePackage)
                {
                    // Remove the old package from the cache and _originalPackages
                    var package2 = PackageModel.FromWamPackage(wamPackage2);
                    DebugLog.Append($"Removing old package: {package2.FullName}");
                    RemoveFromCache(package2, anyVersion: true);

                    _originalPackages.Value.Add(package);
                    _originalPackages.Value = new(_originalPackages.Value.OrderBy((p) => p.Name).ToList());

                    // If the old package was selected, select the new one
                    MyThreading.RunOnUI(() =>
                    {
                        if (CurrentItem.FullName == package2.FullName)
                        {
                            CurrentItem = package;
                        }
                    });
                }
            }
            else if (updateKind == PackageUpdateNotification.Status)
            {
                package.UpdateStatus();
            }
            else
            {
                Debug.Assert(false);
            }

            // Update the badge number on the task bar icon of new packages since refresh
            if (doBadgeUpdate)
            {
                var newPackageCount = Packages.Sum((p) => p.IsNew ? 1 : 0);
                SetBadgeNumber(newPackageCount);
            }

            // Update the UI
            MyThreading.RunOnUI(() =>
            {
                FilterAndSearchPackages();
                RaisePropertyChanged(nameof(NoPackagesFound));
            });
        }

        /// <summary>
        /// Sort the list in the UI by whatever the sort choice is
        /// </summary>
        /// <param name="packages"></param>
        /// <returns></returns>
        ObservableCollection<PackageModel> SortPackages(IEnumerable<PackageModel> packages)
        {
            if (SortByDate)
            {
                return new(packages.OrderByDescending(p => p.InstalledDate));
            }
            else
            {
                return new(packages.OrderBy(p => p.Name));
            }
        }

        /// <summary>
        /// Compare two packgaes by value, maybe ignoring the version
        /// </summary>
        bool IsPackageEqual(PackageModel package1, PackageModel package2, bool includeVersion)
        {
            if (includeVersion)
            {
                return package1.FullName == package2.FullName;
            }

            return package1.FamilyName == package2.FamilyName
                   && package1.PublisherId == package2.PublisherId
                   && package1.ResourceId == package2.ResourceId
                   && package1.Architecture == package2.Architecture;
        }

        void RemoveFromCache(PackageModel package, bool anyVersion = false)
        {
            var existing = _originalPackages.Value.FirstOrDefault(p => IsPackageEqual(p, package, !anyVersion));
            if (existing != null)
            {
                _originalPackages.Value.Remove(existing);
                DebugLog.Append($"Removed {existing.FullName} from cache");
            }
            else
            {
                DebugLog.Append($"Didn't remove {package.FullName} from cache");
            }

            PackageModel.ClearCache(package);
        }


        PackageCatalog _packageCatalog;

        // This is the raw WAM package list.
        // The wrapper class is to ensure we only use it from the worker thread
        WorkerThreadChecker<ObservableCollection<PackageModel>> _originalPackages;

        static ObservableCollection<PackageModel> _packages;
        public ObservableCollection<PackageModel> Packages
        {
            get { return _packages; }
            private set
            {
                _packages = value;

                DebugLog.Append($"New Packages count: {PackageCount}");

                RaisePropertyChanged();

                // When Packages is replaced ListView clears the selected item,
                // so restore it to CurrentItem (if it's in the list)
                if (CurrentItem != null && Packages.Contains(CurrentItem))
                {
                    _lv.SelectedItem = CurrentItem;
                }

                RaisePropertyChanged(nameof(NoPackagesFound));
                RaisePropertyChanged(nameof(PackageCount));

                // This has to be after raising property changes so that the ListView is updated
                EnsureItemSelected();
            }
        }

        int PackageCount => Packages == null ? 0 : Packages.Count;

        bool NoPackagesFound => !IsLoading && (Packages == null || Packages.Count == 0);

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

            _ = Windows.System.Launcher.LaunchUriAsync(new System.Uri($"ms-windows-store://pdp/?PFN={CurrentItem.FamilyName}"));
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

        /// <summary>
        /// True if the Launch button should be enabled.
        /// The PackageModel parameter is really just CurrentItem, but making it a parameter enables
        /// the OneWay x:Bind set up change notifications
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        bool CanLaunch(PackageModel package)
        {
            if(package == null)
            {
                return false;
            }

            if (package.AppEntries != null)
            {
                return package.AppEntries.Count != 0;
            }
            else
            {
                return false;
            }
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

        void StartLoadPackages()
        {
            IsLoading = true;

            // After loading is complete we need some time to get search cached
            IsSearchEnabled = false;

            var isAllUsers = App.IsProcessElevated() && IsAllUsers;

            MyThreading.CreateWorkerThread();

            // Do all the package loading on the worker thread
            _ = MyThreading.RunOnWorkerAsync(() => WorkerThread(isAllUsers));
        }

        /// <summary>
        /// Worker thread for doing all the work with the Package APIs
        /// </summary>
        /// <param name="isAllUsers"></param>
        void WorkerThread(bool isAllUsers)
        {
            MyThreading.SetWorkerThread(Thread.CurrentThread);
            IEnumerable<Package> wamPackages = null;
            if (isAllUsers)
            {
                wamPackages = PackageManager.FindPackages();

            }
            else
            {
                wamPackages = PackageManager.FindPackagesForUser(string.Empty);
            }

            // bugbug: better to call this before loading packages or after?
            InitializePackageCatalogEvents();

            var sorted = from p in wamPackages
                         where !p.IsResourcePackage
                         select PackageModel.FromWamPackage(p);
            var packages = sorted.ToList();

            DebugLog.Append($"{packages.Count} packages loaded");

            _originalPackages.Value = new(packages);

            var initialSort = SortPackages(_originalPackages.Value);

            bool isInput = MainWindow.PipeInputFilterString != null;
            foreach (var p in _originalPackages.Value)
            {
                // Enable filtering on UI thread
                _ = p.Name;

                // If this was launched by piping to it from get-appxpackage,
                // we need to have the FullName loaded
                if (isInput)
                {
                    _ = p.FullName;
                }
            }

            // We're not fully loaded yet, but we're loaded _just_ enough to start showing the UI
            MyThreading.RunOnUI(() =>
            {
                IsLoading = false;

                Packages = initialSort;
                FilterAndSearchPackages();

                if (_loadingDialog != null)
                {
                    _loadingDialog.Hide();
                }
            });

            // Calc the most recent InstalledDate
            PackageModel.LastInstalledDateOnRefresh = _originalPackages.Value.Max(p => p.InstalledDate);

            // Still on the worker thread, finishing loading all the Package data
            // so that we have everything cached
            _packagesToPreload = new(_originalPackages.Value);
            _packagesToLoad = new(_originalPackages.Value);

            // Finish reading the Package Manager from the worker thread
            // This can be overridden from the command line (useful for debugging)
            if (!MainWindow.LazyPreload)
            {
                FinishLoading();
            }
        }

        Queue<PackageModel> _packagesToPreload;
        Queue<PackageModel> _packagesToLoad;

        /// <summary>
        /// Finish loading the packages, working through the two load queues
        /// </summary>
        void FinishLoading()
        {
            PackageModel package;

            // There's two load phases; preload and load.
            // Preload gets enough properties loaded for the list,
            // load gets everything else necessary for the detail and search

            // So as to leave the worker thread available for requests from the UI thread,
            // we only process one item here and then post back for the next.
            // bugbug: DispatcherQueue needs a peek
            // bugbug: could make this more efficient and have a separate queue for work from the UI thread

            if (_packagesToPreload != null)
            {
                package = _packagesToPreload.Dequeue();

                if (_packagesToPreload.Count == 0)
                {
                    _packagesToPreload = null;
                }
            }

            else if (_packagesToLoad != null)
            {
                package = _packagesToLoad.Dequeue();
                package.EnsureInitializeAsync();

                if (_packagesToLoad.Count == 0)
                {
                    _packagesToLoad = null;
                }
            }

            else
            {
                DebugLog.Append("Finished loading");
                MyThreading.RunOnUI(() => IsSearchEnabled = true);
                
                return;
            }

            Debug.Assert(package != null);

            // Move on to the next item (this post might go behind something from the UI thread)
            MyThreading.PostToWorker(
                () => FinishLoading(),
                DispatcherQueuePriority.Low);
        }

        void EnsureItemSelected()
        {
            if (_lv.Items.Count == 0)
            {
                return;
            }

            // If something's already selected, don't change it
            if (CurrentItem != null)
            {
                return;
            }

            // Pick an item at random from the packages (after filtering and searching)
            if (SortByName)
            {
                Random random = new();
                var index = random.Next(0, _lv.Items.Count - 1);
                SelectPackage(_lv.Items[index] as PackageModel);
            }
            else
            {
                //_lv.SelectedIndex = 0;
                _lv.SelectedItem = _lv.Items[0];
            }

        }

        static internal PackageManager PackageManager = new PackageManager();

        public event PropertyChangedEventHandler PropertyChanged;

        void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// The max ActualWidth of the package name TextBlock in the ListView
        /// </summary>
        public double MaxNameWidth
        {
            get => _maxNameWidth;
            set
            {
                _maxNameWidth = value;

                Debug.WriteLine($"ControllingWidth = {value}");

                RaisePropertyChanged();

                // bugbug: This Grid has a TextBox next to a DropDownButton
                // For some reason, when the column that the Grid is in resizes,
                // the TextBox resizes correctly, but the DropDownButton doesn't mover over.
                DispatcherQueue.TryEnqueue(() =>
                {
                    _workaroundGrid.InvalidateMeasure();
                });
            }
        }
        double _maxNameWidth = 200;

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
                    removing = PackageManager.RemovePackageAsync(package.FullName, RemovalOptions.RemoveForAllUsers);
                }
                else
                {
                    removing = PackageManager.RemovePackageAsync(package.FullName);
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
            Debug.Assert(MyThreading.CurrentIsUiThread);

            if (Packages == null)
            {
                return;
            }

            // This is the one place where we get the raw package list from the UI thread
            IEnumerable<PackageModel> packages = _originalPackages.ValueNoThreadCheck;

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
                        Debug.Assert(p.IsFullNameLoaded);
                        if (PipeInputPackages.Contains(p.FullName))
                        {
                            filteredPackages.Add(p);
                        }
                    }
                    else
                    {
                        Debug.Assert(p.IsNameLoaded);

                        var matches = filterRegex.Matches(p.Name);
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

                Packages = SortPackages(packages);
        }

        private void SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
        {
            // Using SelectionChanged to update CurrentItem, rather than a TwoWay binding,
            // because sometimes they're not in sync (CurrentItem can be a resource package,
            // but resource packages aren't in the ListView).

            // SelectedItem goes null every time Packages changes, so in those cases we want to
            // restore selection to CurrentItem. It also goes null if the PackageView is showing
            // a resource package.

            if (_lv.SelectedItem != null)
            {
                CurrentItem = _lv.SelectedItem as PackageModel;
                PostScrollSelectedItemIntoView();
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

                if (e2 is COMException comException && (uint)comException.HResult == 0x800b0100)
                {
                    message += "\n\nNote: you can use the Register button to add an unsigned package";
                }

                _ = MyMessageBox.Show(message, "Failed to add package");
            }
        }


        private void NameSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var fe = sender as FrameworkElement;
            if (fe.ActualWidth > MaxNameWidth)
            {
                MaxNameWidth = fe.ActualWidth;
            }
        }

        private static void LaunchPackage(string aumid)
        {
            ProcessStartInfo si = new();
            si.FileName = $@"shell:AppsFolder\{aumid}";
            si.UseShellExecute = true;

            try
            {
                Process.Start(si);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Launch an app from its package
        /// </summary>
        private void LaunchPackage2(object sender, RoutedEventArgs e)
        {
            var package = _lv.SelectedItem as PackageModel;
            if (package == null)
            {
                Debug.Assert(false);
                return;
            }

            var appEntries = package.AppEntries;

            var count = 0;
            if(appEntries != null)
            {
                count = appEntries.Count;
            }

            if(count == 0)
            {
                return;
            }

            // One aumid means launch it
            else if (count == 1)
            {
                appEntries[0].Launch();
                return;
            }

            // More than one aumid means show a menu
            else
            {
                MenuFlyout flyout = new();
                foreach (var app in appEntries)
                {
                    MenuFlyoutItem item = new();
                    item.Text = app.Id;
                    item.Click += (s, e) => app.Launch();
                    flyout.Items.Add(item);
                }
                _launchButton.Flyout = flyout;
                flyout.ShowAt(_launchButton);
            }
        }


        internal void SelectPackage(PackageModel package)
        {
            // Note that the current package isn't always in the Packages list,
            // e.g. a dependency package of something that _is_ in the list
            CurrentItem = package;

            var matchingPackage = Packages.FirstOrDefault(p => p.FullName == package.FullName);
            if (matchingPackage != null)
            {
                PostScrollSelectedItemIntoView();
            }
            else
            {
                _lv.SelectedIndex = -1;
            }

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

        private void ShowAppxLog(object sender, RoutedEventArgs e)
        {
            if(_logViewerWindow != null)
            {
                _logViewerWindow.Activate();
                return;
            }

            _logViewerWindow = AppxLogViewer.Show();
            _logViewerWindow.Closed += ((_,_) => _logViewerWindow = null);
        }
        Window _logViewerWindow = null;

        /// <summary>
        /// Run Powershell with package identity on the process
        /// </summary>
        private void RunPowershellAsPackage(object sender, RoutedEventArgs e)
        {
            if (!CanLaunch(CurrentItem))
            {
                // Either CurrentItem is null, or it doesn't have app entries
                // (This operation requires an Aumid)
                return;
            }

            // Putting the rest of this in a try/catch because I'm not sure if PS can raise
            // And then not being super careful about the possibility of a package
            // being configured in a way I don't understand and throwing somewhere
            try
            {
                // Run a Powershell process and call Invoke-CommandInDesktopPackage in it,
                // and have that create a second PowerShell process.
                // The first will be hidden though and go away, the second will be left for the user

                // This is an example of the final argument string that will be passed to
                // the first PowerShell:
                //
                // Invoke-CommandInDesktopPackage -PackageFamilyName Microsoft.PowerAutomateDesktop_8wekyb3d8bbwe -AppId PAD.Console -Command powershell -Args '-NoExit -Command "& ''C:\Users\mike\source\repos\MikeHillberg\view-appxpackage\Package\bin\x64\Debug\AppX\Assets\RunAs Package.ps1'' ''Invoke-CommandInDesktopPackage -PackageFamilyName Microsoft.PowerAutomateDesktop_8wekyb3d8bbwe -AppId PAD.Console -Command powershell''" '

                // bugbug: how to figure out if this should be powershell or pwsh?
                ProcessStartInfo psi = new();
                psi.FileName = "powershell.exe";
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;

                // We need a Praid for the call to Invoke-CommandInDesktopPackage
                // This gets the Praid from the first app list entry, though there could be many.
                // But that should be OK because all the aumid have the same package identity.
                // Not sure why any aumid is even necessary?
                var praid = CurrentItem.AppEntries[0].Id;

                // This is the package path of _this_ app, not the CurrentItem package.
                // That's where the script is that we're going to run in the second PowerShell,
                // which displays some messages to the user
                var myPackagePath = Package.Current.InstalledPath;

                // This is the first part of the command that the first PowerShell will run,
                // to call Invoke-CommandInDesktopPackage, giving it the PFN and Praid and
                // telling it to run the second PowerShell
                var invokeCommandBase = @$"Invoke-CommandInDesktopPackage -PackageFamilyName {CurrentItem.FamilyName} -AppId {praid} -Command powershell";


                // These are the args that will be passed to the nested PS that's created by the
                // Invoke script. The parameters passed to runaspackage.ps1 are just for a message
                // The nested PowerShell will run the "RunAs Package.ps1" script.
                // Getting all the quotes correct is complicated, there's a lot of string nesting going on, but it works.
                // A difficult part is allowing for spaces in the script name, 
                // it would be a lot easier to remove it, but when this is run as the actual Store-installed app
                // it has a space because it's in "Program Files". So the script name has a space to test that in inner loop.
                // The `invokeCommandBase` is used as the base of the arguments to the second PowerShell, and also
                // passed to the script, so the script can show a message to the user of what just happened.

                psi.Arguments = $@"{invokeCommandBase} -Args '-NoExit -Command ""& ''{myPackagePath}\Assets\RunAs Package.ps1'' ''{invokeCommandBase}''"" '";



                DebugLog.Append($"Running as Package:");
                DebugLog.Append(psi.Arguments);

                Process process = new();
                process.StartInfo = psi;
                process.Start();

                // bugbug: this takes several seconds to show anything, should show some kind of progress UI
                // process.Exited doesn't get raised for some reason,
                // but that's not the right process anyway
            }
            catch (Exception ex)
            {
                DebugLog.Append($"Failed RunAsPackage: {ex.Message}");
            }
        }

        /// <summary>
        /// Scroll CurrentItem into view
        /// </summary>
        // bugbug: 
        // This shouldn't be necessary, ListView should be doing this, but for some reason it often doesn't
        // Current workaround is sprinkle calls to this
        void PostScrollSelectedItemIntoView()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_lv.SelectedItem == null)
                {
                    return;
                }

                _lv.ScrollIntoView(_lv.SelectedItem);
            });
        }

        /// <summary>
        /// Sort package results by name
        /// </summary>
        bool SortByName
        {
            get => _sortByName;
            set
            {
                _sortByName = value;
                _sortByDate = !value;

                RaisePropertyChanged(nameof(SortByDate));
                RaisePropertyChanged(nameof(SortByName));
                RaisePropertyChanged(nameof(SortLabel));

                FilterAndSearchPackages();
                SaveSettings();
            }
        }
        bool _sortByName = false;


        const string _sortSettingName = "Sort2";
        void SaveSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[_sortSettingName] = SortByDate;
        }

        void LoadSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.TryGetValue(_sortSettingName, out object value))
            {
                SortByDate = (bool)value;
            }
        }

        /// <summary>
        /// Sort package list by installed date
        /// </summary>
        bool SortByDate
        {
            get => _sortByDate;
            set
            {
                _sortByDate = value;
                _sortByName = !value;

                RaisePropertyChanged(nameof(SortByDate));
                RaisePropertyChanged(nameof(SortByName));
                RaisePropertyChanged(nameof(SortLabel));

                FilterAndSearchPackages();
                SaveSettings();
            }
        }
        bool _sortByDate = true;

        /// <summary>
        /// Label for the Sort button
        /// </summary>
        string SortLabel
        {
            get => SortByDate ? "Date" : "Name";
        }

        /// <summary>
        /// Listen to SizeChanged to track the max ActualWidth in the MaxListWidth property
        /// </summary>
        private void List2SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var fe = sender as FrameworkElement;
            if (fe.ActualWidth > MaxListWidth)
            {
                MaxListWidth = fe.ActualWidth;
            }
        }


        /// <summary>
        /// Max ActualWidth of the ListView
        /// </summary>
        public double MaxListWidth
        {
            get => _maxListWidth;
            set
            {
                _maxListWidth = value;
                RaisePropertyChanged();
            }
        }
        double _maxListWidth = 0;

        /// <summary>
        /// Create a bunch of settings to help test/debug the settings viewer
        /// </summary>
        [Conditional("DEBUG")]
        void CreateSettings()
        {
            var createSettings = (ApplicationDataContainer container) =>
            {
                container.Values["Test1"] = "Test1";
                container.Values["Test2"] = "Test2";
            };
            ApplicationDataContainer localSettingsContainer = ApplicationData.Current.LocalSettings;
            createSettings(localSettingsContainer);

            var child = localSettingsContainer.CreateContainer("Container1", ApplicationDataCreateDisposition.Always);
            createSettings(child);

            child = localSettingsContainer.CreateContainer("Container2", ApplicationDataCreateDisposition.Always);
            createSettings(child);

            child = child.CreateContainer("Container3", ApplicationDataCreateDisposition.Always);
            createSettings(child);
        }

    }

    /// <summary>
    /// Hack to help get the code behind available to a DataTemplate
    /// This is set in a ResourceDictionary, making it accessible to {Binding}
    /// (x:Bind in a DataTemplate doesn't have a way to reach out of the template unfortunately)
    /// </summary>
    public class XClassMapper
    {
        public MainWindow XClass { get; set; }

    }
}
