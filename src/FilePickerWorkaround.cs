namespace ViewAppxPackage;

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls.Dialogs;

/// <summary>
/// Workaround for FileOpenPicker not working in an elevated app
/// </summary>
public static class FilePickerWorkaround
{
    /// <summary>
    /// Show the ComCtl File Open dialog (because FileOpenPicker doesn't work in elevated apps)
    /// bugbug: is there any benefit to using FileOpenPicker rather than this?
    /// </summary>
    unsafe public static string ShowDialog(IList<string> filters, string dialogTitle)
    {
        var ofn = new OPENFILENAMEW();
        ofn.lStructSize = (uint)Marshal.SizeOf<OPENFILENAMEW>();

        // Convert the filters string list to a single string separated by \0
        StringBuilder filterBuilder = new();
        filterBuilder.Append("All Files\0*.*\0");
        foreach (string oneFilter in filters)
        {
            // The component strings are separated by null terminators.
            // But c# strings are length-based like BSTRs, and so not confused by the terminator like c++ is
            filterBuilder.Append($"{oneFilter}\0*{oneFilter}\0");
        }

        // I think this is one or two more terminators than necessary, but doesn't hurt so worth the peace of mind
        filterBuilder.Append("\0\0");
        var filterString = filterBuilder.ToString();

        // Buffer to hold the result
        var filenameBuffer = new char[260];
        filenameBuffer[0] = '\0';

        // Fix the strings we'll use the PInvoke call so they don't get relocated by GC
        fixed (
            char* filterFixed = filterString,
            filenameBufferFixed = &filenameBuffer[0],
            title = dialogTitle)
        {
            PCWSTR filterPcwstr = new PCWSTR(filterFixed);
            ofn.lpstrFilter = filterPcwstr;

            ofn.lpstrFile = filenameBufferFixed;
            ofn.nMaxFile = (uint)filenameBuffer.Length;
            Debug.Assert(ofn.nMaxFile == PInvoke.MAX_PATH);

            ofn.lpstrTitle = title;

            if (PInvoke.GetOpenFileName(ref ofn))
            {
                return ofn.lpstrFile.ToString();
            }

            return string.Empty;
        }
    }
}

