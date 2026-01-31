using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Windows.Foundation;
using Windows.Storage;

namespace ViewAppxPackage
{
    // bugbug
    // Name property should be in PackageSettingBase as it is,
    // but Children should be in PackageSettingContainer and Value should be in PackageSetting.
    // And the templates should be x:DataType to PackageSettingContainer and PackageSetting.
    // But there appears to be a bug in ItemsControl where it caches the
    // IDataTemplateComponent (the genepackagerated Bindings code) on the templateRoot,
    // and it's getting the type wrong. (See XamlBindingHelperFactory::GetDataTemplateComponentStatic)
    // So it calls the generated binding code and asks it to set the item and gets a type cast exception.
    // As a workaround, both templates are x:DataType the base type, so no type cast exception.
    // That still means it's hitting the bug, but at least it's not crashing.
    // (Happens on YourPhone app, might requiring setting IsExpanded=true in the template)
    public partial class PackageSettingBase : ObservableObject
    {
        public PackageSettingBase(bool isRoaming)
        {
            This = this;
            IsRoaming = isRoaming;
        }

        /// <summary>
        /// True if this is from RoamingSettings rather than LocalSettings
        /// Real value comes from subclass
        /// </summary>
        public bool IsRoaming { get; private set; }

        public string Name { get; set; }
        public PackageSettingBase Parent;

        [ObservableProperty] // creates `ValueAsString` property
        private string valueAsString;

        public Type ValueType { get; set; }

        // Child Containers
        public IList<PackageSettingBase> Children;

        public KeyValuePair<string, object> KeyValuePair { get; internal set; }

        // bugbug: x:Bind doesn't support a way to bind to the data item itself
        public PackageSettingBase This { get; private set; }

        public PackageModel Package { get; set; }

        /// <summary>
        /// This is set for the fake setting that's used as a TreeView root
        /// </summary>
        public bool IsRoot { get; set; } = false;

        internal PackageSettingValue AsValue()
        {
            return this as PackageSettingValue;
        }

        /// <summary>
        /// Convert an object-typed setting value to a string in a way that's compatible with parsing
        /// </summary>
        internal static string ConvertSettingValueToString(object value)
        {
            if (value is null)
            {
                // Don't think this can happen
                return null;
            }

            // Should't be exceptions, but just in case there's a bug, no point in bringing down the app
            try
            {
                var type = value.GetType();

                // For arrays, string-ize the items, then put into a comma- or line-separated list
                if (type.IsAssignableTo(typeof(Array)))
                {
                    var array = value as Array;
                    if (array.Length == 0)
                    {
                        return string.Empty;
                    }
                    var itemType = array.GetValue(0).GetType();

                    // Figure out if this type has commas in it (meaning we can't put it into a comma-separated list)
                    var typeHasCommas = itemType.IsAssignableTo(typeof(string));
                    typeHasCommas |= itemType.IsAssignableTo(typeof(Point));
                    typeHasCommas |= itemType.IsAssignableTo(typeof(Rect));
                    typeHasCommas |= itemType.IsAssignableTo(typeof(Size));

                    // Recurse for each item and build the list in the  string builder
                    StringBuilder sb = new();
                    for (int i = 0; i < array.Length; i++)
                    {
                        // Add the string-ized value to the output
                        sb.Append(array.GetValue(i).ToString());

                        // Add a comma separator or a newline (except on the last item)
                        if (i + 1 < array.Length)
                        {
                            if (typeHasCommas)
                            {
                                sb.AppendLine();
                            }
                            else
                            {
                                sb.Append(", ");
                            }
                        }
                    }

                    return sb.ToString();
                }

                // ApplicationDataCompositeValue is a speial type that's a dictionary
                // Write it out as
                //    key
                //    value
                //    [blank line]
                else if (type.IsAssignableTo(typeof(Windows.Storage.ApplicationDataCompositeValue)))
                {
                    StringBuilder sb = new();
                    var compositeValue = value as Windows.Storage.ApplicationDataCompositeValue;
                    foreach (var kvp in compositeValue)
                    {
                        var key = kvp.Key;
                        var val = kvp.Value;

                        sb.AppendLine($"    {key}:");

                        var valString = val?.ToString();
                        valString = valString == null ? string.Empty : valString;
                        sb.AppendLine($"    {valString}");
                        sb.AppendLine();
                    }

                    return sb.ToString();
                }

                // Everything else (doesn't have commas, not the dictionary) just write as ToString
                else
                {
                    return value.ToString();
                }
            }
            catch (Exception e)
            {
                DebugLog.Append(e, $"Failed SettingValueToString, `{value}`");
                return "[Internal error]";
            }

        }
    }

