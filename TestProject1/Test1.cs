using Microsoft.UI.Dispatching;
using System.Globalization;
using System.Text.RegularExpressions;
using ViewAppxPackage;
using Windows.Foundation;
using Windows.Management.Deployment;

namespace TestProject1
{
    [TestClass]
    [DoNotParallelize]
    public sealed class UnitTests
    {
        // MSTest sets this
        public TestContext TestContext { get; set; }

        static DispatcherQueue _uiDispatcherQueue = null!;
        static PackageCatalogModel _catalogModel = null!;
        const string viewAppPackageName = "2775CoffeeZeit.28328C7222DA6";

        [ClassInitialize]
        async public static Task ClassInitializeAsync(TestContext context)
        {
            // Create a worker thread
            await MyThreading.CreateWorkerThreadAsync();

            // Create a UI thread
            var controller = DispatcherQueueController.CreateOnDedicatedThread();
            _uiDispatcherQueue = controller.DispatcherQueue;

            var semaphore = new SemaphoreSlim(0, 1);
            _uiDispatcherQueue.TryEnqueue(() =>
            {
                MyThreading.SetCurrentAsUIThread();
                semaphore.Release();
            });
            semaphore.Wait();

            // Initialize the package catalog
            await MyThreading.RunOnWorkerAsync(() =>
            {
                PackageCatalogModel.Instance.Initialize(
                    isAllUsers: false,
                    preloadFullName: false,
                    useSettings: false);

                _catalogModel = PackageCatalogModel.Instance;
            });
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            MyThreading.ShutdownWorkerThread();
            _uiDispatcherQueue.EnqueueEventLoopExit();
        }

        [UITestMethod]
        public void TestLoad()
        {
            var packageCatalog = PackageCatalogModel.Instance;
            Assert.IsNotNull(packageCatalog);
            Assert.IsTrue(packageCatalog.Packages.Count > 0);
            var package = packageCatalog.Packages.First();
            Assert.IsNotNull(package);
        }

        /// <summary>
        /// Test the parsers for new/edit of package settings
        /// </summary>
        [TestMethod]
        public void TestPackageSettingValueParsing()
        {
            ValidatePackageSettingParseFor<Int16>((s) => Int16.Parse(s));
            ValidatePackageSettingParseFor<Int32>((s) => Int32.Parse(s));
            ValidatePackageSettingParseFor<Int64>((s) => Int64.Parse(s));

            ValidatePackageSettingParseFor<UInt16>((s) => UInt16.Parse(s));
            ValidatePackageSettingParseFor<UInt32>((s) => UInt32.Parse(s));
            ValidatePackageSettingParseFor<UInt64>((s) => UInt64.Parse(s));

            ValidatePackageSettingParseFor<short>((s) => short.Parse(s));
            ValidatePackageSettingParseFor<float>((s) => float.Parse(s));
            ValidatePackageSettingParseFor<double>((s) => double.Parse(s));

            ValidatePackageSettingParseFor<bool>((s) => bool.Parse(s));
            ValidatePackageSettingParseFor<char>((s) => char.Parse(s));
            ValidatePackageSettingParseFor<byte>((s) => byte.Parse(s));
            ValidatePackageSettingParseFor<Guid>((s) => Guid.Parse(s));

            // These types have commas, so the array form has newlines
            ValidatePackageSettingParseFor<Point>((s) => SettingEditBox.ParsePoint(s), hasCommas: true);
            ValidatePackageSettingParseFor<Rect>((s) => SettingEditBox.ParseRect(s), hasCommas: true);
            ValidatePackageSettingParseFor<Size>((s) => SettingEditBox.ParseSize(s), hasCommas: true);

            // The start string for these have non-default formats
            ValidatePackageSettingParseFor<DateTimeOffset>((s) => DateTimeOffset.Parse(s));
            ValidatePackageSettingParseFor<TimeSpan>((s) => TimeSpan.Parse(s));
        }

        void ValidatePackageSettingParseFor<T>(Func<string, T> parse, bool hasCommas = false)
        {
            var type = typeof(T);

            // Use the example strings for this type, as shown in the UI, as the starting point
            var exampleString = NewPackageSettingValue.ExampleString(type, isArray: false);

            // Parse the example string with app code
            var parsed = SettingEditBox.TryParseValue(typeof(T), exampleString, out var parsedValue);
            Assert.IsTrue(parsed);
            Assert.IsTrue(parsedValue is T);

            exampleString = ValidatePackageSettingHelper(parse, exampleString, parsedValue, hasCommas);

            // Do all this again but for arrays of the type
            ValidatePackageSettingParseForArrayOf(parse, hasCommas);
        }

        void ValidatePackageSettingParseForArrayOf<T>(Func<string, T> parse, bool hasCommas = false)
        {
            var arrayType = typeof(T).MakeArrayType();

            // Use the example strings for this type, as shown in the UI, as the starting point
            var exampleString = NewPackageSettingValue.ExampleString(typeof(T), isArray: true);

            // Validate that it can be parsed with app code
            var parsed = SettingEditBox.TryParseValue(arrayType, exampleString, out var parsedValue);
            Assert.IsTrue(parsed);
            Assert.IsTrue(parsedValue is T[]);

            var parsedArray = parsedValue as T[];
            Assert.IsNotNull(parsedArray);

            // Split the example string on commas or lines
            var exampleStringParts = hasCommas ? exampleString.Split('\r') : exampleString.Split(',');
            Assert.IsTrue(exampleStringParts.Length == parsedArray.Length);

            // Compare the values parsed with app code with those parsed with test code
            for (int i = 0; i < exampleStringParts.Length; i++)
            {
                ValidatePackageSettingHelper(parse, exampleStringParts[i].Trim(), parsedArray[i]!, hasCommas);
            }
        }

