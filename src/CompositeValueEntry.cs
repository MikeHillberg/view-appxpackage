using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ViewAppxPackage;

/// <summary>
/// Represents a single key/value entry in an ApplicationDataCompositeValue
/// </summary>
public class CompositeValueEntry : INotifyPropertyChanged
{
    private string _key;
    private string _value;
    private string _originalKey;
    private string _originalValue;
    private Type _originalType;

    public static List<Type> SupportedTypes => PackageSettingValue.SupportedTypes;

    public static List<string> SupportedTypeNames => PackageSettingValue.SupportedTypeNames;

    public string Key
    {
        get => _key;
        set { _key = value; OnPropertyChanged(); }
    }

    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// The original type of the value, used when saving back
    /// </summary>
    public Type ValueType
    {
        get => _valueType;
        set
        {
            _valueType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TypeName));
            OnPropertyChanged(nameof(SelectedTypeIndex));
        }
    }
    private Type _valueType = typeof(string);

    private bool _isArray;
    public bool IsArray
    {
        get => _isArray;
        set
        {
            _isArray = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TypeName));
        }
    }

    public string TypeName
    {
        get
        {
            var name = ValueType?.Name ?? "String";
            return _isArray ? name + "[]" : name;
        }
    }

    public int SelectedTypeIndex
    {
        get => SupportedTypes.IndexOf(ValueType);
        set
        {
            if (value >= 0 && value < SupportedTypes.Count)
            {
                ValueType = SupportedTypes[value];
            }
        }
    }

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            _isEditing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EditingVisibility));
            OnPropertyChanged(nameof(ViewVisibility));
        }
    }

    public Microsoft.UI.Xaml.Visibility EditingVisibility =>
        _isEditing ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public Microsoft.UI.Xaml.Visibility ViewVisibility =>
        _isEditing ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

    private bool _isError;
    public bool IsError
    {
        get => _isError;
        set
        {
            _isError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ErrorVisibility));
        }
    }

    public Microsoft.UI.Xaml.Visibility ErrorVisibility =>
        _isError ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    /// <summary>
    /// Snapshot current values before editing so we can revert on cancel
    /// </summary>
    private bool _originalIsArray;

    public void BeginEdit()
    {
        _originalKey = _key;
        _originalValue = _value;
        _originalType = _valueType;
        _originalIsArray = _isArray;
        IsEditing = true;
    }

    /// <summary>
    /// Accept the edit (just exit edit mode)
    /// </summary>
    public void CommitEdit()
    {
        IsEditing = false;
    }

    /// <summary>
    /// Revert to the values before editing started
    /// </summary>
    public void CancelEdit()
    {
        Key = _originalKey;
        Value = _originalValue;
        ValueType = _originalType;
        IsArray = _originalIsArray;
        IsError = false;
        IsEditing = false;
    }

    public event PropertyChangedEventHandler PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
