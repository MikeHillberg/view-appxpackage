using Microsoft.UI.Text;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.Management.Core;
using Windows.UI.Text;

namespace ViewAppxPackage
{
    /// <summary>
    /// Wrapper around Package class
    /// </summary>
    public partial class PackageModel : INotifyPropertyChanged
    {
        Package _package;

        // Cache all the PropertyInfos, used during initialization
        static IList<PropertyInfo> _thisProperties = typeof(PackageModel).GetProperties().ToList();
        
        // We're in the Initializing state until all of the Package has been read
        // Preinitializing is while we're loading the first few properties for the initial UI
        public bool Initializing { get; private set; } = false;
        bool _initialized = false;
        bool _preloaded = false;

        /// <summary>
        /// Get all state out of the Package and into this model (fill the caches).
        /// If called from the UI thread and not already done, 
        /// this will post to the worker thread and raise a change notification.
        /// </summary>
        /// <param name="debug"></param>
        public void EnsureInitializeAsync(bool debug = false)
        {
            if (_initialized || Initializing)
            {
                // Already done or underway
                return;
            }

            Initializing = true;

            if(MainWindow.CurrentIsUiThread)
            {
                DebugLog.Append($"Triggering load for {_name.CurrentValue}");
            }

            // Do the work on the worker thread
            MainWindow.RunOnWorker(() =>
            {
                // Call all the getters. Any of these not already initialized will set their cache
                foreach (var prop in _thisProperties)
                {
                    try
                    {
                        _ = prop.GetValue(this);
                    }
                    catch (Exception e)
                    {
                        DebugLog.Append($"Exception getting {prop.Name}: {e.Message}");
                    }
                }

                _preloaded = true;
                _initialized = true;
                Initializing = false;

                DebugLog.Append($"Loaded {Name}");
                RaisePropertyChangedOnUIThread(null);
            });
        }

        /// <summary>
        /// Ensure the preload properties are loaded.
        /// These are the first few properties we need to show the first frame of the UI
        /// </summary>
        public void EnsurePreinitializeAsync()
        {
            if (_preloaded)
            {
                return;
            }

            this.Preload.EnsurePreloadedAsync();
        }

        /// <summary>
        /// Nested class to handling preloading a few Very Important Properties.
        /// They're loaded off the UI thread and raise a change notification.
        /// Calling this from an x:Bind causes the UI to fault in just these properties,
        /// for just the packages that need them.
        /// </summary>
        public class PreloadPackageModel
        {
            PackageModel _package;
            public PreloadPackageModel(PackageModel package)
            {
                _package = package;
            }

            internal void EnsurePreloadedAsync()
            {
                if (_package._preloaded)
                {
                    // Already done or in progress
                    return;
                }

                _package._preloaded = true;

                MainWindow.RunOnWorker(() =>
                {
                    // Call the property getters to get their cache initialized
                    var name = _package.Name;
                    _ = _package.DisplayName;
                    _ = _package.InstalledDate;

                    _package._preloaded = true;

                    DebugLog.Append($"Preloaded {name}");

                    _package.RaisePropertyChangedOnUIThread(null);
                });
            }

            public string Name
            {
                get
                {
                    EnsurePreloadedAsync();
                    return _package._name.CurrentValue;
                }
            }

            public string DisplayName
            {
                get
                {
                    EnsurePreloadedAsync();
                    return _package._displayName.CurrentValue;
                }
            }

            public DateTimeOffset InstalledDate
            {
                get
                {
                    EnsurePreloadedAsync();
                    return _package._installedDate.CurrentValue;
                }
            }
        }

        /// <summary>
        /// Wrapper of a few properties. Fetching these from the UI thread causes just
        /// these properties to be loaded and cached (off thread),
        /// but not all of the Package properties.
        /// </summary>
        public PreloadPackageModel Preload { get; private set; }


        /// <summary>
        /// Most recent InstalledDate of all the packages
        /// </summary>
        internal static DateTimeOffset LastInstalledDateOnRefresh = DateTimeOffset.MaxValue;

