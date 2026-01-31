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
        internal static Visibility IsVisibleIf(bool value)
        {
            return value ? Visibility.Visible : Visibility.Collapsed;
        }

        internal static Visibility IsVisibleIf(bool value1, bool value2)
        {
            return (value1 && value2) ? Visibility.Visible : Visibility.Collapsed;
        }

        internal static Visibility IsCollapsedIf(bool value)
        {
            return value ? Visibility.Collapsed : Visibility.Visible;
        }

        internal static bool IfNotNull(object value)
        {
            return value != null;
        }

        internal static bool And(bool val1, bool val2)
        {
            return val1 && val2;
        }

        internal static bool Or(bool val1, bool val2)
        {
            return val1 || val2;
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
                return date.ToString("t").ToLower(); // e.g. 1:45 pm
            }
            else if (date.Date == DateTime.Now.Date.Subtract(TimeSpan.FromDays(1)))
            {
                return $"yesterday at {date.ToString("t").ToLower()}";
            }
            else
            {
                return date.ToString("g").ToLower(); // e.g. 6/15/2009 1:45 pm
            }
        }

        public static string FormatDateOrTime(DateTimeOffset date)
        {
            TimeSpan timeDifference = DateTime.Now - date;

            //if (timeDifference.TotalHours < 24)
            if (date.Date == DateTime.Now.Date)
            {
                return date.ToString("t"); // Just the time
            }
            else
            {
                return date.Date.ToShortDateString();
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
            if (index < suffixes.Length)
            {
                suffix = suffixes[index];
            }

            return $"{bytes:0} {suffix}";
        }

        /// <summary>
        /// DependencyObject-like Tag property for non DOs
        /// </summary>
        public static object GetTag(DependencyObject obj)
        {
            return (object)obj.GetValue(TagProperty);
        }

        /// <summary>
        /// DependencyObject-like Tag property for non DOs
        /// </summary>
        public static void SetTag(DependencyObject obj, object value)
        {
            obj.SetValue(TagProperty, value);
        }

        /// <summary>
        /// DependencyObject-like Tag property for non DOs
        /// </summary>
        public static readonly DependencyProperty TagProperty =
            DependencyProperty.RegisterAttached("Tag", typeof(object), typeof(Utils), new PropertyMetadata(null));

    }
}
