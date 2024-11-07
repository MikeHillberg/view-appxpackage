using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.System;

namespace PackageCatalogViewer
{
    public partial class PackageModel
    {
        // Removed Dependencies, Status, Current
        // bugbug: SourceUriSchemeName is not available in the target OS version
        // Try on DisplayName, Logo, PublisherDisplayName


        //public IReadOnlyList<Package> Dependencies
        //{
        //    get
        //    {
        //        if (_dependenciesCalculated)
        //        {
        //            try
        //            {
        //                _dependencies = _package.Dependencies;
        //            }
        //            catch (Exception)
        //            {
        //                _dependencies = default(IReadOnlyList<Package>);
        //            }
        //            _dependenciesCalculated = true;
        //        }
        //        return _dependencies;
        //    }
        //}
        //IReadOnlyList<Package> _dependencies;
        //bool _dependenciesCalculated;


        public String Description
        {
            get
            {
                if (!_descriptionCalculated)
                {
                    try
                    {
                        if (_package.IsResourcePackage)
                        {
                            _description = _package.Description;
                        }
                        else
                        {
                            _description = "";
                        }
                    }
                    catch (Exception)
                    {
                        _description = default(String);
                    }
                    _descriptionCalculated = true;
                }
                return _description;
            }
        }
        String _description;
        bool _descriptionCalculated;


        public String DisplayName
        {
            get
            {
                if (!_displaynameCalculated)
                {
                    try
                    {
                        if(_package.IsResourcePackage)
                        {
                            _displayname = _package.Id.Name;
                        }
                        else
                        {
                            _displayname = _package.DisplayName;
                        }
                    }
                    catch (Exception)
                    {
                        _displayname = default(String);
                    }
                    _displaynameCalculated = true;
                }
                return _displayname;
            }
        }
        String _displayname;
        bool _displaynameCalculated;


        //public StorageFolder EffectiveExternalLocation
        //{
        //    get
        //    {
        //        if (!_effectiveexternallocationCalculated)
        //        {
        //            try
        //            {
        //                _effectiveexternallocation = _package.EffectiveExternalLocation;
        //            }
        //            catch (Exception)
        //            {
        //                _effectiveexternallocation = default(StorageFolder);
        //            }
        //            _effectiveexternallocationCalculated = true;
        //        }
        //        return _effectiveexternallocation;
        //    }
        //}
        //StorageFolder _effectiveexternallocation;
        //bool _effectiveexternallocationCalculated;


        public String EffectiveExternalPath
        {
            get
            {
                if (!_effectiveexternalpathCalculated)
                {
                    try
                    {
                        _effectiveexternalpath = _package.EffectiveExternalPath;
                    }
                    catch (Exception)
                    {
                        _effectiveexternalpath = default(String);
                    }
                    _effectiveexternalpathCalculated = true;
                }
                return _effectiveexternalpath;
            }
        }
        String _effectiveexternalpath;
        bool _effectiveexternalpathCalculated;


        //public StorageFolder EffectiveLocation
        //{
        //    get
        //    {
        //        if (!_effectivelocationCalculated)
        //        {
        //            try
        //            {
        //                _effectivelocation = _package.EffectiveLocation;
        //            }
        //            catch (Exception)
        //            {
        //                _effectivelocation = default(StorageFolder);
        //            }
        //            _effectivelocationCalculated = true;
        //        }
        //        return _effectivelocation;
        //    }
        //}
        //StorageFolder _effectivelocation;
        //bool _effectivelocationCalculated;


        public String EffectivePath
        {
            get
            {
                if (!_effectivepathCalculated)
                {
                    try
                    {
                        _effectivepath = _package.EffectivePath;
                    }
                    catch (Exception)
                    {
                        _effectivepath = default(String);
                    }

                    if(_effectivepath == this.InstalledPath)
                    {
                        _effectivepath = "";
                    }

                    _effectivepathCalculated = true;
                }
                return _effectivepath;
            }
        }
        String _effectivepath;
        bool _effectivepathCalculated;


        public PackageId Id
        {
            get
            {
                if (!_idCalculated)
                {
                    try
                    {
                        _id = _package.Id;
                    }
                    catch (Exception)
                    {
                        _id = default(PackageId);
                    }
                    _idCalculated = true;
                }
                return _id;
            }
        }
        PackageId _id;
        bool _idCalculated;