        private static string ValidatePackageSettingHelper<T>(Func<string, T> parse, string stringValue, object parsedValue, bool hasCommas)
        {
            // Parse the example string with test code
            var testParsedValue = parse(stringValue);
            Assert.IsTrue(((T)parsedValue).Equals(testParsedValue));

            // If the type has commas (like Point), compress interior space to
            // match how the example strings are formatted
            if (hasCommas)
            {
                stringValue = Regex.Replace(stringValue, @",\s+", ",");
            }

            // Convert the parsed example string back to a value and compare the strings
            var parsedValueToString = SettingValueToString(parsedValue);
            Assert.IsTrue(parsedValueToString == stringValue);
            return stringValue;
        }

        static string SettingValueToString(object parsedValue)
        {
            // String-ize TimeSpan and DateTimeOffset to match the friendlier format of the example strings
            // E.g. no leading zeros on the time

            if (parsedValue is TimeSpan timeSpan)
            {
                return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
            else if (parsedValue is DateTimeOffset dt)
            {
                return dt.ToString("M/d/yyyy h:mmtt", CultureInfo.InvariantCulture).ToLower();
            }

            return parsedValue.ToString()!;
        }

        [WorkerTestMethod]
        public void ValidateLoad()
        {
            Assert.IsTrue(MyThreading.CurrentIsWorkerThread);

            PackageManager packageManager = new();
            var wamPackages = packageManager.FindPackagesForUser(userSecurityId: string.Empty);
            wamPackages = from p in wamPackages where !p.IsResourcePackage select p;

            Assert.IsTrue(wamPackages.Count() == PackageCatalogModel.Instance.PackageCount);
        }


        [TestMethod]
        async public Task TestMyThreading()
        {
            var startThreadId = Thread.CurrentThread.ManagedThreadId;

            int workerThreadId = -1;
            await MyThreading.RunOnWorkerAsync(() =>
            {
                workerThreadId = Thread.CurrentThread.ManagedThreadId;
                Assert.IsTrue(workerThreadId != startThreadId);
                Assert.IsTrue(MyThreading.CurrentIsWorkerThread);
            });

            int uiThreadId = -1;
            await MyThreading.RunOnUIAsync(() =>
            {
                uiThreadId = Thread.CurrentThread.ManagedThreadId;
                Assert.IsTrue(uiThreadId != startThreadId);
                Assert.IsTrue(uiThreadId != workerThreadId);
                Assert.IsTrue(MyThreading.CurrentIsUiThread);
            });

            Task task = null!;
            bool completed = false;

            var asyncAction = async () =>
            {
                await Task.Run(() => 
                {
                    // bugbug: no sleeps
                    Thread.Sleep(1_000);
                });

                Assert.IsTrue(!task.IsCompleted);
                completed = true;
            };

            var syncAction = () =>
            {
                // bugbug: no sleeps
                Thread.Sleep(1_000);
                Assert.IsTrue(!task.IsCompleted);
                completed = true;
            };

            completed = false;
            task = MyThreading.RunOnWorkerAsync(asyncAction);
            await task;
            Assert.IsTrue(completed);

            completed = false;
            task = MyThreading.RunOnWorkerAsync(syncAction);
            await task;
            Assert.IsTrue(completed);

            completed = false;
            task = MyThreading.RunOnUIAsync(asyncAction);
            await task;
            Assert.IsTrue(completed);

            completed = false;
            task = MyThreading.RunOnUIAsync(syncAction);
            await task;
            Assert.IsTrue(completed);

            Semaphore sem = new(0, 1);
            syncAction = () => sem.Release();
            MyThreading.PostToUI(() => syncAction());
            await Task.Run(() => sem.WaitOne());
        }

        [WorkerTestMethod]
        async public Task TestDependentsAndDependencies()
        {
            var appPackage = _catalogModel.Packages.FirstOrDefault(p => p.Name == viewAppPackageName);
            Assert.IsTrue(appPackage != null);

            var winAppRuntime = appPackage.Dependencies.FirstOrDefault(p => p.Name.StartsWith("Microsoft.WindowsAppRuntime."));
            Assert.IsTrue(winAppRuntime != null);

            // Initializing a package causes it to add itself to
            // the dependents of dependency packages
            appPackage.EnsureInitializeAsync();

            // Let the UI thread tick to process that Dependents update
            await MyThreading.RunOnUIAsync(() =>
            {
            });

            var appPackage2 = winAppRuntime.Dependents.FirstOrDefault(p => p.Name == viewAppPackageName);
            Assert.IsTrue(appPackage2 != null);
            Assert.IsTrue(appPackage2.FullName == appPackage.FullName);
        }
    }
}
