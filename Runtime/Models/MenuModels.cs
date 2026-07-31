
namespace Aoyon.FaceTune;

internal static class BuiltInMenuGroups
{
    public const string DirectMenuReplace = "DirectMenuReplace";
}

internal enum MenuItemKind
{
    Toggle,
    Radial
}

internal enum MenuIconMode
{
    Manual,
    ExpressionPreview,
    None
}

[Serializable]
internal class MenuIconSettings
{
    public MenuIconMode Mode = MenuIconMode.None;
    public Texture2D? ManualIcon = null;
    public AvatarObjectReference PreviewExpression = new();

    public void ResolveReferences(Component owner)
    {
        PreviewExpression.Get(owner);
    }
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

    // Blendのときメニューの排他 group として使う。空ののときは排他なし。
    // Replace のときは built-in replace group に入る。
    public string BlendExclusiveGroupName = string.Empty;

    public void ResolveReferences(Component owner)
    {
        Icon.ResolveReferences(owner);
        InstallSettings.ResolveReferences(owner);
    }
}