        public DateTimeOffset InstallDate
        {
            get
            {
                if (!_installdateCalculated)
                {
                    try
                    {
                        _installdate = _package.InstallDate;
                    }
                    catch (Exception)
                    {
                        _installdate = default(DateTimeOffset);
                    }
                    _installdateCalculated = true;
                }
                return _installdate;
            }
        }
        DateTimeOffset _installdate;
        bool _installdateCalculated;


        public DateTimeOffset InstalledDate
        {
            get
            {
                if (!_installeddateCalculated)
                {
                    try
                    {
                        _installeddate = _package.InstalledDate;
                    }
                    catch (Exception)
                    {
                        _installeddate = default(DateTimeOffset);
                    }
                    _installeddateCalculated = true;
                }
                return _installeddate;
            }
        }
        DateTimeOffset _installeddate;
        bool _installeddateCalculated;


        //public StorageFolder InstalledLocation
        //{
        //    get
        //    {
        //        if (!_installedlocationCalculated)
        //        {
        //            try
        //            {
        //                _installedlocation = _package.InstalledLocation;
        //            }
        //            catch (Exception)
        //            {
        //                _installedlocation = default(StorageFolder);
        //            }
        //            _installedlocationCalculated = true;
        //        }
        //        return _installedlocation;
        //    }
        //}
        //StorageFolder _installedlocation;
        //bool _installedlocationCalculated;


        public String InstalledPath
        {
            get
            {
                if (!_installedpathCalculated)
                {
                    try
                    {
                        _installedpath = _package.InstalledPath;
                    }
                    catch (Exception)
                    {
                        _installedpath = default(String);
                    }
                    _installedpathCalculated = true;
                }
                return _installedpath;
            }
        }
        String _installedpath;
        bool _installedpathCalculated;


        public Boolean IsBundle
        {
            get
            {
                if (!_isbundleCalculated)
                {
                    try
                    {
                        _isbundle = _package.IsBundle;
                    }
                    catch (Exception)
                    {
                        _isbundle = default(Boolean);
                    }
                    _isbundleCalculated = true;
                }
                return _isbundle;
            }
        }
        Boolean _isbundle;
        bool _isbundleCalculated;


        public Boolean IsDevelopmentMode
        {
            get
            {
                if (!_isdevelopmentmodeCalculated)
                {
                    try
                    {
                        _isdevelopmentmode = _package.IsDevelopmentMode;
                    }
                    catch (Exception)
                    {
                        _isdevelopmentmode = default(Boolean);
                    }
                    _isdevelopmentmodeCalculated = true;
                }
                return _isdevelopmentmode;
            }
        }
        Boolean _isdevelopmentmode;
        bool _isdevelopmentmodeCalculated;


        public Boolean IsFramework
        {
            get
            {
                if (!_isframeworkCalculated)
                {
                    try
                    {
                        _isframework = _package.IsFramework;
                    }
                    catch (Exception)
                    {
                        _isframework = default(Boolean);
                    }
                    _isframeworkCalculated = true;
                }
                return _isframework;
            }
        }
        Boolean _isframework;
        bool _isframeworkCalculated;


        public Boolean IsOptional
        {
            get
            {
                if (!_isoptionalCalculated)
                {
                    try
                    {
                        _isoptional = _package.IsOptional;
                    }
                    catch (Exception)
                    {
                        _isoptional = default(Boolean);
                    }
                    _isoptionalCalculated = true;
                }
                return _isoptional;
            }
        }
        Boolean _isoptional;
        bool _isoptionalCalculated;


        public Boolean IsResourcePackage
        {
            get
            {
                if (!_isresourcepackageCalculated)
                {
                    try
                    {
                        _isresourcepackage = _package.IsResourcePackage;
                    }
                    catch (Exception)
                    {
                        _isresourcepackage = default(Boolean);
                    }
                    _isresourcepackageCalculated = true;
                }
                return _isresourcepackage;
            }
        }
        Boolean _isresourcepackage;
        bool _isresourcepackageCalculated;


        public Boolean IsStub
        {
            get
            {
                if (!_isstubCalculated)
                {
                    try
                    {
                        _isstub = _package.IsStub;
                    }
                    catch (Exception)
                    {
                        _isstub = default(Boolean);
                    }
                    _isstubCalculated = true;
                }
                return _isstub;
            }
        }
        Boolean _isstub;
        bool _isstubCalculated;


