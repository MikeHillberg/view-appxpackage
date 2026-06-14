using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;
using Windows.Win32;

namespace ViewAppxPackage;

public sealed partial class SettingEditBox : UserControl
{
    readonly TreeListViewClickToEditHelper _clickToEdit = new();

    public SettingEditBox()
    {
        this.InitializeComponent();

        // Events to help figure out when to start editing
        // Using AddHandler in order to use handledEventsToo
        _textBlock.AddHandler(PointerPressedEvent, new PointerEventHandler(TextBlock_PointerPressed), handledEventsToo: true);
        _textBlock.AddHandler(TappedEvent, new TappedEventHandler(_textBlock_Tapped), handledEventsToo: true);

        this.LostFocus += (s, e) => LostFocusHandler();
    }

    // Watch for lost focus events to commit changes when focus moves out of this control
    void LostFocusHandler()
    {
        // Ignore transitions through the null state
        var target = FocusManager.GetFocusedElement(this.XamlRoot) as FrameworkElement;
        if (target == null)
        {
            return;
        }

        // If focus moved outside of this control, commit the changed value
        if (!IsFocusWithin(this))
        {
            SaveAndExitEditing();
        }
    }

    /// <summary>
    /// True if focus is in the visual tree below the given element
    /// </summary>
    bool IsFocusWithin(FrameworkElement element)
    {
        // bugbug: calling GetFocusedElement() with no parameters seems to always return null?
        var target = FocusManager.GetFocusedElement(this.XamlRoot) as FrameworkElement;
        while (true)
        {
            if (target == null)
            {
                return false;
            }

            if (Object.ReferenceEquals(target, element))
            {
                return true;
            }

            target = VisualTreeHelper.GetParent(target) as FrameworkElement;
        }
    }

    public PackageSettingValue PackageSettingValue
    {
        get { return (PackageSettingValue)GetValue(PackageSettingValueProperty); }
        set { SetValue(PackageSettingValueProperty, value); }
    }
    public static readonly DependencyProperty PackageSettingValueProperty =
        DependencyProperty.Register("PackageSettingValue", typeof(PackageSettingValue), typeof(SettingEditBox),
            new PropertyMetadata(null));

    public PackageModel Package { get; set; }

    /// <summary>
    /// This is true if the setting is selected in the TreeView
    /// </summary>
    public bool IsSelected
    {
        get { return (bool)GetValue(IsSelectedProperty); }
        set { SetValue(IsSelectedProperty, value); }
    }
    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register("IsSelected", typeof(bool), typeof(SettingEditBox),
            new PropertyMetadata(false, (d, dp) => (d as SettingEditBox).IsSelectedChanged()));

    void IsSelectedChanged()
    {
        _clickToEdit.OnSelectionChanged(IsSelected);
    }

    /// <summary>
    /// This is the value in the TextBox being edited
    /// </summary>
    public string NewValue
    {
        get { return (string)GetValue(NewValueProperty); }
        set { SetValue(NewValueProperty, value); }
    }
    public static readonly DependencyProperty NewValueProperty =
        DependencyProperty.Register("NewValue", typeof(string), typeof(SettingEditBox), new PropertyMetadata(null));

    /// <summary>
    /// This triggers the flip between the TextBlock and the TextBox mode
    /// </summary>
    public bool IsEditing
    {
        get { return (bool)GetValue(IsEditingProperty); }
        set { SetValue(IsEditingProperty, value); }
    }
    public static readonly DependencyProperty IsEditingProperty =
        DependencyProperty.Register("IsEditing", typeof(bool), typeof(SettingEditBox),
            new PropertyMetadata(false, (d, dp) => (d as SettingEditBox).IsEditingChanged()));
    void IsEditingChanged()
    {
        DebugLog.Append($"IsEditing `{PackageSettingValue.Name}`: {IsEditing}");
    }

    /// <summary>
    /// True if the type is an array
    /// </summary>
    public bool IsUnsupportedType
    {
        get { return (bool)GetValue(IsUnsupportedTypeProperty); }
        set { SetValue(IsUnsupportedTypeProperty, value); }
    }
    public static readonly DependencyProperty IsUnsupportedTypeProperty =
        DependencyProperty.Register("IsUnsupportedType", typeof(bool), typeof(SettingEditBox), new PropertyMetadata(false));

    /// <summary>
    /// True if user e.g. tries to set an int to "foo"
    /// </summary>
    public bool IsError
    {
        get { return (bool)GetValue(IsErrorProperty); }
        set { SetValue(IsErrorProperty, value); }
    }
    public static readonly DependencyProperty IsErrorProperty =
        DependencyProperty.Register("IsError", typeof(bool), typeof(SettingEditBox), new PropertyMetadata(false));

    string ExampleString(Type type)
    {
        var isArray = type.IsArray;
        if(isArray)
        {
            type = type.GetElementType();
        }
        return NewPackageSettingValue.ExampleString(type, isArray);
    }

    private void _textBox_Loaded(object sender, RoutedEventArgs e)
    {
        _textBox.AddHandler(PreviewKeyDownEvent, new KeyEventHandler(TextBox_KeyDown), true);
    }

