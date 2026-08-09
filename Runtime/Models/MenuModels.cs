namespace Aoyon.FaceTune;

[Serializable]
internal class MenuIconSettings
{
    public enum Kind
    {
        None,
        Manual,
        ExpressionPreview
    }

    public Kind Mode = Kind.None;

    public Texture2D? ManualIcon = null; // For Kind.Manual
    public Transform? PreviewExpression = null; // For Kind.ExpressionPreview
}


/// <summary>Menu表示とinstall先。parameterや値はMenuComponent側で持つ。</summary>
[Serializable]
internal class MenuSettings
{
    [MenuName]
    public string MenuName = string.Empty;
    public MenuIconSettings Icon = new();
    [MenuInstallContainer]
    public Transform? InstallContainer = null;

}

/// <summary>親のSettingsの条件を受けない高優先度ExpressionをMenuから操作する。</summary>
[Serializable]
internal class DirectMenuSettings
{
    public MenuSettings Menu = new();
    public string GroupName = string.Empty;
}

internal static class BuiltInMenuGroups
{
    public const string DirectMenuReplace = "DirectMenuReplace";
}
