using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;

namespace ViewAppxPackage
{
    public sealed partial class NewPackageSettingValue : FormDialogBase
    {
        public NewPackageSettingValue(ApplicationDataContainer targetContainer)
        {
            _targetContainer = targetContainer;

            this.InitializeComponent();

            TypeStrings = from type in Types
                          select type.Name;
            SelectedIndex = Types.IndexOf(typeof(string));

            // Don't submit (save) on pressing just the Enter key
            this.SubmitOnEnter = false;
        }

        ApplicationDataContainer _targetContainer;

        public string ValueString
        {
            get { return (string)GetValue(ValueStringProperty); }
            set { SetValue(ValueStringProperty, value); }
        }
        public static readonly DependencyProperty ValueStringProperty =
            DependencyProperty.Register("ValueString", typeof(string), typeof(NewPackageSettingValue),
                new PropertyMetadata("", (d, dp) => (d as NewPackageSettingValue).PropertyChanged()));

        public int SelectedIndex
        {
            get { return (int)GetValue(SelectedIndexProperty); }
            set { SetValue(SelectedIndexProperty, value); }
        }
        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register("SelectedIndex", typeof(int), typeof(NewPackageSettingValue),
                new PropertyMetadata(0, (d, dp) => (d as NewPackageSettingValue).PropertyChanged()));

        List<Type> Types = new()
        {
            typeof(bool),
            typeof(int),
            typeof(long),
            typeof(short),
            typeof(char),
            typeof(uint),
            typeof(ulong),
            typeof(ushort),
            typeof(byte),
            typeof(float),
            typeof(double),
            typeof(DateTimeOffset),
            typeof(TimeSpan),
            typeof(Guid),
            typeof(Point),
            typeof(Rect),
            typeof(Size),
            typeof(string),
        };

        IEnumerable<string> TypeStrings;


        public Type NewType;
        public object NewValue;

        public bool IsDuplicateName
        {
            get { return (bool)GetValue(IsDuplicateNameProperty); }
            set { SetValue(IsDuplicateNameProperty, value); }
        }
        public static readonly DependencyProperty IsDuplicateNameProperty =
            DependencyProperty.Register("IsDuplicateName", typeof(bool), typeof(NewPackageSettingValue),
                new PropertyMetadata(false));

        public bool IsArray
        {
            get { return (bool)GetValue(IsArrayProperty); }
            set { SetValue(IsArrayProperty, value); }
        }
        public static readonly DependencyProperty IsArrayProperty =
            DependencyProperty.Register("IsArray", typeof(bool), typeof(NewPackageSettingValue), new PropertyMetadata(false));

        private bool TryParseValue(out object parsedValue)
        {
            var type = Types[this.SelectedIndex];
            if (IsArray)
            {
                type = type.MakeArrayType();
            }
            return SettingEditBox.TryParseValue(Types[this.SelectedIndex], ValueString, out parsedValue);
        }

        /// <summary>
        /// Produce a example string for the currently-selected type
        /// </summary>
        string ExampleString(int selectedIndex, bool isArray)
        {
            var type = Types[selectedIndex];
            return ExampleString(type, isArray);
        }

