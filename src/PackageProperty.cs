using System;
using System.Runtime.CompilerServices;

namespace ViewAppxPackage;

/// <summary>
/// A property of a PackageModel that is lazily initialized off thread
/// </summary>
internal class PackageProperty<T>
{
    T _value = default(T);
    Func<PackageModel, T> _getValue;

    internal PackageProperty(Func<PackageModel, T> getValue)
    {
        _getValue = getValue;
    }

    /// <summary>
    /// Until initialized, we'll return the default value
    /// </summary>
    internal bool Initialized { get; private set; }

    /// <summary>
    /// Go back to the un-initialized state
    /// </summary>
    internal void Clear()
    {
        _value = default;
        Initialized = false;
    }

    /// <summary>
    /// The value of the property. If first called from the UI thread and not initialized,
    /// return the default value and post to the worker thread to get the value,
    /// then raise a change notifications.
    /// </summary>
    /// <param name="model"></param>
    /// <param name="debug"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    internal T Value(
        PackageModel model,
        bool debug = false,
        [CallerMemberName] string name = null)
    {
        EnsureInitialized(model, debug, name);
        return _value;
    }

    /// <summary>
    /// The current value of the property, maybe the default value if not initialized.
    /// But if not initialized, don't go get the value.
    /// This is a thread-safe way to get the current value
    /// </summary>
    internal T CurrentValue => _value;


    bool _initializing = false;

    /// <summary>
    /// No op if already initialized. Otherwise get the value if already off the UI thread,
    /// post to the background the get the value if on the UI thread.
    /// </summary>
    /// <param name="model"></param>
    /// <param name="debug"></param>
    /// <param name="name"></param>
    internal void EnsureInitialized(
        PackageModel model, bool debug, string name = null)
    {
        if (Initialized)
        {
            return;
        }

        if (MyThreading.CurrentIsUiThread)
        {
            // On the UI thread we'll post to the worker thread to get the value
            // If we've already started that for this package/property, then we're done
            if (_initializing)
            {
                return;
            }

            // Avoid stack overflows
            _initializing = true;

            // Start loading all the properties on this package, off the UI thread
            // The idea is that if we're getting this property, we probably need all of them,
            // so get them in bulk rather than raise a million change notifications
            model.EnsureInitializeAsync(debug);
            return;
        }

        // We're on the worker thread

        T value = default;

        // The Package properties are very exceptional
        try
        {
            value = _getValue(model);
        }
        catch
        {
            DebugLog.Append($"Exception getting {model.Name}.{name}");
        }

        // Must set _value before _initialized for thread safety
        _value = value;
        Initialized = true;

        return;
    }
}
