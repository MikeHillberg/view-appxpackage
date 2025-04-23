namespace ViewAppxPackage;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

/// <summary>
/// Common code for dialogs
/// </summary>
public class FormDialogBase : ContentDialog
{
    protected FormDialogBase()
    {
        // The dialog doesn't get key events. Wait for the subclass to construct, then hook PreviewKeyDown
        MyThreading.PostToUI(() =>
        {
            var root = this.Content as FrameworkElement;
            root.PreviewKeyDown += RootKeyDown;
        });
    }

    /// <summary>
    /// When this is set, close the dialog when the Enter key is pressed
    /// </summary>
    protected bool SubmitOnEnter { get; set; } = true;

    /// <summary>
    /// KeyDown handler that checks for Ctrl+Enter, which means submit
    /// </summary>
    private void RootKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter
            && (SubmitOnEnter && SettingEditBox.GetKeyModifiers() == VirtualKeyModifiers.None
                || SettingEditBox.IsExactModifierKeyPressed(VirtualKeyModifiers.Control))
            && IsValid)
        {
            // bugbug: can't find a way to close the dialog programatically and emulate that Save was clicked
            // So set this bool and Hide()
            IsSubmittedProgramatically = true;
            this.Hide();
        }
    }

    /// <summary>
    /// Set by the subclass, controls whether the Save button is enabled
    /// </summary>
    public bool IsValid
    {
        get { return (bool)GetValue(IsValidProperty); }
        set { SetValue(IsValidProperty, value); }
    }
    public static readonly DependencyProperty IsValidProperty =
        DependencyProperty.Register("IsValid", typeof(bool), typeof(FormDialogBase), 
            new PropertyMetadata(false, (d,dp) => (d as FormDialogBase).IsValidChanged()));
    void IsValidChanged()
    {
        IsPrimaryButtonEnabled = IsValid;
    }

    /// <summary>
    /// If this is set, interpret the dialog result as ContentDialogResult.Primary
    /// </summary>
    public bool IsSubmittedProgramatically
    {
        get { return (bool)GetValue(IsSubmittedProgramaticallyProperty); }
        set { SetValue(IsSubmittedProgramaticallyProperty, value); }
    }
    public static readonly DependencyProperty IsSubmittedProgramaticallyProperty =
        DependencyProperty.Register("IsSubmittedProgramatically", typeof(bool), typeof(FormDialogBase), new PropertyMetadata(false));
}
