using Microsoft.UI.Dispatching;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using ViewAppxPackage;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Management.Deployment;

namespace TestProject1;


[TestClass]
[DoNotParallelize]
public sealed class UnitTests
{
    // MSTest sets this
    public TestContext TestContext { get; set; }

    static DispatcherQueue _uiDispatcherQueue = null!;
    static PackageCatalogModel _catalogModel = null!;
    const string viewAppPackageName = "2775CoffeeZeit.28328C7222DA6";
    const string testPackageName = "a60d2c46-59cf-4c4f-87b5-a39bf0be42c9";

    static readonly string tempDirectoryPath = Path.Combine(Path.GetTempPath(), "view-appxpackage.Test");
    static readonly string testMsixPath = Path.Combine(tempDirectoryPath, "TestPackage.msixbundle");

    [ClassInitialize]
    async public static Task ClassInitializeAsync(TestContext context)
    {
        // Make sure any leftover test package from a previous run is gone
        // Do this before initializing the PackageCatalogManager, to avoid confusion
        await RemoveTestPackage();

        // Create a directory for all temp files for this test run
        if (Directory.Exists(tempDirectoryPath))
        {
            Directory.Delete(tempDirectoryPath, true);
        }
        Directory.CreateDirectory(tempDirectoryPath);

        // Put the test package into a file
        var packageBytes = Resource1.TestPackage_1_0_1_0_x86;
        File.WriteAllBytes(testMsixPath, packageBytes);

        // Create a worker thread
        await MyThreading.EnsureWorkerThreadAsync();

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

    private static async Task RemoveTestPackage()
    {
        try
        {
            PackageManager packageManager = new();
            await packageManager.RemovePackageAsync("a60d2c46-59cf-4c4f-87b5-a39bf0be42c9_1.0.1.0_x86__tx8btddkt3yjy");
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed RemovePackage");
            Debug.WriteLine(e.Message);
        }
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        // bugbug: had this as an await call, and this method async,
        // but this call was never completing (except when in the debugger)
        _ = RemoveTestPackage();

        MyThreading.ShutdownWorkerThread();

        Directory.Delete(tempDirectoryPath, true);

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

    /// <summary>
    /// Verify the PackageModel properties
    /// </summary>
    /// <returns></returns>
    [WorkerTestMethod]
    async public Task ValidatePackageModelProperties()
    {
        // Get view-appxpackage's PackageModel
        var package = GetViewAppxPackage();

        // Get the PackageModel filled in
        package.EnsureInitializeAsync();

        Assert.IsTrue(package.Name == "2775CoffeeZeit.28328C7222DA6");
        Assert.IsTrue(package.DisplayName == "view-appxpackage");
        Assert.IsTrue(package.InstalledDate > DateTime.MinValue 
            && package.InstalledDate < DateTime.Now);

        Assert.IsTrue(package.Publisher == "CN=1BD61FE2-F217-4D46-9A05-EE02A424756D");
        Assert.IsTrue(package.PublisherId == "tpt8xzg3yk1mm");
        Assert.IsTrue(package.Version.Major == 1 && package.Version.Minor == 0);
        Assert.IsTrue(package.FullName.StartsWith("2775CoffeeZeit.28328C7222DA6") 
            && package.FullName.EndsWith("tpt8xzg3yk1mm"));
        Assert.IsTrue(package.FamilyName == "2775CoffeeZeit.28328C7222DA6_tpt8xzg3yk1mm");
        Assert.IsTrue(package.Capabilities == "packageManagement, runFullTrust");
        Assert.IsTrue(package.Architecture != Windows.System.ProcessorArchitecture.Unknown);
        Assert.IsTrue(package.InstalledPath.Contains("view-appxpackage"));
        Assert.IsTrue(package.ApplicationDataPath.Contains("CoffeeZeit"));
        Assert.IsTrue(package.EffectivePath.Contains("view-appxpackage"));
        Assert.IsTrue(package.SignatureKind == PackageSignatureKind.None);
        Assert.IsTrue(package.IsDevelopmentMode);
        Assert.IsTrue(package.Status == "Ok");
        Assert.IsTrue(package.AppEntries.Count == 1);
        Assert.IsTrue(package.AppEntries[0].Id == "App");
        Assert.IsTrue(package.AppEntries[0].DisplayName == "ViewAppxPackage");
        Assert.IsTrue(package.AppEntries[0].Description == "ViewAppxPackage");
        Assert.IsTrue(package.AppEntries[0].ExecutionAliases == "view-appxpackage.exe, view-msixpackage.exe");
        Assert.IsTrue(package.AppEntries[0].AppUserModelId == "2775CoffeeZeit.28328C7222DA6_tpt8xzg3yk1mm!App");

        // The EnsureInitialize doesn't wait to calculate the Size,
        // give that a chance to get figured out
        for (int i = 0; i < 20; i++)
        {
            if (package.Size != "")
            {
                break;
            }

            // bugbug: sleep is bad
            await Task.Delay(100);
        }

        // Sanity check the package size
        var size = package.Size.Split(' ')[0].Trim();
        Assert.IsTrue(Int32.Parse(size) > 25);
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

    [TestMethod]
    async public Task TestDependentsAndDependencies()
    {
        PackageModel? viewAppxPackage = null;
        PackageModel? winAppRuntime = null;

        // Get the PackageModel for view-appxpackage
        await MyThreading.RunOnUIAsync(() =>
        {
            viewAppxPackage = GetViewAppxPackage();
            Assert.IsTrue(viewAppxPackage != null);
        });

        // Initialize that PackageModel on the worker thread,
        // and verify that it has the WinAppSDK as a dependency
        await MyThreading.RunOnWorkerAsync(async () =>
        {
            // Initializing a package causes it to add itself to
            // the dependents of dependency packages
            viewAppxPackage!.EnsureInitializeAsync();

            winAppRuntime = viewAppxPackage!.Dependencies.FirstOrDefault(p => p.Name.StartsWith("Microsoft.WindowsAppRuntime."));
            Assert.IsTrue(winAppRuntime != null);

            // Let the UI thread tick to process that Dependents update
            await MyThreading.RunOnUIAsync(() =>
            {
            },
            DispatcherQueuePriority.Low);
        });

        // Verify that that WinAppSDK package has, in turn, view-appxpackage as a dependent
        await MyThreading.RunOnUIAsync(() =>
        {
            var appPackage2 = winAppRuntime!.Dependents.FirstOrDefault(p => p.Name == viewAppPackageName);
            Assert.IsTrue(appPackage2 != null);
            Assert.IsTrue(appPackage2.FullName == viewAppxPackage!.FullName);
        });
    }

    [WorkerTestMethod]
    async public Task TestVerify()
    {
        var package = GetViewAppxPackage();
        var valid = await package.VerifyAsync();
        Assert.IsTrue(valid);
    }

    /// <summary>
    /// Get the PackageModel for view-appxpackage
    /// </summary>
    /// <returns></returns>
    PackageModel GetViewAppxPackage()
    {
        var appPackage = _catalogModel.Packages.FirstOrDefault(p => p.Name == viewAppPackageName);
        Assert.IsTrue(appPackage != null);
        return appPackage;
    }

    static PackageModel? GetTestPackage()
    {
        var appPackage = _catalogModel.Packages.FirstOrDefault(p => p.Name == testPackageName);
        return appPackage;
    }


    [TestMethod]
    async public Task TestAddRemove()
    {
        await AddRemoveHelper(sideload: false);
    }

    [TestMethod]
    async public Task TestSideload()
    {
        await AddRemoveHelper(sideload: true);
    }

    async public Task AddRemoveHelper(bool sideload)
    {
        var package = GetTestPackage();
        Assert.IsTrue(package == null);

        // Register the test package, either as an Add Package or as a loose file deployment of an XML
        await MyThreading.RunOnWorkerAsync(async () =>
        {
            bool progressCalled = false;

            if (sideload)
            {
                // Unzip the package and find the appxmanifest.xml
                var unzipFolder = Path.Combine(tempDirectoryPath, "TestPackage.Unzip");
                var manifestPath = _catalogModel.UnzipPackage(testMsixPath, unzipFolder);
                Assert.IsTrue(!string.IsNullOrEmpty(manifestPath));

                // Register the appxmanifest.xml
                Uri msixUri = new(manifestPath);
                await _catalogModel.RegisterPackageByUriAsync(msixUri, async (op) =>
                {
                    progressCalled = true;
                    var result = await op;
                    Assert.IsTrue(result.IsRegistered);
                });
            }
            else
            {
                // Register the signed package
                Uri msixUri = new Uri(testMsixPath);
                await _catalogModel.AddPackageAsync(msixUri, async (op) =>
                {
                    progressCalled = true;
                    var result = await op;
                    Assert.IsTrue(result.IsRegistered);
                });
            }
            Assert.IsTrue(progressCalled);
        });

        // Wait for the notification to come in that the package has been added
        // (When it does, it will be added to CatalogModel.Packages)
        var found = await RetryLoop.RunAsync(
            TimeSpan.FromMilliseconds(200),
            200,
            async () =>
            {
                await MyThreading.RunOnUIAsync(() =>
                {
                    package = GetTestPackage();
                });

                if (package != null)
                {
                    return true;
                }
                return false;
            });
        Assert.IsTrue(found);

        // Now remove the package
        await MyThreading.RunOnWorkerAsync(async () =>
        {
            bool progressCalled = false;
            await _catalogModel.RemovePackageAsync(package, async (op) =>
            {
                progressCalled = true;
            });
            Assert.IsTrue(progressCalled);
        });

        // Now the package should disappear when a notification comes in
        found = await RetryLoop.RunAsync(
            TimeSpan.FromMilliseconds(200),
            200,
            async () =>
            {
                await MyThreading.RunOnUIAsync(() =>
                {
                    package = GetTestPackage();
                });

                if (package == null)
                {
                    return true;
                }
                return false;
            });
        Assert.IsTrue(found);
    }

    [TestMethod]
    async public Task TestFilter()
    {
        var catalogModel = PackageCatalogModel.Instance;

        await MyThreading.RunOnUIAsync(() =>
        {
            try
            {
                // Updating the Filter causes Packages to be recalculated synchronously
                catalogModel.Filter = "*28328C7222DA6*";
                Assert.IsTrue(catalogModel.Packages.Count == 1);

                var package = catalogModel.Packages[0];
                Assert.IsTrue(package.Name == viewAppPackageName);
            }
            finally
            {
                catalogModel.Filter = null;
            }
        });
    }

    [TestMethod]
    async public Task TestSearch()
    {
        var catalogModel = PackageCatalogModel.Instance;

        await MyThreading.RunOnUIAsync(() =>
        {
            try
            {
                // Updating the SearchText causes Packages to be recalculated synchronously
                // Search for something other than name (which you can already search with the Filter)
                catalogModel.SearchText = "view-appxpackage.exe";
                Assert.IsTrue(catalogModel.Packages.Count == 1);

                var package = catalogModel.Packages[0];
                Assert.IsTrue(package.Name == viewAppPackageName);
            }
            finally
            {
                catalogModel.SearchText = null;
            }
        });
    }

    [WorkerTestMethod]
    public void TestManifest()
    {
        var package = GetViewAppxPackage();
        Assert.IsTrue(package != null);
        Assert.IsTrue(!string.IsNullOrEmpty(package.AppxManifestContent));

        // Sanity check by looking for the Id attribute on /Package/Applications/Application

        var xmlDoc = System.Xml.Linq.XDocument.Parse(package.AppxManifestContent);
        Assert.IsTrue(xmlDoc != null && xmlDoc.Root != null);

        var applications = (from e in xmlDoc.Root.Elements() where e.Name.LocalName == "Applications" select e).FirstOrDefault();
        Assert.IsTrue(applications != null);

        var application = (from e in applications.Elements() where e.Name.LocalName == "Application" select e).FirstOrDefault();
        Assert.IsTrue(application != null);

        var id = application.Attribute("Executable");
        Assert.IsTrue(id?.Value == @"view-appxpackage\view-appxpackage.exe");
        var appPackage2 = winAppRuntime.Dependents.FirstOrDefault(p => p.Name == viewAppPackageName);
        Assert.IsTrue(appPackage2 != null);
        Assert.IsTrue(appPackage2.FullName == appPackage.FullName);
    }

    /// <summary>
    /// Test MCP Server functionality - package family name extraction
    /// </summary>
    [WorkerTestMethod]
    public void TestMcpServerPackageFamilyNames()
    {
        // Create the MCP server service
        var mcpServer = new McpServer();

        // Get package family names directly from the tool method
        var familyNames = mcpServer.ListPackageFamilyNames();

        // Verify we get some family names
        Assert.IsNotNull(familyNames);
        var familyNameList = familyNames.ToList();
        Assert.IsTrue(familyNameList.Count > 0, "Should have at least one package family name");

        // Verify we have the same number of unique family names as packages
        var packageCount = PackageCatalogModel.Instance.Packages.Count;
        var packagesWithFamilyNames = PackageCatalogModel.Instance.Packages
            .Where(p => !string.IsNullOrEmpty(p.FamilyName))
            .Select(p => p.FamilyName)
            .Distinct()
            .Count();

        Assert.AreEqual(packagesWithFamilyNames, familyNameList.Count,
            "Number of unique family names should match packages with family names");

        // Verify family names are sorted
        var sortedFamilyNames = familyNameList.OrderBy(name => name).ToList();
        CollectionAssert.AreEqual(sortedFamilyNames, familyNameList,
            "Family names should be returned in sorted order");

        var winAppruntime = familyNameList.FirstOrDefault(name => name.StartsWith("Microsoft.WindowsAppRuntime."));
        Assert.IsNotNull(winAppruntime, "Should find the Windows App Runtime package family name in the list");
    }

    [WorkerTestMethod]
    public void TestMcpServerFindContainingProperty()
    {
        // Create the MCP server service
        var mcpServer = new McpServer();
        var packages = mcpServer.FindPackagesContainingProperty("Name", "Paint");
        Assert.IsTrue(packages.Contains("Paint"));
    }
}