        /// <summary>
        /// Indicates if this was installed after the last refresh
        /// </summary>
        public bool IsNew => _initialized && !IsNewCleared && (InstalledDate > LastInstalledDateOnRefresh);

        public bool IsNewCleared
        {
            get => _isNewCleared;
            set
            {
                _isNewCleared = value;
                RaisePropertyChangedOnUIThread();
                RaisePropertyChangedOnUIThread(nameof(IsNew));
                RaisePropertyChangedOnUIThread(nameof(BoldIfNew));
            }

        }
        bool _isNewCleared = false;

        /// <summary>
        /// UI helper to make text of new packages bold
        /// </summary>
        public FontWeight BoldIfNew => IsNew ? FontWeights.Bold : FontWeights.Normal;

        public override string ToString()
        {
            return _package == null ? base.ToString() : this.Name;
        }

        // Cache Package instances to ensure that == works
        // Deleted entries aren't cleaned up, but you'd have to delete a lot of packages from the system for that to be an issue
        static Dictionary<string, PackageModel> _modelCache = new Dictionary<string, PackageModel>();

        /// <summary>
        /// Cast a Package to a PackageModel
        /// </summary>
        public static PackageModel FromWamPackage(Package package)
        {
            // Bug workaround
            package = FixVersionBug(package);

            var id = GetCacheId(package);
            if (_modelCache.TryGetValue(id, out var model))
            {
                return model;
            }

            model = new PackageModel(package);
            _modelCache.Add(id, model);
            return model;
        }

