using Windows.System;
using Windows.Win32;

namespace ViewAppxPackage;

/// <summary>
/// Encapsulates the "click on already-selected item to enter edit mode" interaction pattern.
/// </summary>
/// <remarks>
/// Selection happens during PointerPressed, but we want to distinguish within there between
/// "clicking to select" vs "clicking on an already-selected item". By the time Tapped fires,
/// the item is already selected either way.
/// 
/// The solution: track whether selection changed while the pointer button was pressed.
/// If it did, this is a "selecting" click, not an "edit" click.
///
/// TreeViewItems have an IsSelected property that changes on the item itself. The helper
/// tracks whether the pointer button was pressed at the time of selection change, using
/// OnPointerPressed() on the text element to reset the flag.
///   - Call OnSelectionChanged(isSelected) when IsSelected changes
///   - Call OnPointerPressed() in the text element's PointerPressed handler  
///   - Call ShouldEdit() in the Tapped handler; returns true if edit should begin
/// </remarks>
internal class TreeListViewClickToEditHelper
{
    private bool _justSelectedByPointer;

    /// <summary>
    /// Call when the TreeViewItem's IsSelected property changes.
    /// Tracks whether the selection change happened while the pointer was pressed.
    /// </summary>
    public void OnSelectionChanged(bool isSelected)
    {
        if (IsPrimaryPointerButtonPressed())
        {
            if (isSelected)
            {
                _justSelectedByPointer = true;
            }
        }
        else
        {
            _justSelectedByPointer = false;
        }
    }

    /// <summary>
    /// Call in the text element's PointerPressed handler
    /// </summary>
    public void OnPointerPressedIn()
    {
        _justSelectedByPointer = false;
    }

    /// <summary>
    /// Call in the text element's Tapped handler.
    /// Returns true if the item was already selected (i.e., edit should begin).
    /// </summary>
    public bool ShouldEdit(bool isSelected, bool isEditing)
    {
        var result = isSelected && !isEditing && !_justSelectedByPointer;
        _justSelectedByPointer = false;
        return result;
    }


    /// <summary>
    /// See if the left mouse button (or equivalent) is down
    /// </summary>
    internal static bool IsPrimaryPointerButtonPressed()
    {
        short state = PInvoke.GetKeyState((int)VirtualKey.LeftButton);
        return (state & 0x8000) != 0;
    }
}
