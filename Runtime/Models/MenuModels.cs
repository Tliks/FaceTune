using nadena.dev.modular_avatar.core;

namespace Aoyon.FaceTune;

internal enum MenuItemKind
{
    Toggle,
    Radial
}

internal enum MenuConditionMode
{
    Enabled,
    Disabled,
    GreaterThan,
    LessThan
}

internal enum MenuIconMode
{
    Manual,
    ExpressionPreview
}

[Serializable]
internal class MenuIconSettings
{
    public MenuIconMode Mode = MenuIconMode.ExpressionPreview;
    public Texture2D? ManualIcon = null;
    public FaceTuneComponent? PreviewExpression = null;
}

[Serializable]
internal class ExclusiveToggleGroup
{
    public string GroupName = string.Empty;
    public bool DefaultSelected = false;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(GroupName);
}

[Serializable]
internal class MenuInstallSettings
{
    public AvatarObjectReference InstallTargetOverride = new();

    public void ResolveReferences(Component owner)
    {
        InstallTargetOverride.Get(owner);
    }
}
