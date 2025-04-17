using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;
using Windows.Win32;

namespace ViewAppxPackage
{
    public sealed partial class SettingEditBox : UserControl
    {
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

        public PackageSettingBase PackageSettingValue
        {
            get { return (PackageSettingBase)GetValue(PackageSettingValueProperty); }
            set { SetValue(PackageSettingValueProperty, value); }
        }
        public static readonly DependencyProperty PackageSettingValueProperty =
            DependencyProperty.Register("PackageSettingValue", typeof(PackageSettingBase), typeof(SettingEditBox),
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
            // Keep track if selection happens by clicking with the mouse
            // If selection happens by mouse, it's during the pointer-down
            if (IsPrimaryPointerButtonPressed())
            {
                if (IsSelected)
                {
                    _justSelectedByPointer = true;
                }
            }
            else
            {
                _justSelectedByPointer = false;
            }
        }

        // True if selection changed because a pointer pressed on the value TextBlock
        bool _justSelectedByPointer = false;

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

        /// <summary>
        /// Save the value in the TextBox back to the system if the new value is valid
        /// </summary>
        bool TrySave()
        {
            TestParse();

            // Try to parse the new value according to the type we read in originally
            if (TryParseValue(PackageSettingValue.ValueType, NewValue, out var parsedNewValue))
            {
                // We have a valid new value. Write it to the ApplicationDataContainer
                // Wrap in try because these APIs throw a lot
                // Bugbug: should this be done off UI thread?
                try
                {
                    // Set to the correct container
                    var parentContainer = PackageView.GetAppDataContainerForPackage(PackageSettingValue.Package, PackageSettingValue);
                    parentContainer.Values[PackageSettingValue.Name] = parsedNewValue;

                    Debug.Assert(parsedNewValue is not null);
                    if (parsedNewValue is null)
                    {
                        return false;
                    }

                    // Also write it to the model
                    PackageSettingValue.ValueAsString = PackageSettingBase.ConvertSettingValueToString(parsedNewValue);

                    DebugLog.Append($"Saved setting `{PackageSettingValue.Name}`: {parsedNewValue}");
                    return true;
                }
                catch (Exception e)
                {
                    DebugLog.Append(e, $"Failed to update setting: {PackageSettingValue.Name}");
                    return false;
                }
            }
            else
            {
                if (PackageSettingValue.ValueType != typeof(ApplicationDataCompositeValue))
                {
                    DebugLog.Append($"Couldn't parse {PackageSettingValue.Name} as {PackageSettingValue.ValueType.Name}: {NewValue}");
                }
            }

            return false;
        }

        //https://docs.microsoft.com/uwp/api/Windows.Foundation.PropertyType
        //public enum PropertyType
        //{
        //    Boolean = 11,
        //    BooleanArray = 1035,
        //    Char16 = 10,
        //    Char16Array = 1034,
        //    DateTime = 14,
        //    DateTimeArray = 1038,
        //    Double = 9,
        //    DoubleArray = 1033,
        //    Empty = 0,
        //    Guid = 16,
        //    GuidArray = 1040,
        //    Inspectable = 13,
        //    InspectableArray = 1037,
        //    Int16 = 2,
        //    Int16Array = 1026,
        //    Int32 = 4,
        //    Int32Array = 1028,
        //    Int64 = 6,
        //    Int64Array = 1030,
        //    OtherType = 20,
        //    OtherTypeArray = 1044,
        //    Point = 17,
        //    PointArray = 1041,
        //    Rect = 19,
        //    RectArray = 1043,
        //    Single = 8,
        //    SingleArray = 1032,
        //    Size = 18,
        //    SizeArray = 1042,
        //    String = 12,
        //    StringArray = 1036,
        //    TimeSpan = 15,
        //    TimeSpanArray = 1039,
        //    UInt16 = 3,
        //    UInt16Array = 1027,
        //    UInt32 = 5,
        //    UInt32Array = 1029,
        //    UInt64 = 7,
        //    UInt64Array = 1031,
        //    UInt8 = 1,
        //    UInt8Array = 1025,
        //}

