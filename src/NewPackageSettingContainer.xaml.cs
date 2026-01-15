namespace ViewAppxPackage;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.System;

/// <summary>
/// Dialog for creating a new setting container in the package
/// </summary>
public sealed partial class NewPackageSettingContainer : FormDialogBase
{
    public NewPackageSettingContainer(
        ApplicationDataContainer targetContainer,
        ApplicationDataContainer localContainer,
        ApplicationDataContainer roamingContainer)
        : base(targetContainer, localContainer, roamingContainer)
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Name of the new setting container
    /// </summary>
    public string ContainerName
    {
        get { return (string)GetValue(ContainerNameProperty); }
        set { SetValue(ContainerNameProperty, value); }
    }
    public static readonly DependencyProperty ContainerNameProperty =
        DependencyProperty.Register("ContainerName", typeof(string), typeof(NewPackageSettingContainer),
            new PropertyMetadata("", (d, dp) => (d as NewPackageSettingContainer).ContainerNameChanged()));

    void ContainerNameChanged()
    {
        // Validate the container name
        IsDuplicateName = false;
        if (TargetContainer.Containers.ContainsKey(ContainerName))
        {
            IsDuplicateName = true;
            IsValid = false;
        }

        // Keep IsValid up-to-date
        IsValid = !string.IsNullOrEmpty(ContainerName);
    }

    public bool IsDuplicateName
    {
        get { return (bool)GetValue(IsDuplicateNameProperty); }
        set { SetValue(IsDuplicateNameProperty, value); }
    }
    public static readonly DependencyProperty IsDuplicateNameProperty =
        DependencyProperty.Register("IsDuplicateName", typeof(bool), typeof(NewPackageSettingContainer), new PropertyMetadata(false));

}
