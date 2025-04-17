using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
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
        }

        ApplicationDataContainer _targetContainer;

        public string ValueString
        {
            get { return (string)GetValue(ValueStringProperty); }
            set { SetValue(ValueStringProperty, value); }
        }
        public static readonly DependencyProperty ValueStringProperty =
            DependencyProperty.Register("ValueString", typeof(string), typeof(NewPackageSettingValue), 
                new PropertyMetadata("", (d,dp) => (d as NewPackageSettingValue).PropertyChanged()));

        public int SelectedIndex
        {
            get { return (int)GetValue(SelectedIndexProperty); }
            set { SetValue(SelectedIndexProperty, value); }
        }
        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register("SelectedIndex", typeof(int), typeof(NewPackageSettingValue),
                new PropertyMetadata(0, (d,dp) => (d as NewPackageSettingValue).PropertyChanged()));

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
            typeof(Single),
            typeof(DateTimeOffset),
            typeof(TimeSpan),
            typeof(Guid),
            typeof(Point),
            typeof(Rect),
            typeof(Size),
            typeof(string),
        };

        IEnumerable<string> TypeStrings;

        //private void Button_Click(object sender, RoutedEventArgs e)
        //{
        //    if(TryParseValue(out var parsedValue))
        //    {
        //        NewType = Types[SelectedIndex];
        //        NewValue = parsedValue;
        //        this.Hide();
        //    }
        //}

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
            if(IsArray)
            {
                type = type.MakeArrayType();
            }
            return SettingEditBox.TryParseValue(Types[this.SelectedIndex], ValueString, out parsedValue);
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
                new PropertyMetadata("", (d,dp) => (d as NewPackageSettingValue).PropertyChanged()));
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
            catch(Exception e)
            {
                DebugLog.Append(e, $"Failed evaluating new package setting value");
                return false;
            }
        }

        private void RootKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter
                && SettingEditBox.IsExactModifierKeyPressed(VirtualKeyModifiers.Control)
                && IsPrimaryButtonEnabled)
            {
                IsSubmittedProgramatically = true;
                this.Hide();
            }
        }
    }
}
