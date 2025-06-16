namespace ViewAppxPackage;

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Management.Deployment;
using Windows.Storage;

internal partial class PackageCatalogModel : ObservableObject, INotifyPropertyChanged
{
    PackageCatalog _packageCatalog;
    Queue<PackageModel> _packagesToPreload;
    Queue<PackageModel> _packagesToLoad;
    string _filter;
    string _searchText;
    bool _preloadFullName = false;
    PackageModel _currentItem;
    bool _sortByName = false;
    bool _sortByDate = true;
    const string _sortSettingName = "Sort2";
    bool _useSettings;

    // This is the full package list.
    // The wrapper class is to ensure we only use it from the worker thread
    WorkerThreadChecker<ObservableCollection<PackageModel>> _originalPackages;

    // This is the view on the package list (after search/filter)
    ObservableCollection<PackageModel> _packages;

    static internal PackageCatalogModel Instance = new PackageCatalogModel();
    static internal PackageManager PackageManager = new PackageManager();

    internal bool IsAllUsers;

    /// <summary>
    /// Loaded enough that it's OK to show the UI
    /// </summary>
    internal event EventHandler MinimallyLoaded;

    /// <summary>
    /// Background loading is complete too
    /// </summary>
    internal event EventHandler FullyLoaded;

    /// <summary>
    /// Raised when the Packages property has been set and processed
    /// </summary>
    internal event EventHandler PackagesReplaceComplete;

    [ObservableProperty]
    internal int packageCount;




    /// <summary>
    /// Initialize the catalog model
    /// </summary>
    /// <param name="isAllUsers">True if running elevated</param>
    /// <param name="preloadFullName">If true, initialize FullName (so that UI thread can use it)</param>
    internal void Initialize(bool isAllUsers, bool preloadFullName, bool useSettings)
    {
        _useSettings = useSettings;
        _preloadFullName = preloadFullName;

        IEnumerable<Package> wamPackages = null;

        // Need this before we start loading so that we know how/what to load
        LoadSettings();

        IsAllUsers = isAllUsers;

        // Get all the packages on the system or for this user
        if (isAllUsers)
        {
            wamPackages = PackageManager.FindPackages();
        }
        else
        {
            // "" means current user
            wamPackages = PackageManager.FindPackagesForUser(userSecurityId: string.Empty);
        }

        // Hook events notifying of updates to the catalog
        // bugbug: better to call this before loading packages or after?
        this.InitializePackageCatalogEvents();

        // Create a sorted list of PackageModels
        var sorted = from p in wamPackages
                     where !p.IsResourcePackage
                     select PackageModel.FromWamPackage(p);
        var packages = sorted.ToList();

        DebugLog.Append($"{packages.Count} packages loaded");

        // This is the baseline list, filter and search will produce this.Packages
        _originalPackages.Value = new(packages);

        var initialSort = SortPackages(_originalPackages.Value);

        foreach (var p in _originalPackages.Value)
        {
            p.DoInitialLoad(preloadFullName);

            //// Enable filtering on UI thread
            //_ = p.Name;

            //// If this was launched by piping to it from get-appxpackage,
            //// we need to have the FullName loaded
            //if (preloadFullName)
            //{
            //    _ = p.FullName;
            //}
        }

        // We're not fully loaded yet, but we're loaded _just_ enough to start showing the UI
        MyThreading.PostToUI(() =>
        {
            Packages = initialSort;
            FilterAndSearchPackages();

            MinimallyLoaded?.Invoke(this, EventArgs.Empty);
            //IsLoading = false;

            //if (_loadingDialog != null)
            //{
            //    _loadingDialog.Hide();
            //}
        });

        // Calc the most recent InstalledDate
        PackageModel.LastInstalledDateOnRefresh = _originalPackages.Value.Max(p => p.InstalledDate);

        // Still on the worker thread, finishing loading all the Package data
        // so that we have everything cached
        _packagesToPreload = new(_originalPackages.Value);
        _packagesToLoad = new(_originalPackages.Value);

        // Finish reading the Package Manager from the worker thread
        // This can be overridden from the command line (useful for debugging)
        if (!App.LazyPreload)
        {
            FinishLoading();
        }
    }

