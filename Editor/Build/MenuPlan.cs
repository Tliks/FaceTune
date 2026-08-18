namespace Aoyon.FaceTune.Build;

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

internal sealed class MenuPlan
{
    public IReadOnlyList<MenuNodePlan> RootNodes { get; }
    public IReadOnlyDictionary<Transform, IReadOnlyList<MenuNodePlan>> ExistingFolderChildren { get; }

    public MenuPlan(
        IEnumerable<MenuNodePlan> rootNodes,
        IReadOnlyDictionary<Transform, IReadOnlyList<MenuNodePlan>> existingFolderChildren)
    {
        RootNodes = rootNodes.ToArray();
        ExistingFolderChildren = existingFolderChildren.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<MenuNodePlan>)pair.Value.ToArray());
    }
}
