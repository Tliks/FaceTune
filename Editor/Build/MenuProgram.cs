namespace Aoyon.FaceTune.Build;

internal enum MenuParameterType
{
    Bool,
    Int,
    Float
}

internal sealed record MenuParameterPlan(
    string Name,
    MenuParameterType Type,
    float DefaultValue,
    bool Saved);

internal abstract record MenuIconPlan
{
    public sealed record Manual(Texture2D? Texture) : MenuIconPlan;
    public sealed record ExpressionPreview(ExpressionItem? Expression) : MenuIconPlan;
}

internal abstract record MenuNodePlan(
    string DisplayName,
    MenuIconPlan Icon);

internal sealed record MenuFolderPlan(
    string DisplayName,
    MenuIconPlan Icon,
    IReadOnlyList<MenuNodePlan> Children)
    : MenuNodePlan(DisplayName, Icon);

internal sealed record MenuControlPlan(
    string DisplayName,
    MenuIconPlan Icon,
    MenuItemKind Kind,
    string ParameterName,
    float Value)
    : MenuNodePlan(DisplayName, Icon);

internal sealed record ExternalMenuInstallRequest(
    GameObject Target,
    IReadOnlyList<MenuNodePlan> Children);

internal sealed class MenuProgram
{
    public IReadOnlyList<MenuNodePlan> RootNodes { get; }
    public IReadOnlyList<ExternalMenuInstallRequest> ExternalInstalls { get; }
    public IReadOnlyList<MenuParameterPlan> Parameters { get; }

    public MenuProgram(
        IReadOnlyList<MenuNodePlan> rootNodes,
        IReadOnlyList<ExternalMenuInstallRequest> externalInstalls,
        IReadOnlyList<MenuParameterPlan> parameters)
    {
        RootNodes = rootNodes;
        ExternalInstalls = externalInstalls;
        Parameters = parameters;
    }
}
