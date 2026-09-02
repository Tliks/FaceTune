namespace Aoyon.FaceTune;

[Serializable]
internal class MenuIconSettings
{
    public enum Kind
    {
        None = 0,
        Manual = 10,
        ExpressionPreview = 20
    }

    public Kind Mode = Kind.None;

    public Texture2D? ManualIcon = null; // For Kind.Manual
    public Transform? PreviewExpression = null; // For Kind.ExpressionPreview
}


[Serializable]
internal class MenuSettings
{
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
    public int PriorityOffset = 10;

    [NonSerialized]
    internal MenuCondition? GeneratedCondition;
}

internal static class BuiltInMenuGroups
{
    public const string DirectMenuReplace = "DirectMenuReplace";
    public const string ExpressionSet = "ExpressionSet";
}
