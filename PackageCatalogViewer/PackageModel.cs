using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;

namespace PackageCatalogViewer
{
    public partial class PackageModel : INotifyPropertyChanged
    {
        Package _package;

        static Dictionary<string, PackageModel> _modelCache = new Dictionary<string, PackageModel>();

        public static implicit operator PackageModel(Package package)
        {
            if (_modelCache.TryGetValue(package.Id.FullName, out var model))
            {
                return model;
            }

            model = new PackageModel(package);
            _modelCache.Add(package.Id.FullName, model);
            return model;
        }

        PackageModel(Package package)
        {
            _package = package;
        }

        bool _dependentsCalculated = false;
        List<PackageModel> _dependents = new List<PackageModel>();
        static internal async void StartCalculateDependents(IEnumerable<PackageModel> packages)
        {
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

        static List<PropertyInfo> _propertyInfos = null;
        public static ObservableCollection<PackageModel> Find(
            string search,
            IEnumerable<PackageModel> packages)
        {
            if (_propertyInfos == null)
            {
                string[] _ignore = { "InstallDate", "Dependencies" };
                _propertyInfos = (from property in typeof(PackageModel).GetProperties()
                                  where !_ignore.Contains(property.Name)
                                  where property.PropertyType != typeof(bool)
                                  select property).ToList();

                foreach(var p in _propertyInfos)
                {
                    Debug.WriteLine(p.Name);
                }

            }


            ObservableCollection<PackageModel> finds = new();

            foreach (var package in packages)
            {
                foreach (var property in _propertyInfos)
                {
                    var value = property.GetValue(package);
                    if (value != null && value.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
                    {
                        finds.Add(package);
                        break;
                    }
                }
            }

            return finds;
        }

        void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        static List<PropertyInfo> _booleanPropertyInfos = null;
        public static string GetTrueBooleans(PackageModel package)
        {
            if (package == null)
            {
                return "";
            }

            if (_booleanPropertyInfos == null)
            {
                string[] _ignore = { "InstallDate", "Dependencies" };
                _booleanPropertyInfos = (from property in typeof(PackageModel).GetProperties()
                                         where !_ignore.Contains(property.Name)
                                         where property.PropertyType == typeof(bool)
                                         select property).ToList();
            }

            StringBuilder sb = null;
            foreach (var p in _booleanPropertyInfos)
            {
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

        public string VersionString
        {
            get
            {
                var ver = _package.Id.Version;
                return $"{ver.Major}.{ver.Minor}.{ver.Revision}.{ver.Build}";
            }
        }


        string _status;
        public string Status
        {
            get
            {
                try
                {
                    var status = _package.Status;

                    var statusType = status.GetType();
                    var properties = statusType.GetProperties();

                    if (status.VerifyIsOK())
                    {
                        _status = "Ok"; // "OK" looks better, but matching get-appxpackage behavior
                    }
                    else
                    {
                        // bugbug: haven't figured out how to test this

                        _status = "Not Ok";
                        foreach (var property in properties)
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




        internal List<PackageModel> Dependents
        {
            get
            {
                return _dependentsCalculated ? _dependents : null;
            }
        }

        List<PackageModel> _dependencies = null;

        public event PropertyChangedEventHandler PropertyChanged;

        public IReadOnlyCollection<PackageModel> Dependencies
        {
            get
            {
                if (_dependencies == null)
                {
                    try
                    {
                        List<Package> copy = new(_package.Dependencies);
                        _dependencies = (from p in copy
                                         select (PackageModel)p)
                                         .ToList();
                    }
                    catch (Exception)
                    {
                        Debug.Assert(false);
                        _dependencies = new List<PackageModel>();
                    }
                }

                return _dependencies.AsReadOnly();
            }
        }

        string _packageUserInformation;
        internal string PackageUserInformation
        {
            get
            {
                if (_packageUserInformation == null)
                {
                    _packageUserInformation = "";

                    if (MainWindow.Instance.IsElevated)
                    {
                        var pm = MainWindow._packageManager;

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
                                    SecurityIdentifier sid = new (user.UserSecurityId);
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
    }
}
