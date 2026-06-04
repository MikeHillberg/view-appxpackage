using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.Win32;

namespace ViewAppxPackage;

/// <summary>
/// Encapsulates the "click on already-selected item to enter edit mode" interaction pattern.
/// 
/// The challenge: selection happens during PointerPressed, but we want to distinguish between
/// "clicking to select" vs "clicking on an already-selected item". By the time Tapped fires,
/// the item is already selected either way.
/// 
/// The solution: track whether selection changed while the pointer button was pressed.
/// If it did, this is a "selecting" click, not an "edit" click.
/// 
/// Two patterns are provided because TreeView and ListView surface selection state differently:
///
/// Pattern 1 (TreeViewItem): TreeViewItems have an IsSelected property that changes on the
/// item itself. The helper tracks whether the pointer button was pressed at the time of
/// selection change, using OnPointerPressed() on the text element to reset the flag.
///   - Call OnTreeViewSelectionChanged(isSelected) when IsSelected changes
///   - Call OnPointerPressed() in the text element's PointerPressed handler  
///   - Call ShouldEdit() in the Tapped handler; returns true if edit should begin
///
/// Pattern 2 (ListView): ListView doesn't expose per-item IsSelected property changes.
/// Instead, it fires a single SelectionChanged event on the list. SelectionChanged fires
/// *before* Tapped, so ShouldEditInListView checks whether selection changed during the
/// current interaction. If it did, the click was to select (not to edit).
///   - Call OnListViewSelectionChanged() in the ListView's SelectionChanged handler
///   - Call ShouldEditInListView(isCurrentlySelected) in the item's Tapped handler; returns true if edit should begin
/// </summary>
internal class ClickToEditHelper
{
    private bool _justSelectedByPointer;
    // --- Pattern 1: For use with TreeViewItem (has per-item IsSelected property) ---

    /// <summary>
    /// Call when the TreeViewItem's IsSelected property changes.
    /// Tracks whether the selection change happened while the pointer was pressed.
    /// </summary>
    public void OnTreeViewSelectionChanged(bool isSelected)
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
    public void OnPointerPressed()
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

    // --- Pattern 2: For use with ListView (no per-item IsSelected; uses list-level SelectionChanged) ---

    /// <summary>
    /// Call in the ListView's SelectionChanged handler.
    /// If selection changed, the click was to select, not to edit.
    /// </summary>
    public void OnListViewSelectionChanged()
    {
        _selectionChangedDuringThisInteraction = true;
    }

    /// <summary>
    /// Call in the item's Tapped handler for ListView usage.
    /// Returns true if the tapped item was already selected (i.e., edit should begin).
    /// SelectionChanged fires before Tapped when the selection changes, so if it hasn't
    /// fired by now, the item was already selected.
    /// </summary>
    public bool ShouldEditInListView(bool isItemCurrentlySelected)
    {
        var result = isItemCurrentlySelected && !_selectionChangedDuringThisInteraction;
        _selectionChangedDuringThisInteraction = false;
        return result;
    }

    private bool _selectionChangedDuringThisInteraction;

    // --- Shared utility ---

    /// <summary>
    /// See if the left mouse button (or equivalent) is down
    /// </summary>
    internal static bool IsPrimaryPointerButtonPressed()
    {
        short state = PInvoke.GetKeyState((int)VirtualKey.LeftButton);
        return (state & 0x8000) != 0;
    }
}