        public Uri Logo
        {
            get
            {
                if (!_logoCalculated)
                {
                    try
                    {
                        if (_package.IsResourcePackage)
                        {
                            _logo = null;
                        }
                        else
                        {
                            _logo = _package.Logo;
                        }
                    }
                    catch (Exception)
                    {
                        _logo = default(Uri);
                    }
                    _logoCalculated = true;
                }
                return _logo;
            }
        }
        Uri _logo;
        bool _logoCalculated;


        //public StorageFolder MachineExternalLocation
        //{
        //    get
        //    {
        //        if (!_machineexternallocationCalculated)
        //        {
        //            try
        //            {
        //                _machineexternallocation = _package.MachineExternalLocation;
        //            }
        //            catch (Exception)
        //            {
        //                _machineexternallocation = default(StorageFolder);
        //            }
        //            _machineexternallocationCalculated = true;
        //        }
        //        return _machineexternallocation;
        //    }
        //}
        //StorageFolder _machineexternallocation;
        //bool _machineexternallocationCalculated;


        public String MachineExternalPath
        {
            get
            {
                if (!_machineexternalpathCalculated)
                {
                    try
                    {
                        _machineexternalpath = _package.MachineExternalPath;
                    }
                    catch (Exception)
                    {
                        _machineexternalpath = default(String);
                    }
                    _machineexternalpathCalculated = true;
                }
                return _machineexternalpath;
            }
        }
        String _machineexternalpath;
        bool _machineexternalpathCalculated;


        //public StorageFolder MutableLocation
        //{
        //    get
        //    {
        //        if (!_mutablelocationCalculated)
        //        {
        //            try
        //            {
        //                _mutablelocation = _package.MutableLocation;
        //            }
        //            catch (Exception)
        //            {
        //                _mutablelocation = default(StorageFolder);
        //            }
        //            _mutablelocationCalculated = true;
        //        }
        //        return _mutablelocation;
        //    }
        //}
        //StorageFolder _mutablelocation;
        //bool _mutablelocationCalculated;


        public String MutablePath
        {
            get
            {
                if (!_mutablepathCalculated)
                {
                    try
                    {
                        _mutablepath = _package.MutablePath;
                    }
                    catch (Exception)
                    {
                        _mutablepath = default(String);
                    }
                    _mutablepathCalculated = true;
                }
                return _mutablepath;
            }
        }
        String _mutablepath;
        bool _mutablepathCalculated;


        public String PublisherDisplayName
        {
            get
            {
                if (!_publisherdisplaynameCalculated)
                {
                    try
                    {
                        _publisherdisplayname = _package.PublisherDisplayName;
                    }
                    catch (Exception)
                    {
                        _publisherdisplayname = default(String);
                    }
                    _publisherdisplaynameCalculated = true;
                }
                return _publisherdisplayname;
            }
        }
        String _publisherdisplayname;
        bool _publisherdisplaynameCalculated;


        public PackageSignatureKind SignatureKind
        {
            get
            {
                if (!_signaturekindCalculated)
                {
                    try
                    {
                        _signaturekind = _package.SignatureKind;
                    }
                    catch (Exception)
                    {
                        _signaturekind = default(PackageSignatureKind);
                    }
                    _signaturekindCalculated = true;
                }
                return _signaturekind;
            }
        }
        PackageSignatureKind _signaturekind;
        bool _signaturekindCalculated;


        //public String SourceUriSchemeName
        //{
        //    get
        //    {
        //        if (_sourceurischemenameCalculated)
        //        {
        //            try
        //            {
        //                _sourceurischemename = _package.SourceUriSchemeName;
        //            }
        //            catch (Exception)
        //            {
        //                _sourceurischemename = default(String);
        //            }
        //            _sourceurischemenameCalculated = true;
        //        }
        //        return _sourceurischemename;
        //    }
        //}
        //String _sourceurischemename;
        //bool _sourceurischemenameCalculated;


        //public PackageStatus Status
        //{
        //    get
        //    {
        //        if (_statusCalculated)
        //        {
        //            try
        //            {
        //                _status = _package.Status;
        //            }
        //            catch (Exception)
        //            {
        //                _status = default(PackageStatus);
        //            }
        //            _statusCalculated = true;
        //        }
        //        return _status;
        //    }
        //}
        //PackageStatus _status;
        //bool _statusCalculated;


