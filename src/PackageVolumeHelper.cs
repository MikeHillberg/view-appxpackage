using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Management.Deployment;

namespace ViewAppxPackage;

/// <summary>
/// Helper class that uses the Windows Platform SDK PackageManager APIs
/// to look up the PackageVolume for a package.
/// Separated from PackageModel to avoid confusion with the Windows App SDK
/// Package APIs used elsewhere.
/// </summary>
internal static class PackageVolumeHelper
{
    static PackageManager _packageManager => PackageCatalogModel.PackageManager;

    // Cache: family name -> volume name
    static volatile Dictionary<string, string> _familyToVolume;

    /// <summary>
    /// Whether the cache has been built and is ready for lookups
    /// </summary>
    internal static bool IsCacheReady => _familyToVolume != null;

    /// <summary>
    /// Fired on the UI thread when the cache finishes building
    /// </summary>
    internal static event Action CacheReady;

    /// <summary>
    /// Enumerate packages on a volume, using the current-user API when not elevated
    /// </summary>
    internal static IEnumerable<Windows.ApplicationModel.Package> FindPackagesOnVolume(PackageVolume volume)
    {
        if (App.IsProcessElevated())
            return volume.FindPackages();
        else
            return volume.FindPackagesForUser(string.Empty);
    }

    /// <summary>
    /// Build the cache on a background thread. When complete, fires CacheReady on the UI thread.
    /// </summary>
    internal static void StartBuildingCache()
    {
        _ = MyThreading.RunOnWorkerAsync(() =>
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var volumes = _packageManager.FindPackageVolumes();
                foreach (var volume in volumes)
                {
                    try
                    {
                        foreach (var pkg in FindPackagesOnVolume(volume))
                        {
                            var familyName = pkg.Id.FamilyName;
                            map.TryAdd(familyName, volume.Name);
                        }
                    }
                    catch (Exception e)
                    {
                        DebugLog.Append($"Exception enumerating packages on volume {volume.Name}: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                DebugLog.Append($"Exception calling FindPackageVolumes: {e.Message}");
            }

            _familyToVolume = map;

            // Raise on background thread
            CacheReady?.Invoke();
        });
    }

    /// <summary>
    /// Find the volume name for a package by family name.
    /// Returns null if cache is not yet ready.
    /// </summary>
    internal static string FindVolumeNameForPackage(string familyName)
    {
        if (string.IsNullOrEmpty(familyName))
            return null;

        var cache = _familyToVolume;
        if (cache == null)
            return null;

        return cache.TryGetValue(familyName, out var volumeName) ? volumeName : "";
    }

    /// <summary>
    /// Clear the cache (e.g. when reloading packages)
    /// </summary>
    internal static void ClearCache()
    {
        _familyToVolume = null;
    }
}