    public class PackageSettingValue : PackageSettingBase
    {
        public PackageSettingValue(bool isRoaming) : base(isRoaming)
        {
        }

        //// To make this a required property it has to be set on the subclass
        //override public required bool IsRoaming { get; set; }

        /// <summary>
        /// Set a new value to this setting
        /// </summary>  
        public bool TrySave(string newValue)
        {
            // Try to parse the new value according to the type we read in originally
            if (TryParseValue(this.ValueType, newValue, out var parsedNewValue))
            {
                // We have a valid new value. Write it to the ApplicationDataContainer
                // Wrap in try because these APIs throw a lot
                try
                {
                    // Set to the correct container
                    var parentContainer = this.Package.GetAppDataContainerForSetting(this);
                    parentContainer.Values[this.Name] = parsedNewValue;

                    Debug.Assert(parsedNewValue is not null);
                    if (parsedNewValue is null)
                    {
                        return false;
                    }

                    //var applicationData = PackageSettingValue.Package.GetApplicationData();
                    //if (applicationData != null)
                    //{
                    //    applicationData.SignalDataChanged();
                    //}

                    // Also write it to the model
                    this.ValueAsString = PackageSettingBase.ConvertSettingValueToString(parsedNewValue);

                    DebugLog.Append($"Saved setting `{this.Name}`: {parsedNewValue}");
                    return true;
                }
                catch (Exception e)
                {
                    DebugLog.Append(e, $"Failed to update setting: {this.Name}");
                    return false;
                }
            }
            else
            {
                if (this.ValueType != typeof(ApplicationDataCompositeValue))
                {
                    DebugLog.Append($"Couldn't parse {this.Name} as {this.ValueType.Name}: {newValue}");
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
                        case Type t when t == typeof(string):
                            string[] lines = ReadLines(value);
                            var strings = new String[lines.Length];
                            for (int i = 0; i < lines.Length; i++)
                            {
                                strings[i] = lines[i].Trim();
                            }
                            parsedValue = strings;
                            return true;

                        case Type t when t == typeof(Point):
                            lines = ReadLines(value);
                            var points = new Point[lines.Length];
                            for (int i = 0; i < lines.Length; i++)
                            {
                                points[i] = ParsePoint(lines[i]);
                            }
                            parsedValue = points;
                            return true;

                        case Type t when t == typeof(Rect):
                            lines = ReadLines(value);
                            var rects = new Rect[lines.Length];
                            for (int i = 0; i < lines.Length; i++)
                            {
                                rects[i] = ParseRect(lines[i]);
                            }
                            parsedValue = rects;
                            return true;

                        case Type t when t == typeof(Size):
                            lines = ReadLines(value);
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
                                if (!TryParseValue(arrayType, lines[i].Trim(), out var element))
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

        public static Point ParsePoint(string pointString)
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

        public static Rect ParseRect(string rectString)
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

        public static Size ParseSize(string sizeString)
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

        /// <summary>
        /// Read a string into a string array of lines
        /// </summary>
        private static string[] ReadLines(string value)
        {
            StringReader reader = new(value);
            List<string> linesList = new();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                linesList.Add(line);
            }

            var lines = linesList.ToArray();
            return lines;
        }



    }

    public class PackageSettingContainer : PackageSettingBase
    {
        public PackageSettingContainer(bool isRoaming) : base(isRoaming)
        {
        }

        //// To make this a required property it has to be set on the subclass
        //override public required bool IsRoaming { get; set; }
    }
}
