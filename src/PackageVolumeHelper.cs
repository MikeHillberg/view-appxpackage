using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Management.Deployment;

namespace ViewAppxPackage;

/// <summary>
/// Helper class that uses the Windows Platform SDK PackageManager APIs
/// to look up the PackageVolume for a package.
/// Separated from PackageModel class in order to avoid confusion with
/// the WinAppSDK Package APIs used elsewhere.
/// </summary>
internal static class PackageVolumeHelper
{
    // WinAppSDK PackageManager
    static PackageManager _packageManager => PackageCatalogModel.PackageManager;

    // package full name -> volume name
    // Volatile because it's written on the worker thread but read on the UI thread
    static volatile Dictionary<string, string> _fullNameToVolumeName;

    /// <summary>
    /// Whether the cache has been built and is ready for lookups
    /// </summary>
    internal static bool IsCacheReady => _fullNameToVolumeName != null;

    /// <summary>
    /// Build the cache on a worker thread
    /// </summary>
    internal static async Task InitializeAsync()
    {
        await Task.Run(() =>
        {
            // There's no API to get a volume from a package, but there is an API to
            // get all packages for a volume. So use that and build a map
            // of package full name => volume name
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
                            map.TryAdd(pkg.Id.FullName, volume.Name);
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

            _fullNameToVolumeName = map;
        });

        return;
    }

    /// <summary>
    /// Enumerate packages on a volume
    /// </summary>
    internal static IEnumerable<Windows.ApplicationModel.Package> 
        FindPackagesOnVolume(PackageVolume volume)
    {
        if (App.IsProcessElevated())
        {
            // All users
            return volume.FindPackages();
        }
        else
        {
            return volume.FindPackagesForUser(string.Empty);
        }
    }

    /// <summary>
    /// Find the volume name for a package by full name.
    /// Returns null if map is not yet ready.
    /// </summary>
    internal static string FindVolumeNameForPackage(string fullName)
    {
        if (string.IsNullOrEmpty(fullName))
        {
            return null;
        }

        // Make a local copy to be thread safe
        var cache = _fullNameToVolumeName;
        if (cache == null)
        {
            return null;
        }

        return cache.TryGetValue(fullName, out var volumeName) ? volumeName : "";
    }

    /// <summary>
    /// Clear the cache (e.g. when reloading packages)
    /// </summary>
    internal static void ClearCache()
    {
        _fullNameToVolumeName = null;
    }
}