    /// <summary>
    /// List of package names that we got from stdin
    /// </summary>
    internal string[] PipeInputPackages = null;

    /// <summary>
    ///  Search text, regex form
    /// </summary>
    internal string SearchText
    {
        get { return _searchText; }
        set
        {
            _searchText = value;
            RaisePropertyChanged();
            FilterAndSearchPackages();
        }
    }

    /// <summary>
    /// Package name filter, e.g. *App*
    /// </summary>
    internal string Filter
    {
        get { return _filter; }
        set
        {
            _filter = value;
            RaisePropertyChanged();
            FilterAndSearchPackages();
        }
    }

    /// <summary>
    /// Reset Search and Filter properties with only one set of change notifications
    /// </summary>
    internal void ResetSearchAndFilter()
    {
        _searchText = null;
        _filter = null;

        RaisePropertyChanged(nameof(Filter));
        RaisePropertyChanged(nameof(SearchText));
        FilterAndSearchPackages();
    }

    public ObservableCollection<PackageModel> Packages
    {
        get { return _packages; }
        private set
        {
            _packages = value;

            DebugLog.Append($"New Packages count: {PackageCount}");

            RaisePropertyChanged();

            //// When Packages is replaced ListView clears the selected item,
            //// so restore it to CurrentItem (if it's in the list)
            //if (CurrentItem != null && Packages.Contains(CurrentItem))
            //{
            //    _lv.SelectedItem = CurrentItem;
            //}

            //RaisePropertyChanged(nameof(NoPackagesFound));
            UpdatePackageCount();

            //// This has to be after raising property changes so that the ListView is updated
            //EnsureItemSelected();
            PackagesReplaceComplete?.Invoke(this, EventArgs.Empty);
        }
    }


    [ObservableProperty]
    public int newPackageCount;


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

            //MyThreading.PostToUI(() => IsSearchEnabled = true);
            MyThreading.PostToUI(() => FullyLoaded?.Invoke(this,EventArgs.Empty));