    private void TextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        // Escape key reverts the change and goes back to view mode
        // Control+Enter key commits the edit and leaves edit mode (unless the new value is bad)

        if (e.Key == VirtualKey.Escape)
        {
            CancelEditing();
            return;
        }
        else if (e.Key != VirtualKey.Enter)
        {
            // Not Cancel and not Enter
            return;
        }

        // See if this is specifically Ctrl+Enter
        if (IsExactModifierKeyPressed(VirtualKeyModifiers.Control))
        {
            // Ctrl+Enter means commit
            e.Handled = true;
            SaveAndExitEditing();
        }
    }

    /// <summary>
    /// See if a keyboard modifier and only that modifier(s) is pressed
    /// </summary>
    static internal bool IsExactModifierKeyPressed(VirtualKeyModifiers modifiers)
    {
        var currentModifiers = GetKeyModifiers();
        if (!currentModifiers.HasFlag(modifiers))
        {
            return false;
        }

        // The modifier is  set. See if anything else is too, ortherwise return true

        var allModifiers = VirtualKeyModifiers.Menu
            | VirtualKeyModifiers.Control
            | VirtualKeyModifiers.Windows
            | VirtualKeyModifiers.Shift;

        var otherModifiers = allModifiers & (~modifiers);
        return (otherModifiers & modifiers) == 0;
    }

    /// <summary>
    /// Figure out which if any of the keyboard modifiers are currently pressed
    static internal VirtualKeyModifiers GetKeyModifiers()
    {
        var modifiers = VirtualKeyModifiers.None;

        if (IsKeyPressed(VirtualKey.Shift))
        {
            modifiers |= VirtualKeyModifiers.Shift;
        }

        if (IsKeyPressed(VirtualKey.Control))
        {
            modifiers |= VirtualKeyModifiers.Control;
        }

        if (IsKeyPressed(VirtualKey.Menu))
        {
            modifiers |= VirtualKeyModifiers.Menu;
        }

        if (IsKeyPressed(VirtualKey.LeftWindows) || IsKeyPressed(VirtualKey.RightWindows))
        {
            modifiers |= VirtualKeyModifiers.Windows;
        }

        return modifiers;
    }

    static internal bool IsKeyPressed(VirtualKey key)
    {
        var keyboardSource = InputKeyboardSource.GetForIsland(MainWindow.RootElement.XamlRoot.ContentIsland);
        var keyState = keyboardSource.GetKeyState(key);
        return keyState.HasFlag(VirtualKeyStates.Down);
    }

    private void TextBlock_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _clickToEdit.OnPointerPressedIn();
    }

    /// <summary>
    /// Go into edit mode if possible
    /// </summary>
    public void StartEditing()
    {
        if (IsEditing)
        {
            return;
        }

        // Composite values get their own editor dialog
        if (PackageSettingValue.ValueType == typeof(ApplicationDataCompositeValue))
        {
            var dialog = new CompositeValueEditorWindow(PackageSettingValue);
            _ = dialog.ShowAsync();
            return;
        }

        // Validate that we can edit this type by seeing if we can parse the value that comes from the actual package setting
        if (!ViewAppxPackage.PackageSettingValue.TryParseValue(PackageSettingValue.ValueType, PackageSettingValue.ValueAsString, out var parsedValue)
            || PackageSettingBase.ConvertSettingValueToString(parsedValue) != PackageSettingValue.ValueAsString)
        {
            // Turn on an error message
            IsUnsupportedType = true;
            return;
        }
        IsUnsupportedType = false;

        // Switch from TextBlock to TextBox
        IsEditing = true;

        // If this isn't set, TextBox will truncate the input value at the end of the first line
        // This has to be set before setting the value (updating NewValue)?
        _textBox.AcceptsReturn = PackageSettingValue.ValueType == typeof(string) || PackageSettingValue.ValueType.IsArray;

        // Put the current value into the TextBox
        // Clear it first, though, because otherwise the text in the TextBox can get truncated,
        // but NewValue hasn't changed, so setting ValueAsString to it doesn't update the TextBox.
        // (The truncation happens if AcceptsReturn is set to false)
        NewValue = null;
        NewValue = PackageSettingValue.ValueAsString;

        // Focus the TextBox which for some reason we can't do on the current event
        // (probably because the TextBox isn't Loaded yet)
        MyThreading.PostToUI(() =>
        {
            _textBox.Focus(FocusState.Programmatic);
            _textBox.SelectAll();
        });
    }

    /// <summary>
    /// Revert the change and go back to view mode
    /// </summary>
    private void CancelEditing()
    {
        DebugLog.Append("CancelEditing");
        IsEditing = false;
        IsError = false;
    }

    /// <summary>
    /// Save the value from the TextBox and flip back to the TextBlock.
    /// Stay in edit mode if the value is invalid though
    /// </summary>
    public void SaveAndExitEditing()
    {
        if (!IsEditing)
        {
            return;
        }

        if ((PackageSettingValue as PackageSettingValue).TrySave(NewValue))
        {
            IsError = false;
            IsEditing = false;
        }
        else
        {
            IsError = true;
        }
    }

    private void _textBlock_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (_clickToEdit.ShouldEdit(IsSelected, IsEditing))
        {
            StartEditing();
        }
    }
}