        /// <summary>
        /// Fixes a bug in the Windows API where the Version is all zeros.
        /// </summary>
        private static Package FixVersionBug(Package wamPackage)
        {
            // bugbug: open a bug
            // When we get package in a notification, the Version is all zeros.
            // There might be other problems too, but that's the one I've noticed.
            // Re-reading the package from the system seems to fix it.

            // Not sure if there are any null checks necessary, but being safe
            var version = wamPackage?.Id?.Version;
            if (version != null && version.HasValue)
            {
                var v = version.Value;
                if (v.Major + v.Minor + v.Revision + v.Build == 0)
                {
                    try
                    {
                        // Re-read from the system
                        var fullName = wamPackage.Id.FamilyName;
                        var refindPackages = MainWindow.PackageManager.FindPackagesForUser("", fullName);

                        // If we got something, replace the original value
                        if (refindPackages != null)
                        {
                            var reloadedPackage = refindPackages.FirstOrDefault();
                            if (reloadedPackage != null)
                            {
                                wamPackage = reloadedPackage;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        DebugLog.Append($"Exception getting updated package: {e.Message}");
                    }
                }
            }

            return wamPackage;
        }



        internal static void ClearCache(PackageModel package)
        {
            var id = GetCacheId(package._package);
            _modelCache.Remove(id);
        }

        static string GetCacheId(Package wamPackage)
        {
            return wamPackage.Id.FullName;
        }

        public bool IsFullNameLoaded => _fullName.Initialized;
        public bool IsNameLoaded => _name.Initialized;


        PackageModel(Package package)
        {
            _package = package;
            Preload = new PreloadPackageModel(this);
        }

        /// <summary>
        /// Calculate all the package sizes on a background thread.
        /// Size is defined as the package directory plus the application data directory.
        /// </summary>
        static internal async void StartCalculatingSizes(IEnumerable<PackageModel> packages)
        {
            packages = packages.ToList();
            await Task.Run(() =>
            {
                // Calculate sizes (and set into the PackageModel) on the background thread
                foreach (var package in packages)
                {
                    package.StartCalculateSize(raiseChangeNotification: false);
                }
            });

            // Raise change notifications on the UI thread
            foreach (var package in packages)
            {
                package.RaisePropertyChangedOnUIThread(nameof(Size));
            }
        }


        public string ApplicationDataPath
        {
            get
            {
                if (_applicationDataPath == null)
                {
                    _applicationDataPath = "";
                    try
                    {
                        // This throws a lot
                        // Bugbug: is there a way to avoid it when it will throw?
                        // Is FileNotFoundException expected for a framework package?
                        var applicationData = ApplicationDataManager.CreateForPackageFamily(this.FamilyName);

                        // There's no API (that I can find) for the package's directory of LocalFolder, Settings, etc.
                        // So use take LocalFolder and use its parent directory
                        _applicationDataPath = Path.GetDirectoryName(applicationData.LocalFolder.Path);
                    }
                    catch (Exception e)
                    {
                        DebugLog.Append($"Failed getting ApplicationDataPath for {FullName}: {e.Message}");
                    }
                }

                return _applicationDataPath;
            }
        }
        string _applicationDataPath = null;

        public string Size
        {
            get
            {
                if (_size == "")
                {
                    StartCalculateSize(raiseChangeNotification: true);
                }

                return _size;
            }
            private set
            {
                _size = value;
                RaisePropertyChangedOnUIThread();
            }
        }
        string _size = "";

        async void StartCalculateSize(bool raiseChangeNotification)
        {
            await Task.Run(() =>
            {
                // E.g. "C:\Program Files\WindowsApps\Microsoft.Windows.Photos_2024.11120.5010.0_x64__8wekyb3d8bbwe"
                var installSize = GetDirectorySize(InstalledPath);

                // E.g. "C:\Users\mikehill\AppData\Local\Packages\Microsoft.Windows.Photos_8wekyb3d8bbwe"
                var dataSize = GetDirectorySize(ApplicationDataPath);

                var totalSize = installSize + dataSize;

                _size = $"{Utils.FormatByteSize(totalSize)} ({Utils.FormatByteSize(installSize)} install and {Utils.FormatByteSize(dataSize)} data)";
            });

            if (raiseChangeNotification)
            {
                RaisePropertyChangedOnUIThread(nameof(Size));
            }
        }

        private static long GetDirectorySize(string path)
        {
            long totalSize = 0;

            // Copilot suggested this be in a try/catch
            try
            {
                if (!Directory.Exists(path))
                {
                    return 0;
                }

                var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    totalSize += fileInfo.Length;
                }
            }
            catch (Exception e)
            {
                DebugLog.Append($"Failed calculating directory size: {e.Message}");
            }

            return totalSize;
        }

        /// <summary>
        /// Fork a thread and fine all dependents for a package.
        /// (Packages whose Dependencies collection reference this one.)
        /// </summary>
        /// <param name="packages"></param>
        static internal async void StartCalculateDependents(IEnumerable<PackageModel> packages)
        {
            // Make a copy to avoid threading issues
            packages = packages.ToList();

            await Task.Run(() =>
            {
                foreach (var package in packages)
                {
                    foreach (var dependency in package.Dependencies)
                    {
                        dependency._dependents.Add(package);
                    }
                }
            });

            foreach (var package in packages)
            {
                package._dependentsCalculated = true;
                package.RaisePropertyChangedOnUIThread(nameof(Dependents));
            }

        }
        bool _dependentsCalculated = false;
        List<PackageModel> _dependents = new List<PackageModel>();

        /// <summary>
        /// Find packages that match a regex string
        /// </summary>
        public static List<PackageModel> FindPackages(
            string search,
            IEnumerable<PackageModel> packages)
        {
            EnsurePackageModelPropertyInfos();

            List<PackageModel> finds = new();
            Regex searchRegex = new Regex(search.Trim(),
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            foreach (var package in packages)
            {
                // Search all the properties of this package
                foreach (var property in _packageModelPropertyInfos)
                {
                    var value = property.GetValue(package);

                    if (value == null)
                    {
                        continue;

                    }

                    // For string property types, we check the property value
                    if (property.PropertyType == typeof(string))
                    {
                        //if (value.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
                        if (searchRegex.IsMatch(value.ToString()))
                        {
                            finds.Add(package);
                            break;
                        }
                    }

                    // For bool property types, if the property is true, we check the _name_ of the property
                    else if (property.PropertyType == typeof(bool))
                    {
                        // if ((bool)value && property.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                        if ((bool)value && searchRegex.IsMatch(property.Name))
                        {
                            finds.Add(package);
                            break;
                        }
                    }
                }
            }

            return finds;
        }

        private static void EnsurePackageModelPropertyInfos()
        {
            if (_packageModelPropertyInfos == null)
            {
                // Cache the PropertyInfos

                // Skip some that we don't want included in a search
                string[] _ignore = { nameof(Dependencies), nameof(IsNew), nameof(IsNewCleared), nameof(Size), nameof(IsNameLoaded), nameof(IsFullNameLoaded) };

                _packageModelPropertyInfos = (from property in typeof(PackageModel).GetProperties()
                                              where !_ignore.Contains(property.Name)
                                              select property).ToList();
            }
        }
        static List<PropertyInfo> _packageModelPropertyInfos = null;



        internal void RaisePropertyChangedOnUIThread([CallerMemberName] string propertyName = null)
        {
            var args = new PropertyChangedEventArgs(propertyName);

            if (MainWindow.CurrentIsUiThread)
            {
                if (!MainWindow.IsShuttingDown)
                {
                    PropertyChanged?.Invoke(this, args);
                }
            }
            else
            {
                MainWindow.PostToUI(() => PropertyChanged?.Invoke(this, args));
            }
        }

        /// <summary>
        /// Find all boolean properties of this package that have a value of true.
        /// Returned as a list of names in a string
        /// </summary>
        public static string GetTrueBooleans(PackageModel package)
        {
            if (package == null)
            {
                return "";
            }

            EnsurePackageModelPropertyInfos();
            StringBuilder sb = null;
            foreach (var p in _packageModelPropertyInfos)
            {
                if (p.PropertyType != typeof(bool))
                {
                    continue;
                }

                var value = (bool)p.GetValue(package);
                if (value)
                {
                    if (sb == null)
                    {
                        sb = new StringBuilder();
                    }
                    else
                    {
                        sb.Append(", ");
                    }

                    sb.Append(p.Name);
                }
            }

            return sb == null ? "" : sb.ToString();
        }

        /// <summary>
        /// Package version as a string
        /// </summary>
        public string VersionString
        {
            get
            {
                var ver = this.Version;

                // bugbug: why does get-appxpackage do Major.Minor.Build.Revision rather than
                // ...Revision.Build?
                return $"{ver.Major}.{ver.Minor}.{ver.Build}.{ver.Revision}";
            }
        }

        ///// <summary>
        ///// Package Status as a string
        ///// </summary>
        public string Status => _status.Value(this);

        PackageProperty<string> _status = new(model =>
        {
            var package = model._package;

            // If we're running AllUsers, and a package for another user is removed, we won't get a notification for it
            // In that state the Package.SignatureKind throws an error. 
            // Special-case that error and invent a new status
            // bugbug: there must be a better way?
            // bugbug: this doesn't handle when a package is _added_ for another user
            // bugbug: respond to this error by re-reading the package list
            try
            {
                // bugbug
                var signatureKind = package.SignatureKind;
            }
            catch (COMException comException) when ((uint)comException.HResult == 0x80071130)
            {
                return "Removed?";
            }

            var status = package.Status;

            if (status.VerifyIsOK())
            {
                return "Ok"; // "OK" looks better, but matching get-appxpackage behavior
            }
            else
            {
                // bugbug: haven't figured out how to test this

                // If the package status isn't OK, there's a bool on PackageStatus to say what the problem is
                // Search for the one that's true and return its name
                // bugbug: Sometimes VerifyIsOK() returns false, but none of the bools are set
                // (saw this when testing a packaged app that called AddPackageByUriAsync on itself with
                // DeferRegistrationWhenPackagesAreInUse set to install an update)

                var ret = "Not Ok";
                foreach (var property in _statusProperties)
                {
                    if (property.PropertyType == typeof(bool) && (bool)property.GetValue(status))
                    {
                        ret = property.Name;
                        break;
                    }
                }

                return ret;
            }
        });

        /// <summary>
        /// Refresh the package status (triggered by a notification from the package)
        /// </summary>
        internal void UpdateStatus()
        {
            // Clear the status so that the  next time it's accessed we'll re-query it
            _status.Clear();
            RaisePropertyChangedOnUIThread(nameof(Status));
        }

        static Type _statusType = typeof(PackageStatus);
        static PropertyInfo[] _statusProperties = _statusType.GetProperties();

        static void LogException(Exception e, [CallerMemberName] string caller = null)
        {
            DebugLog.Append($"Exception in {caller} : {e}");
        }


        /// <summary>
        /// Packages that depend on this package.
        /// </summary>
        internal List<PackageModel> Dependents
        {
            get
            {
                // Dependents are calculated async on startup
                return _dependentsCalculated ? _dependents : null;
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;


        /// <summary>
        /// Cache of Package dependencies
        /// </summary>
        public IReadOnlyCollection<PackageModel> Dependencies => _dependencies.Value(this);
        PackageProperty<IReadOnlyCollection<PackageModel>> _dependencies = new(model =>
        {
            // bugbug
            // Package.Dependencies has a tendency to throw that the collection is being modified
            // during the enumeration. So we'll retry a few times
            List<Package> copy = null;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    copy = new(model._package.Dependencies);
                    break;
                }
                catch (InvalidOperationException e)
                {
                    LogException(e);
                }
            }

            if (copy == null)
            {
                return (new List<PackageModel>()).AsReadOnly();
            }

            try
            {
                return (from p in copy select PackageModel.FromWamPackage(p)).ToList();
            }
            catch (Exception e)
            {
                LogException(e);
                return (new List<PackageModel>()).AsReadOnly();
            }

        });




        ///// <summary>
        ///// Cache of Package dependencies
        ///// </summary>
        //public IReadOnlyCollection<PackageModel> Dependencies
        //{
        //    get
        //    {
        //        if (_dependencies == null)
        //        {
        //            // _package.Dependencies has a tendency to throw that the collection is being modified
        //            // during the enumeration. So we'll retry a few times
        //            List<Package> copy = null;
        //            for (int i = 0; i < 3; i++)
        //            {
        //                try
        //                {
        //                    // bugbug
        //                    copy = new(_package.Dependencies);
        //                    break;
        //                }
        //                catch (InvalidOperationException e)
        //                {
        //                    LogException(e);
        //                }
        //            }

        //            if (copy == null)
        //            {
        //                return null;
        //            }

        //            try
        //            {
        //                _dependencies = (from p in copy
        //                                 select PackageModel.FromWamPackage(p))
        //                                 .ToList();
        //            }
        //            catch (Exception e)
        //            {
        //                LogException(e);
        //                _dependencies = new();
        //            }
        //        }

        //        return _dependencies.AsReadOnly();
        //    }
        //}
        //List<PackageModel> _dependencies = null;


        /// <summary>
        /// String with a list of users that have this package
        /// </summary>
        internal string PackageUserInformation
        {
            get
            {
                if (_packageUserInformation == null)
                {
                    _packageUserInformation = "";

                    if (MainWindow.Instance.IsElevated)
                    {
                        var pm = MainWindow.PackageManager;

                        try
                        {
                            var users = pm.FindUsers(_package.Id.FullName);
                            StringBuilder sb = new();
                            var first = true;
                            foreach (var user in users)
                            {
                                if (!first)
                                {
                                    sb.AppendLine();
                                }
                                first = false;

                                {
                                    SecurityIdentifier sid = new(user.UserSecurityId);
                                    try
                                    {
                                        var account = (NTAccount)sid.Translate(typeof(NTAccount));
                                        sb.Append($"{account.Value}");
                                    }
                                    catch (IdentityNotMappedException e)
                                    {
                                        LogException(e);
                                        sb.Append($"{user.UserSecurityId}");
                                    }

                                    sb.Append($" ({user.InstallState})");

                                }
                            }

                            _packageUserInformation = sb.ToString();
                        }
                        catch (Exception e)
                        {
                            LogException(e);
                            Debug.Assert(false);
                            return default(string);
                        }
                    }
                }

                return _packageUserInformation;
            }
        }
        string _packageUserInformation;


        /// <summary>
        /// XML of the AppxManifest
        /// </summary>
        public string AppxManifestContent
        {
            get
            {
                EnsureManifestPropertiesAsync();
                return _appxManifestContent;
            }
        }
        string _appxManifestContent = null;


        /// <summary>
        /// App Execution Alias from the AppxManifest
        /// </summary>
        public string AppExecutionAlias
        {
            get
            {
                EnsureManifestPropertiesAsync();
                return _appExecutionAlias;
            }
        }
        string _appExecutionAlias = null;



        /// <summary>
        /// App Execution Alias from the AppxManifest
        /// </summary>
        public string Protocol
        {
            get
            {
                EnsureManifestPropertiesAsync();
                return _protocol;
            }
        }
        string _protocol = null;

        void EnsureManifestPropertiesAsync()
        {
            if (_appxManifestContent != null)
            {
                return;
            }

            if(!MainWindow.CurrentIsWorkerThread)
            {
                MainWindow.RunOnWorker(() => EnsureManifestPropertiesAsync());
                return;
            }

            Debug.Assert(MainWindow.CurrentIsWorkerThread);

            _appxManifestContent = "";
            _protocol = "";
            _appExecutionAlias = "";
            _capabilities = "";
            _fileTypeAssociations = "";
            _aumids = "";

            try
            {
                // bugbug: API to get this?
                var path = Path.Combine(this.InstalledPath, "AppxManifest.xml");
                if (Path.Exists(path))
                {
                    _appxManifestContent = File.ReadAllText(path);
                }
            }
            catch (Exception e)
            {
                LogException(e);
                return;
            }

            if(_appxManifestContent == "")
            {
                return;
            }

            try
            {
                XDocument doc = XDocument.Parse(AppxManifestContent);
                if (doc == null)
                {
                    return;
                }

                // Protocols

                XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
                var protocolElements = doc.Descendants(uap + "Protocol");
                StringBuilder sb = new();
                foreach (var protocolElement in protocolElements)
                {
                    var attribute = protocolElement.Attribute("Name");
                    if (attribute != null)
                    {
                        sb.Append($"{attribute.Value}: ");
                    }
                    _protocol = sb == null ? "" : sb.ToString();
                }


                // Execution Aliases

                XNamespace desktop = "http://schemas.microsoft.com/appx/manifest/desktop/windows10";
                var executionAliases = doc.Descendants(desktop + "ExecutionAlias");
                sb = null;
                foreach (var executionAlias in executionAliases)
                {
                    if (sb == null)
                    {
                        sb = new StringBuilder();
                    }
                    else
                    {
                        sb.Append(", ");
                    }

                    var attribute = executionAlias.Attribute("Alias");
                    if (attribute != null)
                    {
                        sb.Append(attribute.Value);
                    }
                    _appExecutionAlias = sb == null ? "" : sb.ToString();
                }


                //  Aumids

                XNamespace foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
                var applications = doc.Descendants(foundation + "Application");
                sb = null;
                _aumidsList = new();
                foreach (var application in applications)
                {
                    var attribute = application.Attribute("Id");
                    if (attribute != null)
                    {
                        if (sb == null)
                        {
                            sb = new StringBuilder();
                        }
                        else
                        {
                            // bugbug: add a space here to make it easier for LaunchPackage to pull out the first aumid
                            // (See the bugbug there about how it shouldn't be pulling out just the first aumid)
                            sb.AppendLine(" ");
                        }

                        var aumid = $"{FamilyName}!{attribute.Value}";
                        _aumidsList.Add(aumid);
                        sb.Append(aumid);
                    }


                    // Add in the App.Executable attribute value too
                    var executable = application.Attribute("Executable");
                    if (executable != null)
                    {
                        if (sb == null)
                        {
                            Debug.Assert(false); // Can't have an Application without an Id, right?
                            sb = new StringBuilder();
                        }

                        sb.Append($" ({executable.Value})");
                    }
                }

                _aumids = sb == null ? "" : sb.ToString();



                // Capabilities
                //
                // bugbug: The <Capability> element in <Capabilities> is in at least two different xmlns
                // So quick fix here is to just find unqualified Capability elements
                // Better would be to figure out the actual namespaces (or at least restrict to <Capabilities> elements)
                // bugbug: Microsoft.Windows.DevHome_0.1901.687.0_x64__8wekyb3d8bbwe has a <Capabilities><Capability> too

                var nameAttributes = from descendantElement in doc.Descendants()
                                     where descendantElement.Name.LocalName == "Capability"
                                     let nameAttribute = descendantElement.Attribute("Name")
                                     where nameAttribute != null
                                     orderby nameAttribute.Value
                                     select nameAttribute;

                sb = null;
                foreach (var nameAttribute in nameAttributes)
                {
                    if (sb == null)
                    {
                        sb = new StringBuilder();
                    }
                    else
                    {
                        sb.Append(", ");
                    }

                    sb.Append($"{nameAttribute.Value}");
                }

                _capabilities = sb == null ? "" : sb.ToString();


                // File Type Associations

                var fileTypes = doc.Descendants(uap + "FileType");
                sb = null;
                foreach (var fileType in fileTypes)
                {
                    if (sb == null)
                    {
                        sb = new StringBuilder();
                    }
                    else
                    {
                        sb.Append(" ");
                    }

                    sb.Append(fileType.Value);
                }
                _fileTypeAssociations = sb == null ? "" : sb.ToString();

            }
            catch (Exception e)
            {
                LogException(e);
            }

            // Bugbug: Update this to only raise for the actual properties updated here
            RaisePropertyChangedOnUIThread(null);
        }


        /// <summary>
        /// App User Model ID (as a string)
        /// </summary>
        public string Aumids
        {
            get
            {
                // bugbug: some apps have an Application.Id but don't show up in GetAppListEntries() API
                // So read the XML instead
                // E.g. c5e2524a-ea46-4f67-841f-6a9465d9d515

                EnsureManifestPropertiesAsync();

                return _aumids;
            }
        }
        string _aumids = null;

        /// <summary>
        /// App User Model ID (as a list of strings)
        /// </summary>
        public IReadOnlyList<string> AumidsList
        {
            get
            {
                EnsureManifestPropertiesAsync();
                return _aumidsList;
            }
        }
        List<string> _aumidsList = null;


        /// <summary>
        /// Capabilities from the appx manifest
        /// </summary>
        public string Capabilities
        {
            get
            {
                EnsureManifestPropertiesAsync();
                return _capabilities;
            }
        }
        string _capabilities = null;

        /// <summary>
        /// FTAs from the appx manifest
        /// </summary>
        public string FileTypeAssociations
        {
            get
            {
                EnsureManifestPropertiesAsync();
                return _fileTypeAssociations;
            }
        }
        string _fileTypeAssociations = null;

        public string Description => _description.Value(this);
        PackageProperty<string> _description = new(model =>
        {
            // bugbug: Resource packages don't have descriptions?
            // This saves having to catch an exception
            if (model._package.IsResourcePackage)
            {
                return "";
            }
            else
            {
                return model._package.Description;
            }
        });

        public string DisplayName => _displayName.Value(this);
        PackageProperty<string> _displayName = new(model =>
        {
            // bugbug: Resource packages don't have display names?
            // This saves having to catch an exception
            if (model._package.IsResourcePackage)
            {
                return model._package.Id.Name;
            }
            else
            {
                return model._package.DisplayName;
            }
        });

        public Uri Logo => _logo.Value(this);
        PackageProperty<Uri> _logo = new(model =>
        {
            // bugbug: Resource packages don't have logos?
            // This saves having to catch an exception
            if (model._package.IsResourcePackage)
            {
                return null;
            }
            else
            {
                return model._package.Logo;
            }
        });
    }
}