            return;
        }

        Debug.Assert(package != null);

        // Move on to the next item (this post might go behind something from the UI thread)
        if (!MyThreading.HasShutdownStarted)
        {
            MyThreading.PostToWorker(
                () => FinishLoading(),
                DispatcherQueuePriority.Low);
        }
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
        if (!MyThreading.CurrentIsWorkerThread)
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

        bool newCountNeedsUpdating = false;

        var package = PackageModel.FromWamPackage(wamPackage);

        if (updateKind == PackageUpdateNotification.Install)
        {
            // We don't track resource packages in the package list
            // (FindPackages and get-appxpackage don't return these either)
            if (!wamPackage.IsResourcePackage)
            {
                // Shouldn't be necessary but playing it safe
                RemoveFromCache(package);

                package.DoInitialLoad(_preloadFullName);

                _originalPackages.Value.Add(package);
                _originalPackages.Value = SortPackages(_originalPackages.Value);

                newCountNeedsUpdating = true;
            }
        }
        else if (updateKind == PackageUpdateNotification.Uninstall)
        {
            // The underlying package is coming as a different instance,
            // so check the PFullName
            MyThreading.PostToUI(() =>
            {
                if(CurrentItem == null)
                {
                    return;
                }

                if (CurrentItem.FullName == package.FullName)
                {
                    CurrentItem = null;
                }
            });

            RemoveFromCache(package);
            newCountNeedsUpdating = true;
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
                MyThreading.PostToUI(() =>
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
        int newCount = 0;
        if (newCountNeedsUpdating)
        {
            newCount = _originalPackages.Value.Sum((p) => p.IsNew ? 1 : 0);
        }

        // Update the UI
        MyThreading.PostToUI(() =>
        {
            if(newCountNeedsUpdating)
            {
                NewPackageCount = newCount;
            }

            FilterAndSearchPackages();
            UpdatePackageCount();
        });
    }

    /// <summary>
    /// Hook up event listeners to PackageCatalog
    /// </summary>
    void InitializePackageCatalogEvents()
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

    void UpdatePackageCount()
    {
        if (Packages == null)
        {
            PackageCount = 0;
            return;
        }

        var count = Packages.Count;
        PackageCount = count;
    }

    void RaisePropertyChanged([CallerMemberName] string propertyName = null)
    {
        this.OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
    }

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
            var isInput = _filter.Trim() == App.PipeInputFilterString;

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
    /// Remove an appx package
    /// </summary>
    async public Task RemovePackageAsync(
        PackageModel package,
        Func<IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress>, Task> progressFuncAsync)
    {
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
            //_ = await CallWithProgress(removing, "Removing ...");
            await progressFuncAsync(removing);
        }
        catch (Exception e2)
        {
            Debug.WriteLine(e2.Message);
            _ = MyMessageBox.Show(e2.Message, "Failed to remove package", isOKEnabled: true);
        }
    }

    async public Task AddPackageAsync(
        Uri fileUri,
        Func<IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress>, Task> progressFuncAsync)
    {
        var adding = PackageManager.AddPackageAsync(fileUri, null, DeploymentOptions.None);

        //_ = await CallWithProgress(adding, "Adding ...");
        await progressFuncAsync(adding);
    }

    async public Task RegisterPackageByUriAsync(
        Uri manifestUri,
        Func<IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress>, Task> progressFuncAsync)
    {
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

            await progressFuncAsync(registering);
        }
        catch (Exception e2)
        {
            _ = MyMessageBox.Show(e2.Message, "Failed to add package", isOKEnabled: true);
        }
    }


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

    /// <summary>
    /// Sort package results by name
    /// </summary>
    public bool SortByName
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

    /// <summary>
    /// Sort package list by installed date
    /// </summary>
    internal bool SortByDate
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

    /// <summary>
    /// Label for the Sort button
    /// </summary>
    internal string SortLabel
    {
        get => SortByDate ? "Date" : "Name";
    }

    void SaveSettings()
    {
        if(!_useSettings)
        {
            return;
        }

        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        localSettings.Values[_sortSettingName] = SortByDate;
    }

    void LoadSettings()
    {
        if (!_useSettings)
        {
            return;
        }

        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        if (localSettings.Values.TryGetValue(_sortSettingName, out object value))
        {
            SortByDate = (bool)value;
        }
    }

     /// <summary>
     /// Unzip a package for sideloading ad a loose file deployment
     /// </summary>
    internal string UnzipPackage(string packagePath, string directoryPath)
    {
        DebugLog.Append($"Unzipping {packagePath} to {directoryPath}");

        // Bugbug: a bit of guessing here, should figure out a proper parsing
        // Algorithm is to unzip the file and look for appxmanifest.xml
        // If that doesn't work, look for a msix or appx file in the unzip,
        // and if there unzip and look for appxmanifest.xml again

        // ZipFile feels like something that could throw exceptions
        try
        {
            for (int i = 0; i < 2; i++)
            {
                // Unzip the package to a directory
                ZipFile.ExtractToDirectory(packagePath, directoryPath);

                // If there's an appxmanifest.xml, return the full path to it
                var manifestPath = Path.Combine(directoryPath, "AppxManifest.xml");
                if (File.Exists(manifestPath))
                {
                    return manifestPath;
                }

                // There wasn't an appxmanifest.xml, see if there's an msix or appx
                packagePath = Directory.GetFiles(directoryPath, "*.msix").FirstOrDefault();
                if (packagePath == null)
                {
                    packagePath = Directory.GetFiles(directoryPath, "*.appx").FirstOrDefault();
                    if (packagePath == null)
                    {
                        DebugLog.Append($"No msix/appx file found in {directoryPath}");
                        return null;
                    }
                }

                // Try this new packagePath and directoryPath
                directoryPath = $"{packagePath}.unzip";
            }
        }
        catch (Exception e)
        {
            DebugLog.Append(e, "Couldn't unzip package");
        }

        return null;
    }
}
