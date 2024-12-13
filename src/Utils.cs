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

        /// <summary>
        /// Formats a date to a string. If the date is today, only the time is shown. Otherwise, the date and time are shown.
        /// </summary>
        public static string FormatDate(DateTimeOffset date)
        {
            TimeSpan timeDifference = DateTime.Now - date;

            //if (timeDifference.TotalHours < 24)
            if (date.Date == DateTime.Now.Date)
            {
                return date.ToString("t"); // Just the time
            }
            else
            {
                return date.ToString("g"); // 6/15/2009 1:45 PM
            }
        }

        /// <summary>
        /// Formats a byte size to a human-readable string.
        /// </summary>
        public static string FormatByteSize(long byteCount)
        {
            string[] suffixes = { "Bytes", "KB", "MB", "GB", "TB", "PB", "EB" };
            int index = 0;
            double bytes = byteCount;

            while (bytes >= 1024 && index < suffixes.Length - 1)
            {
                index++;
                bytes /= 1024;
            }

            var suffix = "";
            if(index < suffixes.Length)
            {
                suffix = suffixes[index];
            }

            return $"{bytes:0} {suffix}";
        }
    }
}
