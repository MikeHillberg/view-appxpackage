using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.ApplicationModel;
using Windows.Storage;

namespace PackageCatalogViewer
{
    public class PackageModel
    {
        Package _package;

        public static implicit operator PackageModel(Package package)
        {
            return new PackageModel(package);
        }

        PackageModel(Package package)
        {
            _package = package;
        }

        List<PackageModel> _dependencies = null;
        public IReadOnlyCollection<PackageModel> Dependencies
        {
            get
            {
                if (_dependencies == null)
                {
                    try
                    {
                        //return _package.Dependencies;
                        _dependencies = (from p in _package.Dependencies
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


        public String Description
        {
            get
            {
                try
                {
                    if(_package.IsResourcePackage)
                    {
                        return default(string);
                    }    
                    return _package.Description;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(String);
                }
            }
        }


        public String DisplayName
        {
            get
            {
                try
                {
                    if(_package.IsResourcePackage)
                    {
                        return _package.Id.Name;
                    }
                    return _package.DisplayName;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(String);
                }
            }
        }


        public StorageFolder EffectiveExternalLocation
        {
            get
            {
                try
                {
                    return _package.EffectiveExternalLocation;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(StorageFolder);
                }
            }
        }


        public String EffectiveExternalPath
        {
            get
            {
                try
                {
                    return _package.EffectiveExternalPath;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(String);
                }
            }
        }


        public StorageFolder EffectiveLocation
        {
            get
            {
                try
                {
                    return _package.EffectiveLocation;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(StorageFolder);
                }
            }
        }


        public String EffectivePath
        {
            get
            {
                try
                {
                    return _package.EffectivePath;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(String);
                }
            }
        }


        public PackageId Id
        {
            get
            {
                try
                {
                    return _package.Id;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(PackageId);
                }
            }
        }


        public DateTimeOffset InstallDate
        {
            get
            {
                try
                {
                    return _package.InstallDate;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(DateTimeOffset);
                }
            }
        }


        public DateTimeOffset InstalledDate
        {
            get
            {
                try
                {
                    return _package.InstalledDate;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(DateTimeOffset);
                }
            }
        }


        public StorageFolder InstalledLocation
        {
            get
            {
                try
                {
                    return _package.InstalledLocation;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(StorageFolder);
                }
            }
        }


        public String InstalledPath
        {
            get
            {
                try
                {
                    return _package.InstalledPath;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(String);
                }
            }
        }


        public Boolean IsBundle
        {
            get
            {
                try
                {
                    return _package.IsBundle;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(Boolean);
                }
            }
        }


        public Boolean IsDevelopmentMode
        {
            get
            {
                try
                {
                    return _package.IsDevelopmentMode;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(Boolean);
                }
            }
        }


        public Boolean IsFramework
        {
            get
            {
                try
                {
                    return _package.IsFramework;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(Boolean);
                }
            }
        }


        public Boolean IsOptional
        {
            get
            {
                try
                {
                    return _package.IsOptional;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(Boolean);
                }
            }
        }


        public Boolean IsResourcePackage
        {
            get
            {
                try
                {
                    return _package.IsResourcePackage;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(Boolean);
                }
            }
        }


        public Boolean IsStub
        {
            get
            {
                try
                {
                    return _package.IsStub;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(Boolean);
                }
            }
        }


        public Uri Logo
        {
            get
            {
                try
                {
                    return _package.Logo;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(Uri);
                }
            }
        }


        public StorageFolder MachineExternalLocation
        {
            get
            {
                try
                {
                    return _package.MachineExternalLocation;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(StorageFolder);
                }
            }
        }


        public String MachineExternalPath
        {
            get
            {
                try
                {
                    return _package.MachineExternalPath;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(String);
                }
            }
        }


        public StorageFolder MutableLocation
        {
            get
            {
                try
                {
                    return _package.MutableLocation;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(StorageFolder);
                }
            }
        }


        public String MutablePath
        {
            get
            {
                try
                {
                    return _package.MutablePath;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(String);
                }
            }
        }


        public string VersionString
        {
            get
            {
                var ver = _package.Id.Version;
                return $"{ver.Major}.{ver.Minor}.{ver.Revision}.{ver.Build}";
            }
        }

        public String PublisherDisplayName
        {
            get
            {
                try
                {
                    if(_package.IsResourcePackage)
                    {
                        return _package.Id.Publisher;
                    }
                    return _package.PublisherDisplayName;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(String);
                }
            }
        }


        public PackageSignatureKind SignatureKind
        {
            get
            {
                try
                {
                    return _package.SignatureKind;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(PackageSignatureKind);
                }
            }
        }


        //public String SourceUriSchemeName
        //{
        //    get
        //    {
        //        try
        //        {
        //            return _package.SourceUriSchemeName;
        //        }
        //        catch (Exception)
        //        {
        //            Debug.Assert(false);
        //            return default(String);
        //        }
        //    }
        //}


        public PackageStatus Status
        {
            get
            {
                try
                {
                    return _package.Status;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(PackageStatus);
                }
            }
        }


        public StorageFolder UserExternalLocation
        {
            get
            {
                try
                {
                    return _package.UserExternalLocation;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(StorageFolder);
                }
            }
        }


        public String UserExternalPath
        {
            get
            {
                try
                {
                    return _package.UserExternalPath;
                }
                catch (Exception)
                {
                    Debug.Assert(false);
                    return default(String);
                }
            }
        }

    }
}
