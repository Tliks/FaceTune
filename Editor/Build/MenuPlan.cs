namespace Aoyon.FaceTune.Build;

internal abstract record MenuIconPlan
{
    public sealed record Manual(Texture2D? Texture) : MenuIconPlan;
    public sealed record ExpressionPreview(ExpressionItem? Expression) : MenuIconPlan;
}

internal abstract record MenuNodePlan(
    Transform HierarchyAnchor,
    string DisplayName,
    MenuIconPlan Icon);

internal sealed record MenuFolderPlan(
    Transform HierarchyAnchor,
    string DisplayName,
    MenuIconPlan Icon,
    IReadOnlyList<MenuNodePlan> Children)
    : MenuNodePlan(HierarchyAnchor, DisplayName, Icon);

internal sealed record MenuControlPlan(
    Transform HierarchyAnchor,
    string DisplayName,
    MenuIconPlan Icon,
    MenuComponent.Kind Kind,
    string ParameterName,
    float Value)
    : MenuNodePlan(HierarchyAnchor, DisplayName, Icon);

internal sealed class MenuPlan
{
    public IReadOnlyList<MenuNodePlan> Installations { get; }
    public IReadOnlyDictionary<Transform, IReadOnlyList<MenuNodePlan>> ExistingFolderChildren { get; }

    public MenuPlan(
        IEnumerable<MenuNodePlan> installations,
        IReadOnlyDictionary<Transform, IReadOnlyList<MenuNodePlan>> existingFolderChildren)
    {
        Installations = installations.ToArray();
        ExistingFolderChildren = existingFolderChildren.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<MenuNodePlan>)pair.Value.ToArray());
    }
}
