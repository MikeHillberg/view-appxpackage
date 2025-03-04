using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel.Core;
using Windows.Storage.Streams;
using Windows.Foundation;
using Microsoft.UI.Xaml.Media.Imaging;


namespace ViewAppxPackage
{
    /// <summary>
    /// Model for the
    /// [AppListEntry](https://docs.microsoft.com/uwp/api/Windows.ApplicationModel.Core.AppListEntry)
    /// class, which represents apps in the package (the Applications tag).
    /// There are interesting app properties not in the Win10 API,
    /// so some of the properties on this class from the appx manifest.
    /// </summary>
    public class AppListEntryModel : INotifyPropertyChanged
    {
        AppListEntry _appListEntry;

        public AppListEntryModel(AppListEntry entry)
        {
            This = this;
            _appListEntry = entry;

            Debug.Assert(MyThreading.CurrentIsWorkerThread);

            // Load properties that are in the AppListEntry (wrapped in a try/catch)
            RunCatch(() => AppUserModelId = _appListEntry.AppUserModelId);
            RunCatch(() => Description = _appListEntry.DisplayInfo.Description);
            RunCatch(() => DisplayName = _appListEntry.DisplayInfo.DisplayName);
            RunCatch(() => _logoRef = _appListEntry.DisplayInfo.GetLogo(new Size(50, 50)));

            // Calculate the ID (PRAID)
            Id = AppUserModelId.Split("!")[1];
        }

        /// <summary>
        /// A reference to this object.
        /// This is a workaround for x:Bind not supporting the "." path
        /// </summary>
        public AppListEntryModel This { get; private set; }

        void RunCatch(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                DebugLog.Append($"Couldn't read AppListEntry property");
                DebugLog.Append(e);
            }
        }

        public string AppUserModelId { get; private set; }
        public string Description { get; private set; }
        public string DisplayName { get; private set; }

        // Note: this is calculated from the Aumid
        public string Id { get; private set; }

        // Note: this is read from the appx manifest
        public string ExecutionAliases { get; internal set; }

        // Note: this is read from the appx manifest
        public string FileTypeAssociations { get; internal set; }

        // Note: this is read from the appx manifest
        public string Protocols { get; set; }

        // Logo converted to a Xaml BitmapImage from a RandomAccessStreamReference
        public BitmapImage Logo
        {
            get
            {
                if (_logo == null)
                {
                    CreateLogoBitmapSource();
                }

                return _logo;
            }
        }
        BitmapImage _logo;

        // This is the value read from the AppListEntry
        RandomAccessStreamReference _logoRef;

        void CreateLogoBitmapSource()
        {
            MyThreading.RunOnUI(async () =>
            {
                var ras = await _logoRef.OpenReadAsync();
                _logo = new();
                _logo.DecodePixelHeight = 36;
                _logo.DecodePixelWidth = 36;
                _logo.DecodePixelType = DecodePixelType.Logical;
                await _logo.SetSourceAsync(ras);
                RaisePropertyChanged(nameof(Logo));
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void RaisePropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal void Launch()
        {
            _ = MyThreading.RunOnWorkerAsync(() => _ = _appListEntry.LaunchAsync());
        }
    }
}
