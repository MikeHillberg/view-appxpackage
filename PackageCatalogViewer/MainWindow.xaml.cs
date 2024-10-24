using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.Storage.Pickers;
using WinRT.Interop;




// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PackageCatalogViewer
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            StartGetAllPackagesForUser();

            this.InitializeComponent();
            RootElement = this._root;

            RootElement.Loaded += (s, e) =>
            {
                _filterBox.Focus(FocusState.Programmatic);
            };

            _packageCatalog = PackageCatalog.OpenForCurrentUser();
            _packageCatalog.PackageInstalling += (s, e) => UpdateDeployment(e.IsComplete, e.Package, true);
            _packageCatalog.PackageUninstalling += (s, e) => UpdateDeployment(e.IsComplete, e.Package, false);
        }

        internal static FrameworkElement RootElement { get; private set; }

        void UpdateDeployment(bool isComplete, Package package, bool isInstalling)
        {
            if (!isComplete)
            {
                return;
            }

            if (Packages == null)
            {
                // bugbug: race
                return;
            }

            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (isInstalling)
                {
                    _originalpackages.Add(package);
                    _originalpackages = new ObservableCollection<Package>(_originalpackages.OrderBy((p) => p.DisplayName).ToList());
                    FilterPackages();
                }
                else
                {
                    var existing = _originalpackages.FirstOrDefault(p => p.Id.FullName == package.Id.FullName);
                    _originalpackages.Remove(existing);
                    FilterPackages();
                }
            });
        }


        PackageCatalog _packageCatalog;
        ObservableCollection<Package> _originalpackages;
        static ObservableCollection<Package> _packages;
        public ObservableCollection<Package> Packages
        {
            get { return _packages; }
            private set { _packages = value; RaisePropertyChanged(); }
        }



        async void StartGetAllPackagesForUser()
        {
            IEnumerable<Package> packages = null;
            await Task.Run(() =>
            {
                packages = _packageManager.FindPackagesForUser(string.Empty);
                //packages = packages.OrderBy((p) => p.DisplayName).ToList();
                var sorted = from p in packages
                             where !p.IsResourcePackage
                             let name = p.DisplayName
                             orderby string.IsNullOrEmpty(name) ? "zzz" : name
                             select p;
                packages = sorted.ToList();
            });

            Debug.WriteLine($"{packages.ToArray()[0].DisplayName}");
            _originalpackages = new ObservableCollection<Package>(packages);
            Packages = _originalpackages;
        }

        PackageManager _packageManager = new PackageManager();

        public event PropertyChangedEventHandler PropertyChanged;

        void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        double _minListWidth = 200;
        double MinListWidth
        {
            get => _minListWidth;
            set
            {
                _minListWidth = value;
                RaisePropertyChanged();
            }
        }

        async private void Remove(object sender, RoutedEventArgs e)
        {
            var package = _lv.SelectedItem as Package;
            if (package == null)
            {
                return;
            }

            var result = await MyMessageBox.Show(package.DisplayName, title: "Remove?", isOKEnabled: true, closeButtonText: "Cancel");
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            _ = _packageManager.RemovePackageAsync(package.Id.FullName);
        }



        string _filter;
        public string Filter
        {
            get { return _filter; }
            set
            {
                _filter = value;
                RaisePropertyChanged();
                FilterPackages();
            }
        }

        void FilterPackages()
        {
            if (Packages == null)
            {
                // bugbug: race
                return;
            }

            if (string.IsNullOrEmpty(_filter))
            {
                Packages = _originalpackages;
                return;
            }

            var updatedFilter = $"^{_filter}$";
            updatedFilter = updatedFilter.Replace("?", ".");
            updatedFilter = updatedFilter.Replace("*", ".*");


            Regex regex = new(updatedFilter, RegexOptions.IgnoreCase);

            var filter = _filter.ToLower();
            var packages = from p in _originalpackages
                               //where p.DisplayName.ToLower().Contains(filter)
                           let matches = regex.Matches(p.DisplayName)
                           where matches.Count > 0
                           select p;
            Packages = new ObservableCollection<Package>(packages);
        }

        private void TestClick(object sender, RoutedEventArgs e)
        {

        }

        private void SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
        {
            if (_lv.SelectedItem == null)
            {
                // bugbug: why isn't the x:Bind doing this?
                _detail.Package = null;
            }
        }

        async private void AddClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);


            picker.FileTypeFilter.Add(".appx");
            picker.FileTypeFilter.Add(".msix");
            var storageFile = await picker.PickSingleFileAsync();
            if(storageFile == null)
            {
                return;
            }

            var fileUri = new Uri(storageFile.Path);

            _ = _packageManager.AddPackageAsync(fileUri, null, DeploymentOptions.None);
        }

        private void ListSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if(_lv.ActualWidth > MinListWidth)
            {
                MinListWidth = _lv.ActualWidth;
            }
        }

        private void LaunchClick(object sender, RoutedEventArgs e)
        {
            var package = _lv.SelectedItem as Package;
            if(package == null)
            {
                return;
            }

            // shell:AppsFolder\$($(get-appxpackage $app | select PackageFamilyName).PackageFamilyName)!app

            ProcessStartInfo si = new();
            si.FileName = $@"shell:AppsFolder\{package.Id.FamilyName}!app";
            si.UseShellExecute = true;

            try
            {
                Process.Start(si);
            }
            catch (Exception ex)
            {
            }
        }
    }
}
