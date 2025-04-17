using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;
using Windows.Foundation;

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
        public PackageSettingBase()
        {
            This = this;
        }

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
    }

    public class PackageSettingContainer : PackageSettingBase
    {
    }
}