        /// <summary>
        /// Parse a string according to the specified type.
        /// </summary>
        internal static bool TryParseValue(Type type, string value, out object parsedValue)
        {
            parsedValue = null;

            try
            {
                if (type == typeof(ApplicationDataCompositeValue))
                {
                    // This is the only valid type that can't parsed yet
                    return false;
                }

                // Recurse for arrays
                if (type.IsArray)
                {
                    var arrayType = type.GetElementType();

                    // Types with commas (like Point or String) are separated by lines.
                    // Everything else (default case) is separated by commas
                    switch (arrayType)
                    {
                        case Type t when t == typeof(Size):
                            var lines = value.Split('\r');
                            var strings = new String[lines.Length];
                            for (int i = 0; i < lines.Length; i++)
                            {
                                strings[i] = lines[i].Trim();
                            }
                            parsedValue = strings;
                            return true;

                        case Type t when t == typeof(Point):
                            lines = value.Split('\r');
                            var points = new Point[lines.Length];
                            for (int i = 0; i < lines.Length; i++)
                            {
                                points[i] = ParsePoint(lines[i]);
                            }
                            parsedValue = points;
                            return true;

                        case Type t when t == typeof(Rect):
                            lines = value.Split('\r');
                            var rects = new Rect[lines.Length];
                            for (int i = 0; i < lines.Length; i++)
                            {
                                rects[i] = ParseRect(lines[i]);
                            }
                            parsedValue = rects;
                            return true;

                        case Type t when t == typeof(Size):
                            lines = value.Split('\r');
                            var sizes = new Size[lines.Length];
                            for (int i = 0; i < lines.Length; i++)
                            {
                                sizes[i] = ParseSize(lines[i]);
                            }
                            parsedValue = sizes;
                            return true;

                        default:
                            lines = value.Split(',');
                            var array = Array.CreateInstance(arrayType, lines.Length);
                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (!TryParseValue(arrayType, lines[i], out var element))
                                {
                                    return false;
                                }
                                array.SetValue(element, i);
                            }
                            parsedValue = array;
                            return true;
                    }
                }

                // Parse the non-array value according to the type
                parsedValue = type switch
                {
                    Type t when t == typeof(bool)
                        => bool.Parse(value),

                    Type t when t == typeof(int)
                        => int.Parse(value),
                    Type t when t == typeof(long)
                        => long.Parse(value),
                    Type t when t == typeof(short)
                        => short.Parse(value),
                    Type t when t == typeof(char) // Char16
                        => char.Parse(value),

                    Type t when t == typeof(uint)
                        => uint.Parse(value),
                    Type t when t == typeof(ulong)
                        => ulong.Parse(value),
                    Type t when t == typeof(ushort)
                        => ushort.Parse(value),
                    Type t when t == typeof(byte)
                        => byte.Parse(value),

                    Type t when t == typeof(float)
                        => float.Parse(value),
                    Type t when t == typeof(double)
                        => double.Parse(value),
                    Type t when t == typeof(Single)
                        => Single.Parse(value),

                    Type t when t == typeof(DateTimeOffset)
                        => DateTimeOffset.Parse(value),
                    Type t when t == typeof(DateTime)
                        => DateTime.Parse(value),
                    Type t when t == typeof(TimeSpan)
                        => TimeSpan.Parse(value),

                    Type t when t == typeof(Guid)
                        => Guid.Parse(value),
                    Type t when t == typeof(Point)
                        => ParsePoint(value),
                    Type t when t == typeof(Rect)
                        => ParseRect(value),
                    Type t when t == typeof(Size)
                        => ParseSize(value),

                    Type t when t == typeof(string)
                        => value,

                    _ => throw new ArgumentException("Invalid value")
                };
            }
            catch (Exception e)
            {
                DebugLog.Append(e, $"Couldn't parse as {type.Name}: `{value}`");
                return false;
            }

            return true;
        }

        // bugbug: replace this with a unit test
        [Conditional("DEBUG")]
        void TestParse()
        {
            var point = ParsePoint("1,2");
            Debug.Assert(point == (new Point(1, 2)));

            var rect = ParseRect("1,2,3,4");
            Debug.Assert(rect == new Rect(1, 2, 3, 4));

            var size = ParseSize("1,2");
            Debug.Assert(size == new Size(1, 2));
        }

        static Point ParsePoint(string pointString)
        {
            var coordinates = pointString.Split(',');

            if (coordinates.Length == 2 &&
                double.TryParse(coordinates[0], out double x) &&
                double.TryParse(coordinates[1], out double y))
            {
                return new Point(x, y);
            }
            else
            {
                throw new Exception($"Failed to parse point: {pointString}");
            }
        }

        static Rect ParseRect(string rectString)
        {
            string[] values = rectString.Split(',');

            if (values.Length == 4 &&
                double.TryParse(values[0], out double x) &&
                double.TryParse(values[1], out double y) &&
                double.TryParse(values[2], out double width) &&
                double.TryParse(values[3], out double height))
            {
                return new Rect(x, y, width, height);
            }
            else
            {
                throw new Exception($"Couldn't parse Rect: {rectString}");
            }
        }

        static Size ParseSize(string sizeString)
        {
            string[] values = sizeString.Split(',');

            if (values.Length == 2 &&
                double.TryParse(values[0], out double width) &&
                double.TryParse(values[1], out double height))
            {
                return new Size(width, height);
            }
            else
            {
                throw new Exception($"Couldn't parse size: {sizeString}");
            }
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

        //private bool IsAnyModifierKeyPressed()
        //{
        //    return IsKeyPressed(VirtualKey.Shift)
        //        || IsKeyPressed(VirtualKey.Control)
        //        || IsKeyPressed(VirtualKey.Menu);
        //}

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
            // Possible to get another pointer-pressed before the Tapped
            _justSelectedByPointer = false;
        }

        /// <summary>
        /// See if the left mouse button (or equivalent) is down
        /// </summary>
        static bool IsPrimaryPointerButtonPressed()
        {
            // GetKeyState returns a short where the least significant bit indicates the button state
            short state = PInvoke.GetKeyState((int)VirtualKey.LeftButton);
            return (state & 0x8000) != 0;
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

            Debug.Assert(!_justSelectedByPointer);
            _justSelectedByPointer = false;

            // Validate that we can edit this type by seeing if we can parse the value that comes from the actual package setting
            if (!TryParseValue(PackageSettingValue.ValueType, PackageSettingValue.ValueAsString, out var parsedValue)
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

            if (TrySave())
            {
                IsError = false;
                IsEditing = false;
                _textBlock.Focus(FocusState.Programmatic);
            }
            else
            {
                IsError = true;
            }
        }

        private void _textBlock_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // If the TextBlock is tapped while the item is selected, go into edit mode
            // The trick is figuring out that condition.
            // Selection happens during PointerPressed, so by the time Tapped occurs we're already selected
            // So we don't know if we were selected before the tap or during it
            // Event pattern
            //     PointerPressed
            //     IsSelected
            //     ... message pump ...
            //     PointerReleased
            //     Tapped
            // 
            // So what we do is track if IsSelected changes while the pointer button is pressed
            // If so, ignore the Tapped event

            if (IsSelected && !IsEditing && !_justSelectedByPointer)
            {
                StartEditing();
            }

            _justSelectedByPointer = false;
        }
    }
}