        //public StorageFolder UserExternalLocation
        //{
        //    get
        //    {
        //        if (!_userexternallocationCalculated)
        //        {
        //            try
        //            {
        //                _userexternallocation = _package.UserExternalLocation;
        //            }
        //            catch (Exception)
        //            {
        //                _userexternallocation = default(StorageFolder);
        //            }
        //            _userexternallocationCalculated = true;
        //        }
        //        return _userexternallocation;
        //    }
        //}
        //StorageFolder _userexternallocation;
        //bool _userexternallocationCalculated;


        public String UserExternalPath
        {
            get
            {
                if (!_userexternalpathCalculated)
                {
                    try
                    {
                        _userexternalpath = _package.UserExternalPath;
                    }
                    catch (Exception)
                    {
                        _userexternalpath = default(String);
                    }
                    _userexternalpathCalculated = true;
                }
                return _userexternalpath;
            }
        }
        String _userexternalpath;
        bool _userexternalpathCalculated;




        public ProcessorArchitecture Architecture
        {
            get
            {
                if (!_architectureCalculated)
                {
                    try
                    {
                        _architecture = _package.Id.Architecture;
                    }
                    catch (Exception)
                    {
                        _architecture = default(ProcessorArchitecture);
                    }
                    _architectureCalculated = true;
                }
                return _architecture;
            }
        }
        ProcessorArchitecture _architecture;
        bool _architectureCalculated;


        public String Author
        {
            get
            {
                if (!_authorCalculated)
                {
                    try
                    {
                        _author = _package.Id.Author;
                    }
                    catch (Exception)
                    {
                        _author = default(String);
                    }
                    _authorCalculated = true;
                }
                return _author;
            }
        }
        String _author;
        bool _authorCalculated;


        public String FamilyName
        {
            get
            {
                if (!_familynameCalculated)
                {
                    try
                    {
                        _familyname = _package.Id.FamilyName;
                    }
                    catch (Exception)
                    {
                        _familyname = default(String);
                    }
                    _familynameCalculated = true;
                }
                return _familyname;
            }
        }
        String _familyname;
        bool _familynameCalculated;


        public String FullName
        {
            get
            {
                if (!_fullnameCalculated)
                {
                    try
                    {
                        _fullname = _package.Id.FullName;
                    }
                    catch (Exception)
                    {
                        _fullname = default(String);
                    }
                    _fullnameCalculated = true;
                }
                return _fullname;
            }
        }
        String _fullname;
        bool _fullnameCalculated;


        public String Name
        {
            get
            {
                if (!_nameCalculated)
                {
                    try
                    {
                        _name = _package.Id.Name;
                    }
                    catch (Exception)
                    {
                        _name = default(String);
                    }
                    _nameCalculated = true;
                }
                return _name;
            }
        }
        String _name;
        bool _nameCalculated;


        public String ProductId
        {
            get
            {
                if (!_productidCalculated)
                {
                    try
                    {
                        _productid = _package.Id.ProductId;
                    }
                    catch (Exception)
                    {
                        _productid = default(String);
                    }
                    _productidCalculated = true;
                }
                return _productid;
            }
        }
        String _productid;
        bool _productidCalculated;


        public String Publisher
        {
            get
            {
                if (!_publisherCalculated)
                {
                    try
                    {
                        _publisher = _package.Id.Publisher;
                    }
                    catch (Exception)
                    {
                        _publisher = default(String);
                    }
                    _publisherCalculated = true;
                }
                return _publisher;
            }
        }
        String _publisher;
        bool _publisherCalculated;


        public String PublisherId
        {
            get
            {
                if (!_publisheridCalculated)
                {
                    try
                    {
                        _publisherid = _package.Id.PublisherId;
                    }
                    catch (Exception)
                    {
                        _publisherid = default(String);
                    }
                    _publisheridCalculated = true;
                }
                return _publisherid;
            }
        }
        String _publisherid;
        bool _publisheridCalculated;


        public String ResourceId
        {
            get
            {
                if (!_resourceidCalculated)
                {
                    try
                    {
                        _resourceid = _package.Id.ResourceId;
                    }
                    catch (Exception)
                    {
                        _resourceid = default(String);
                    }
                    _resourceidCalculated = true;
                }
                return _resourceid;
            }
        }
        String _resourceid;
        bool _resourceidCalculated;


        public PackageVersion Version
        {
            get
            {
                if (!_versionCalculated)
                {
                    try
                    {
                        _version = _package.Id.Version;
                    }
                    catch (Exception)
                    {
                        _version = default(PackageVersion);
                    }
                    _versionCalculated = true;
                }
                return _version;
            }
        }
        PackageVersion _version;
        bool _versionCalculated;



    }
}
