using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
using Windows.Storage;
using System.Threading;
using Microsoft.UI.Dispatching;
using System.Text;

namespace ViewAppxPackage;

public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    internal PackageCatalogModel CatalogModel;

    // Dialog that shows until we have the bare minimum loaded
    ContentDialog _loadingDialog;

    // Goes true on DispatcherQueue.ShutdownStarting
    static internal bool IsShuttingDown = false;

    // MCP Server instance for serving package data via Model Context Protocol
    private McpServerService _mcpServer;

    /// <summary>
    /// True if running in MCP server mode (headless)
    /// </summary>
    private bool IsMcpServerMode => _mcpServer != null;

    public MainWindow()
    {
        Instance = this;
        MyThreading.SetCurrentAsUIThread();

        CatalogModel = PackageCatalogModel.Instance;

        ProcessCommandLineArguments();

        // bugbug: there's a race where we load the initial set here but
        // haven't set up the change event listeners yet
        StartLoadPackages();

        // In MCP server mode, we only need package loading, not UI initialization
        if (_isMcpServerMode)
        {
            return;
        }

        this.InitializeComponent();

        HookCatalogModelEvents();

        // Hack to reference code behind from within a template
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

        Closed += (s, e) =>
        {
            _logViewerWindow?.Close();
            _mcpServer?.Stop();
        };

        EnsureSampleSettings();
    }

    private void HookCatalogModelEvents()
    {
        CatalogModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CatalogModel.PackageCount))
            {
                RaisePropertyChanged(nameof(NoPackagesFound));
            }
            else if (e.PropertyName == nameof(CatalogModel.NewPackageCount))
            {
                SetBadgeNumber(CatalogModel.NewPackageCount);
            }
        };

        // Raised at the end of changing CatalogManager.Packages
        CatalogModel.PackagesReplaceComplete += (s, e) =>
        {
            RaisePropertyChanged(nameof(NoPackagesFound));

            if (CatalogModel.CurrentItem != null)
            {
                // When Packages is replaced ListView clears the selected item,
                // so restore it to CurrentItem (if it's in the list)
                if (CatalogModel.Packages.Contains(CurrentItem))
                {
                    _lv.SelectedItem = CurrentItem;
                }

                // If the current item isn't in the list, clear it.
                // The subsequent EnsureItemSelected will pick something
                else
                {
                    CatalogModel.CurrentItem = null;
                }
            }

            // This has to be after raising property changes so that the ListView is updated
            EnsureItemSelected();
        };

        // Take down the "loading..." dialog when all the packages are read and the key properties fetched
        CatalogModel.MinimallyLoaded += (s, e) =>
        {
            IsLoading = false;

            if (_loadingDialog != null)
            {
                _loadingDialog.Hide();
            }
        };

        // Enable the regex search when all the properties on all the packages have been read
        CatalogModel.FullyLoaded += (s, e) =>
        {
            IsSearchEnabled = true;
        };
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

        // Show the loading dialog
        EnsureLoadingDialog();
    }

    /// <summary>
    /// True if we've loaded packages and found none (probably filtered out)
    /// </summary>
    bool NoPackagesFound => !IsLoading && (CatalogModel.Packages == null || CatalogModel.Packages.Count == 0);

    /// <summary>
    /// Show the loading dialog if we're in IsLoading and not already showing the Help dialog
    /// </summary>
    void EnsureLoadingDialog()
    {
        // You can't show two dialogs at the same time
        if (IsLoading && !_showingHelp)
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


    async void ShowHelp()
    {
        Help help = new()
        {
            XamlRoot = _lv.XamlRoot,
            CloseButtonText = "Close"
        };

        _showingHelp = true;
        _ = await help.ShowAsync();
        _showingHelp = false;

        // Now that the Help dialog is out of the way it might be necessary to show the loading dialog
        EnsureLoadingDialog();
    }
    bool _showingHelp = false;

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
            : "Preparing for search ...";
    }

    private string FilterPlaceholderText = "Filter with wildcards, e.g. *App* (Ctrl+F)";

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
        if (!MainWindow.Instance.IsElevated)
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
            // Loop through all arguments to find flags, allowing them in any order
            bool foundSpecialFlag = false;
            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i].ToLower();
                
                if (arg == "-lazy")
                {
                    // For debugging
                    LazyPreload = true;
                    foundSpecialFlag = true;
                }
                else if (arg == "-mcpserver")
                {
                    // Start MCP server mode - this will run as a separate instance
                    StartMcpServerMode();
                    foundSpecialFlag = true;
                }
            }

            // If we found special flags, don't treat remaining args as filter
            if (foundSpecialFlag)
            {
                return;
            }

            // Use first non-flag argument as filter
            _commandLineProvided = true;
            CatalogModel.Filter = args[1];
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
                CatalogModel.PipeInputPackages = names.ToArray();

                // Set the filter box to "$input" indicating we should use this list of names
                CatalogModel.Filter = PipeInputFilterString;
            }
        }
    }

    /// <summary>
    /// Magic value for the filter string that means we're using the input from the stdin
    /// </summary>
    static public string PipeInputFilterString = "$input";

    internal static FrameworkElement RootElement { get; private set; }

    internal static MainWindow Instance;

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

        return CatalogModel.CurrentItem != null && CatalogModel.CurrentItem.CanOpenStore;
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

    private PackageModel CurrentItem => CatalogModel.CurrentItem;

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
        if (package == null)
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

    async void StartLoadPackages()
    {
        IsLoading = true;

        // After loading is complete we need some time to get search cached
        IsSearchEnabled = false;

        var isAllUsers = App.IsProcessElevated() && IsAllUsers;

        // Everything we do with actual Package APIs is on a single background thread
        await MyThreading.CreateWorkerThreadAsync();

        // Do all the package loading on the worker thread
        _ = MyThreading.RunOnWorkerAsync(
            () => CatalogModel.WorkerThread(
               isAllUsers: isAllUsers,
               isInput: MainWindow.PipeInputFilterString != null));
    }

    /// <summary>
    /// Worker thread for doing all the work with the Package APIs
    /// </summary>
    /// <param name="isAllUsers"></param>
    //void WorkerThread(bool isAllUsers)
    //{
    //    MyThreading.SetWorkerThread(Thread.CurrentThread);

    //    bool isInput = MainWindow.PipeInputFilterString != null;
    //    PackageCatalogModel.Instance.Initialize(
    //        isAllUsers,
    //        preloadFullName: isInput,
    //        useSettings: true);
    //}

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
        if (CatalogModel.SortByName)
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

    public event PropertyChangedEventHandler PropertyChanged;

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

        _ = CatalogModel.RemovePackageAsync(
            package,
            async (op) =>
            {
                _ = await CallWithProgress(op, "Removing ...");
            });
    }


    void RaisePropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
            CatalogModel.CurrentItem = _lv.SelectedItem as PackageModel;
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
        picker.FileTypeFilter.Add(".appxbundle");
        picker.FileTypeFilter.Add(".msix");
        picker.FileTypeFilter.Add(".msixbundle");
        var storageFile = await picker.PickSingleFileAsync();
        if (storageFile == null)
        {
            return;
        }

        var fileUri = new Uri(storageFile.Path);

        try
        {
            await CatalogModel.AddPackageAsync(fileUri, async (op) =>
            {
                _ = await CallWithProgress(op, "Adding ...");
            });
            //var adding = PackageManager.AddPackageAsync(fileUri, null, DeploymentOptions.None);
            //_ = await CallWithProgress(adding, "Adding ...");

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
        if (appEntries != null)
        {
            count = appEntries.Count;
        }

        if (count == 0)
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
        CatalogModel.CurrentItem = package;

        var matchingPackage = CatalogModel.Packages.FirstOrDefault(p => p.FullName == package.FullName);
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
        CatalogModel.CurrentItem = package;
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

        await CatalogModel.RegisterPackageByUriAsync(manifestUri, async (op) =>
        {
            var addResult = await CallWithProgress(op, "Adding ...");
            if (!addResult.IsRegistered)
            {
                _ = MyMessageBox.Show($"{addResult.ErrorText}", "Failed to add package", isOKEnabled: true);
            }
        });

        //Windows.Management.Deployment.RegisterPackageOptions options = new()
        //{
        //    DeveloperMode = true
        //};

        //try
        //{
        //    // bugbug: update the docs
        //    // Requires <rescap:Capability Name="packageManagement"/>
        //    // or results in
        //    // "The filename, directory name, or volume label syntax is incorrect."
        //    var registering = PackageManager.RegisterPackageByUriAsync(manifestUri, options);

        //    var addResult = await CallWithProgress(registering, "Adding ...");
        //    if (!addResult.IsRegistered)
        //    {
        //        _ = MyMessageBox.Show($"{addResult.ErrorText}", "Failed to add package", isOKEnabled: true);
        //    }
        //}
        //catch (Exception e2)
        //{
        //    _ = MyMessageBox.Show(e2.Message, "Failed to add package", isOKEnabled: true);
        //}
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
        if (_logViewerWindow != null)
        {
            _logViewerWindow.Activate();
            return;
        }

        _logViewerWindow = AppxLogViewer.Show();
        _logViewerWindow.Closed += ((_, _) => _logViewerWindow = null);
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
    void EnsureSampleSettings()
    {
        var createSettings = (ApplicationDataContainer container) =>
        {
            EnsureSampleSetting(container, "StringSample1", "Test 1");
            EnsureSampleSetting(container, "StringSample2", "Test 2");
            EnsureSampleSetting(container, "Int32Sample", (int)123);
            EnsureSampleSetting(container, "DateSample", DateTimeOffset.Now);
            EnsureSampleSetting(container, "IntArraySample", new int[] { 1, 2, 3 });
            EnsureSampleSetting(container, "StringArraySample", new string[] { "Hello", "world" });
            EnsureSampleSetting(container, "PointArraySample", new Point[] { new Point(0, 1), new Point(2, 3) });
            EnsureSampleSetting(container, "RectArraySample", new Rect[] { new Rect(1, 2, 3, 4), new Rect(5, 6, 7, 8) });
            EnsureSampleSetting(container, "SizeArraySample", new Size[] { new Size(1, 2), new Size(3, 4) });

            StringBuilder sb = new();
            sb.AppendLine("Line 1");
            sb.AppendLine("Line 2");
            EnsureSampleSetting(container, "MultiLineSample", sb.ToString());

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

    void EnsureSampleSetting<T>(
        ApplicationDataContainer container,
        string key,
        T value
        )
    {
        if (!container.Values.TryGetValue(key, out var current))
        {
            container.Values[key] = value;
        }
    }

    /// <summary>
    /// Starts the application in MCP (Model Context Protocol) server mode.
    /// In this mode, the application runs headlessly and serves package data via MCP tools
    /// without showing the UI. This allows external systems to query package information.
    /// </summary>
    private async void StartMcpServerMode()
    {
        try
        {
            // Initialize the MCP server
            _mcpServer = new McpServerService();

            // Start the server - this will wait for package loading to complete
            // and then serve MCP tools
            await _mcpServer.StartAsync();
        }
        catch (Exception ex)
        {
            DebugLog.Append($"Failed to start MCP server: {ex.Message}");
            
            // Exit the application if server startup fails
            App.Current?.Exit();
        }
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
