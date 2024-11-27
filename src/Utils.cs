using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ViewAppxPackage
{
    internal class Utils
    {
        internal static Visibility IsVisible(bool value)
        {
            return value ? Visibility.Visible : Visibility.Collapsed;
        }

        internal static Visibility IsNotVisible(bool value)
        {
            return value ? Visibility.Collapsed : Visibility.Visible;
        }

        internal static bool Not(bool value)
        {
            return !value;
        }

        internal static bool NotAny(bool value1, bool value2)
        {
            return !(value1 || value2);
        }

        internal static bool IsntEmpty(string s)
        {
            return !string.IsNullOrEmpty(s);
        }

        internal static string PluralS(int count)
        {
            return count == 1 ? "" : "s";
        }
    }
}
