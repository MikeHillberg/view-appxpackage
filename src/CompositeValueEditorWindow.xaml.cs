using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.Storage;

namespace ViewAppxPackage;

/// <summary>
/// A ContentDialog for editing the key/value pairs of an ApplicationDataCompositeValue
/// </summary>
public sealed partial class CompositeValueEditorWindow : ContentDialog
{
    private readonly PackageSettingValue _setting;
    private readonly ApplicationDataCompositeValue _compositeValue;
    private readonly TreeListViewClickToEditHelper _clickToEdit = new();


    public ObservableCollection<CompositeValueEntry> Entries { get; } = new();

    public CompositeValueEditorWindow(PackageSettingValue setting)
    {
        this.InitializeComponent();

        _setting = setting;
        _compositeValue = setting.KeyValuePair.Value as ApplicationDataCompositeValue;

        Title = $"{setting.Name} - {setting.Package.DisplayName}";
        XamlRoot = MainWindow.RootElement.XamlRoot;

        // Load existing entries
        if (_compositeValue != null)
        {
            foreach (var kvp in _compositeValue)
            {
                var valueType = kvp.Value?.GetType() ?? typeof(string);
                var isArray = valueType.IsArray;
                var elementType = isArray ? valueType.GetElementType() : valueType;

                Entries.Add(new CompositeValueEntry
                {
                    Key = kvp.Key,
                    Value = kvp.Value != null ? PackageSettingBase.ConvertSettingValueToString(kvp.Value) : string.Empty,
                    ValueType = elementType,
                    IsArray = isArray
                });
            }
        }
    }

    private void Save_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        try
        {
            // Build a new composite value from the entries
            var newComposite = new ApplicationDataCompositeValue();
            foreach (var entry in Entries)
            {
                if (string.IsNullOrEmpty(entry.Key))
                {
                    continue;
                }

                // Invalid values can't get this far, but don't crash
                var effectiveType = entry.IsArray ? entry.ValueType.MakeArrayType() : entry.ValueType;
                if (PackageSettingValue.TryParseValue(effectiveType, entry.Value, out var parsed))
                {
                    newComposite[entry.Key] = parsed;
                }
                else
                {
                    // Can't happen
                    newComposite[entry.Key] = entry.Value;
                }
            }

            // Write to the container
            var parentContainer = _setting.Package.GetAppDataContainerForSetting(_setting);
            parentContainer.Values[_setting.Name] = newComposite;

            // Update the model (not really necessary because we're going to close the dialog)
            _setting.KeyValuePair = new KeyValuePair<string, object>(_setting.KeyValuePair.Key, newComposite);
            _setting.ValueAsString = PackageSettingBase.ConvertSettingValueToString(newComposite);

            DebugLog.Append($"Saved composite setting `{_setting.Name}` with {newComposite.Count} entries");
        }
        catch (Exception ex)
        {
            _errorText.Text = $"Error saving: {ex.Message}";
            _errorText.Visibility = Visibility.Visible;
            DebugLog.Append(ex, $"Failed to save composite setting: {_setting.Name}");
            args.Cancel = true;
        }
    }

    /// <summary>
    /// Add a new item with default key/value
    /// </summary>
    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var entry = new CompositeValueEntry
        {
            Key = "Key",
            Value = "Value",
            ValueType = typeof(string)
        };
        Entries.Add(entry);
        _treeView.SelectedItem = entry;
        entry.BeginEdit();
    }

    /// <summary>
    /// Put item into editing mode
    /// </summary>
    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_treeView.SelectedItem is CompositeValueEntry entry)
        {
            entry.BeginEdit();
        }
    }

    /// <summary>
    /// Remove selected item from the composite
    /// </summary>
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_treeView.SelectedItem is CompositeValueEntry entry)
        {
            Entries.Remove(entry);
        }
    }

    /// <summary>
    /// Save change and leave edit mode for an item if valid. Otherwise show an error
    /// </summary>
    private void ItemSave_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CompositeValueEntry entry)
        {
            // Validate the value can be parsed as the selected type
            var effectiveType = entry.IsArray ? entry.ValueType.MakeArrayType() : entry.ValueType;
            if (!PackageSettingValue.TryParseValue(effectiveType, entry.Value, out _))
            {
                entry.IsError = true;
                return;
            }

            entry.IsError = false;
            entry.CommitEdit();
        }
    }

    /// <summary>
    /// Revert changes to an item
    /// </summary>
    private void ItemCancel_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CompositeValueEntry entry)
        {
            entry.CancelEdit();
        }
    }

    /// <summary>
    /// Loaded handler for the view-mode TextBlocks
    /// </summary>
    private void ViewText_Loaded(object sender, RoutedEventArgs e)
    {
        // Register with handledEventsToo so we still get events
        // even when IsTextSelectionEnabled marks them as handled
        if (sender is UIElement element)
        {
            element.AddHandler(UIElement.TappedEvent, new Microsoft.UI.Xaml.Input.TappedEventHandler(ViewText_Tapped), true);
            element.AddHandler(UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(ViewText_PointerPressed), true);
        }
    }

    /// <summary>
    /// PointerPressed handler for the view-mode TextBlocks
    /// </summary>
    private void ViewText_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // Forward to logic that tracks when we should go into edit mode
        _clickToEdit.OnPointerPressedIn();

        // If clicking on the view-mode text we should go into edit mode
        if ((sender as FrameworkElement)?.DataContext is CompositeValueEntry entry)
        {
            _treeView.SelectedItem = entry;
        }
    }

    /// <summary>
    /// Tapped handler for view-mode TextBlocks
    /// </summary>
    private void ViewText_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        // If you tap on a view-mode TextBlock that's part of a selected item,
        // go into edit mode
        if ((sender as FrameworkElement)?.DataContext is CompositeValueEntry entry
            && entry == _treeView.SelectedItem
            && _clickToEdit.ShouldEdit(true, entry.IsEditing))
        {
            entry.BeginEdit();
        }
    }

    /// <summary>
    /// Listen to changes to selection on the TreeView
    /// </summary>
    private void TreeView_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        // Notify the handler that tracks when we should go into edit mode
        _clickToEdit.OnSelectionChanged(_treeView.SelectedItem != null);

        // Cancel editing on any previously-editing items
        foreach (var old in args.RemovedItems)
        {
            if (old is CompositeValueEntry entry && entry.IsEditing)
            {
                entry.CancelEdit();
            }
        }
    }
}
