using nadena.dev.modular_avatar.core;

namespace Aoyon.FaceTune;

internal enum MenuItemKind
{
    Toggle,
    Radial
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
    public bool IsEnabled => !string.IsNullOrWhiteSpace(GroupName);
    [NonSerialized] public int Value = 0;
}

[Serializable]
internal class MenuInstallSettings
{
    public AvatarObjectReference InstallContainerOverride = new();

    public void ResolveReferences(Component owner)
    {
        InstallContainerOverride.Get(owner);
    }
}

[Serializable]
internal class DirectMenuSettings
{
    public string MenuName = string.Empty;
    public MenuIconSettings Icon = new();
    public MenuInstallSettings InstallSettings = new();

    public string GroupName = string.Empty; // Auto(Blend) or Group
    public DirectMenuSuppressionMode SupressMode = DirectMenuSuppressionMode.Auto;

    public void ResolveReferences(Component owner)
    {
        InstallSettings.ResolveReferences(owner);
    }
}

internal enum DirectMenuSuppressionMode
{
    None,
    Auto,
    Replace,
    Group
}