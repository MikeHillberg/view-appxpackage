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

                // Try to parse back to original type, fall back to string
                var effectiveType = entry.IsArray ? entry.ValueType.MakeArrayType() : entry.ValueType;
                if (PackageSettingValue.TryParseValue(effectiveType, entry.Value, out var parsed))
                {
                    newComposite[entry.Key] = parsed;
                }
                else
                {
                    newComposite[entry.Key] = entry.Value;
                }
            }

            // Write to the container
            var parentContainer = _setting.Package.GetAppDataContainerForSetting(_setting);
            parentContainer.Values[_setting.Name] = newComposite;

            // Update the model
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

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var entry = new CompositeValueEntry
        {
            Key = "Key",
            Value = "Value",
            ValueType = typeof(string)
        };
        Entries.Add(entry);
        _listView.SelectedItem = entry;
        entry.BeginEdit();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_listView.SelectedItem is CompositeValueEntry entry)
        {
            entry.BeginEdit();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_listView.SelectedItem is CompositeValueEntry entry)
        {
            Entries.Remove(entry);
        }
    }

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

    private void ItemCancel_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CompositeValueEntry entry)
        {
            entry.CancelEdit();
        }
    }

    private readonly TreeListViewClickToEditHelper _clickToEdit = new();

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

    private void ViewText_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // IsTextSelectionEnabled eats pointer events, preventing the ListViewItem from selecting.
        // Programmatically select the item when the text is clicked.
        if ((sender as FrameworkElement)?.DataContext is CompositeValueEntry entry)
        {
            _listView.SelectedItem = entry;
        }
    }

    private void ViewText_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CompositeValueEntry entry
            && entry == _listView.SelectedItem
            && _clickToEdit.ShouldEditInListView(true))
        {
            entry.BeginEdit();
        }
    }

    private void ListView_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        _clickToEdit.OnListViewSelectionChanged();

        // Cancel editing on any previously-editing items
        foreach (var old in e.RemovedItems)
        {
            if (old is CompositeValueEntry entry && entry.IsEditing)
            {
                entry.CancelEdit();
            }
        }
    }

    }