        /// <summary>
        /// Produce an example of a string-ized form of a setting type
        /// </summary>
        static internal string ExampleString(Type type, bool isArray)
        {
            Debug.Assert(!type.IsArray);

            const string date1 = "4/4/1975 8:00am";
            const string date2 = "10/6/2012 5:00pm";
            const string time1 = "1:00:00";
            const string time2 = "2:00:00";
            const string guid1 = "12345678-1234-1234-1234-123456789012";
            const string guid2 = "12345678-1234-1234-1234-123456789013";
            const string twoInts1 = "1, 2";
            const string twoInts2 = "3, 4";
            const string rect1 = "0, 0, 1024, 768";
            const string rect2 = "0, 0, 2256, 1504";
            const string int1 = "123";
            const string int2 = "1, 2, 3";
            const string float1 = "3.141";
            const string float2 = "2.718";
            const string string1 = "Hello world";
            const string string2 = "How are you?";
            const string bool1 = "True";
            const string bool2 = "False";
            const string char1 = "a";
            const string char2 = "b";

            // Assume initially that it's not an array
            string example = type switch
            {
                Type t when t == typeof(bool) => bool1,
                Type t when t == typeof(int) => int1,
                Type t when t == typeof(long) => int1,
                Type t when t == typeof(short) => int1,
                Type t when t == typeof(char) => char1,
                Type t when t == typeof(uint) => int1,
                Type t when t == typeof(ulong) => int1,
                Type t when t == typeof(ushort) => int1,
                Type t when t == typeof(byte) => int1,
                Type t when t == typeof(float) => float1,
                Type t when t == typeof(double) => float1,
                Type t when t == typeof(DateTimeOffset) => date1,
                Type t when t == typeof(TimeSpan) => time1,
                Type t when t == typeof(Guid) => guid1,
                Type t when t == typeof(Point) => twoInts1,
                Type t when t == typeof(Rect) => rect1,
                Type t when t == typeof(Size) => twoInts1,
                Type t when t == typeof(string) => string1,
                _ => "?",
            };

            // Modify that if it is an array
            if (isArray)
            {
                example = example switch
                {
                    bool1 => $"{bool1}, {bool2}",
                    char1 => $"{char1}, {char2}",
                    int1 => int2,
                    float1 => $"{float1}, {float2}",
                    twoInts1 => $"{twoInts1}\r{twoInts2}",
                    rect1 => $"{rect1}\r{rect2}",
                    string1 => $"{string1}\r{string2}",
                    date1 => $"{date1}, {date2}",
                    time1 => $"{time1}, {time2}",
                    guid1 => $"{guid1}, {guid2}",
                    _ => "?",
                };
            }

            Debug.Assert(example != "?" || type.IsAssignableTo(typeof(ApplicationDataCompositeValue)));

            return example;
        }

        public bool CanSave
        {
            get { return (bool)GetValue(CanSaveProperty); }
            set { SetValue(CanSaveProperty, value); }
        }
        public static readonly DependencyProperty CanSaveProperty =
            DependencyProperty.Register("CanSave", typeof(bool), typeof(NewPackageSettingValue), new PropertyMetadata(false));

        public string SettingName
        {
            get { return (string)GetValue(SettingNameProperty); }
            set { SetValue(SettingNameProperty, value); }
        }
        public static readonly DependencyProperty SettingNameProperty =
            DependencyProperty.Register("SettingName", typeof(string), typeof(NewPackageSettingValue),
                new PropertyMetadata("", (d, dp) => (d as NewPackageSettingValue).PropertyChanged()));
        void PropertyChanged()
        {
            this.IsValid = UpdateValueIfValid();
        }

        public bool IsInvalidValue
        {
            get { return (bool)GetValue(IsInvalidValueProperty); }
            set { SetValue(IsInvalidValueProperty, value); }
        }
        public static readonly DependencyProperty IsInvalidValueProperty =
            DependencyProperty.Register("IsInvalidValue", typeof(bool), typeof(NewPackageSettingValue), new PropertyMetadata(false));

        bool UpdateValueIfValid()
        {
            IsDuplicateName = false;
            IsInvalidValue = false;

            try
            {
                if (string.IsNullOrEmpty(this.SettingName))
                {
                    return false;
                }

                if (_targetContainer.Values.ContainsKey(this.SettingName))
                {
                    IsDuplicateName = true;
                }

                if (this.SelectedIndex < 0)
                {
                    return false;
                }

                if (string.IsNullOrEmpty(this.ValueString))
                {
                    return false;
                }

                if (!TryParseValue(out var parsedValue))
                {
                    IsInvalidValue = true;
                }

                if (IsDuplicateName || IsInvalidValue)
                {
                    return false;
                }

                NewValue = parsedValue;
                return true;
            }
            catch (Exception e)
            {
                DebugLog.Append(e, $"Failed evaluating new package setting value");
                return false;
            }
        }

    }
}
