using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel;

namespace ViewAppxPackage
{
    /// <summary>
    /// Wrapper around Package class
    /// </summary>
    public partial class PackageModel : INotifyPropertyChanged
    {
        Package _package;

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
            var id = GetVersionIndependentId(package);
            if (_modelCache.TryGetValue(id, out var model))
            {
                return model;
            }

            model = new PackageModel(package);
            _modelCache.Add(id, model);
            return model;
        }

        internal static void ClearCache(PackageModel package)
        {
            var id = GetVersionIndependentId(package._package);
            _modelCache.Remove(id);
        }

        static string GetVersionIndependentId(Package wamPackage)
        {
            return $"{wamPackage.Id.Name}%{wamPackage.Id.ResourceId}";
        }


        PackageModel(Package package)
        {
            _package = package;
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
                package.RaisePropertyChanged(nameof(Dependents));
            }

        }
        bool _dependentsCalculated = false;
        List<PackageModel> _dependents = new List<PackageModel>();

        /// <summary>
        /// Find packages that match a regex string
        /// </summary>
        public static List<PackageModel> FindPackages(
            string searchRegex,
            IEnumerable<PackageModel> packages)
        {
            if (_packageModelPropertyInfos == null)
            {
                // Cache the PropertyInfos

                // Skip some that we don't want included in a search
                string[] _ignore = { "InstallDate", "Dependencies" };

                _packageModelPropertyInfos = (from property in typeof(PackageModel).GetProperties()
                                              where !_ignore.Contains(property.Name)
                                              select property).ToList();
            }


            List<PackageModel> finds = new();

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
                        if (value.ToString().Contains(searchRegex, StringComparison.OrdinalIgnoreCase))
                        {
                            finds.Add(package);
                            break;
                        }
                    }

                    // For bool property types, if the property is true, we check the _name_ of the property
                    else if (property.PropertyType == typeof(bool))
                    {
                        if ((bool)value && property.Name.Contains(searchRegex, StringComparison.OrdinalIgnoreCase))
                        {
                            finds.Add(package);
                            break;
                        }
                    }
                }
            }

            return finds;
        }
        static List<PropertyInfo> _packageModelPropertyInfos = null;

        void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
                var ver = _package.Id.Version;
                return $"{ver.Major}.{ver.Minor}.{ver.Revision}.{ver.Build}";
            }
        }


        /// <summary>
        /// Package Status as a string
        /// </summary>
        public string Status
        {
            get
            {
                try
                {
                    // If we're running AllUsers, and a package for another user is removed, we won't get a notification for it
                    // In that state the Package.SignatureKind throws an error. 
                    // Special-case that error and invent a new status
                    // bugbug: there must be a better way?
                    // bugbug: this doesn't handle when a package is _added_ for another user
                    // bugbug: respond to this error by re-reading the package list
                    try
                    {
                        var signatureKind = _package.SignatureKind;
                    }
                    catch (COMException comException) when ((uint)comException.HResult == 0x80071130)
                    {
                        return "Removed?";
                    }

                    var status = _package.Status;

                    if (status.VerifyIsOK())
                    {
                        _status = "Ok"; // "OK" looks better, but matching get-appxpackage behavior
                    }
                    else
                    {
                        // bugbug: haven't figured out how to test this

                        // If the package status isn't OK, there's a bool on PackageStatus to say what the problem is
                        // Search for the one that's true and return its name
                        // bugbug: Sometimes VerifyIsOK() returns false, but none of the bools are set
                        // (saw this when testing a packaged app that called AddPackageByUriAsync on itself with
                        // DeferRegistrationWhenPackagesAreInUse set to install an update)

                        _status = "Not Ok";
                        foreach (var property in _statusProperties)
                        {
                            if (property.PropertyType == typeof(bool) && (bool)property.GetValue(status))
                            {
                                _status = property.Name;
                                break;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(string);
                }

                return _status;
            }
        }
        string _status;
        static Type _statusType = typeof(PackageStatus);
        static PropertyInfo[] _statusProperties = _statusType.GetProperties();


        /// <summary>
        /// Packages that depend on this package.
        /// </summary>
        internal List<PackageModel> Dependents
        {
            get
            {
                return _dependentsCalculated ? _dependents : null;
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Cache of Package dependencies
        /// </summary>
        public IReadOnlyCollection<PackageModel> Dependencies
        {
            get
            {
                if (_dependencies == null)
                {
                    // _package.Dependencies has a tendency to throw that the collection is being modified
                    // during the enumeration. So we'll retry a few times
                    List<Package> copy = null;
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            copy = new(_package.Dependencies);
                            break;
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }

                    if (copy == null)
                    {
                        return null;
                    }

                    try
                    {
                        _dependencies = (from p in copy
                                         select PackageModel.FromWamPackage(p))
                                         .ToList();
                    }
                    catch (Exception)
                    {
                        _dependencies = new();
                    }
                }

                return _dependencies.AsReadOnly();
            }
        }
        List<PackageModel> _dependencies = null;


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
                                    catch (IdentityNotMappedException)
                                    {
                                        sb.Append($"{user.UserSecurityId}");
                                    }

                                    sb.Append($" ({user.InstallState})");

                                }
                            }

                            _packageUserInformation = sb.ToString();
                        }
                        catch (Exception)
                        {
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
                EnsureManifestProperties();
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
                EnsureManifestProperties();
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
                EnsureManifestProperties();
                return _protocol;
            }
        }
        string _protocol = null;

        void EnsureManifestProperties()
        {
            if (_appxManifestContent != null)
            {
                return;
            }

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
            catch (Exception)
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

                        sb.Append($"{FamilyName}!{attribute.Value}");
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
                var capabilities = doc.Descendants().Where(e => e.Name.LocalName == "Capability");
                sb = null;
                foreach (var capability in capabilities)
                {
                    var attribute = capability.Attribute("Name");
                    if (attribute != null)
                    {
                        if (sb == null)
                        {
                            sb = new StringBuilder();
                        }
                        else
                        {
                            sb.Append(", ");
                        }

                        sb.Append($"{attribute.Value}");
                    }
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
            catch (Exception)
            {
            }
        }


        /// <summary>
        /// App User Model ID
        /// </summary>
        public string Aumids
        {
            get
            {
                // bugbug: some apps have an Application.Id but don't show up in GetAppListEntries() API
                // So read the XML instead
                // E.g. c5e2524a-ea46-4f67-841f-6a9465d9d515

                EnsureManifestProperties();

                //if (_aumid == null)
                //{
                //    _aumid = "";

                //    try
                //    {
                //        var entries = _package.GetAppListEntries();
                //        if (entries.Count > 0)
                //        {
                //            StringBuilder sb = null;
                //            foreach (var entry in entries)
                //            {
                //                if (sb == null)
                //                {
                //                    sb = new StringBuilder();
                //                }
                //                else
                //                {
                //                    sb.Append(", ");
                //                }

                //                sb.Append(entry.AppUserModelId);
                //            }
                //            _aumid = sb.ToString();
                //        }
                //    }
                //    catch (Exception)
                //    {
                //        _aumid = "";
                //    }
                //}

                return _aumids;
            }
        }
        string _aumids = null;

        /// <summary>
        /// Capabilities from the appx manifest
        /// </summary>
        public string Capabilities
        {
            get
            {
                EnsureManifestProperties();
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
                EnsureManifestProperties();
                return _fileTypeAssociations;
            }
        }
        string _fileTypeAssociations = null;

    }
}
