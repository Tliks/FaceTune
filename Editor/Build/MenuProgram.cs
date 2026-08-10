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
    MenuComponent.Kind Kind,
    string ParameterName,
    float Value)
    : MenuNodePlan(DisplayName, Icon);

internal sealed record MenuInstallationPlan(
    Transform? Anchor,
    IReadOnlyList<MenuNodePlan> Nodes);

internal sealed class MenuProgram
{
    public IReadOnlyList<MenuInstallationPlan> Installations { get; }
    public IReadOnlyList<MenuParameterPlan> Parameters { get; }

    public MenuProgram(
        IReadOnlyList<MenuInstallationPlan> installations,
        IReadOnlyList<MenuParameterPlan> parameters)
    {
        Installations = installations;
        Parameters = parameters;
    }
}
