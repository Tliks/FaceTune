namespace Aoyon.FaceTune.Build;

internal sealed class CreateMenuPlanPass : FaceTunePass<CreateMenuPlanPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.create-menu-plan";
    public override string DisplayName => "Create Menu Plan";

    protected override void Execute(FaceTuneContext context)
    {
        context.SetMenuPlan(MenuPlanBuilder.Build(context));
    }
}

internal static class MenuPlanBuilder
{
    public static MenuPlan Build(FaceTuneContext context)
    {
        var expressionByTransform = context.RequireExpressionPlan().Items
            .GroupBy(item => item.SourceTransform)
            .ToDictionary(group => group.Key, group => group.First());

        var menus = context.AvatarContext.Root
            .GetComponentsInChildren<MenuComponent>(true);
        var folders = menus
            .Where(menu => menu.MenuKind == MenuComponent.Kind.Folder)
            .ToDictionary(menu => menu, menu => new FolderNode(menu));
        var existingFolders = context.PlatformSupport.GetMenuFolderObjects()
            .SkipDestroyed()
            .Select(folder => folder.transform)
            .ToHashSet();
        var root = new NodeCollection();
        var existingFolderChildren = new Dictionary<Transform, NodeCollection>();

        foreach (var folder in folders.Values)
        {
            var destination = ResolveDestination(
                folder.Source,
                folders,
                existingFolders,
                existingFolderChildren,
                root);
            destination.Folders.Add(folder);
        }

        foreach (var menu in menus.Where(menu => menu.MenuKind != MenuComponent.Kind.Folder))
        {
            var destination = ResolveDestination(
                menu,
                folders,
                existingFolders,
                existingFolderChildren,
                root);
            destination.Controls.Add(menu);
        }

        var builtExistingFolderChildren = new Dictionary<Transform, IReadOnlyList<MenuNodePlan>>();
        foreach (var (folder, children) in existingFolderChildren)
        {
            var nodes = BuildChildren(children, expressionByTransform);
            if (nodes.Count != 0)
            {
                builtExistingFolderChildren.Add(folder, nodes);
            }
        }

        var rootNodes = BuildChildren(root, expressionByTransform);
        return new MenuPlan(rootNodes, builtExistingFolderChildren);
    }

    private static IReadOnlyList<MenuNodePlan> BuildChildren(
        NodeCollection children,
        IReadOnlyDictionary<Transform, ExpressionItem> expressionByTransform)
    {
        var nodes = new List<MenuNodePlan>();

        foreach (var folder in children.Folders)
        {
            var built = BuildFolder(folder, expressionByTransform);
            if (built != null)
            {
                nodes.Add(built);
            }
        }

        foreach (var control in children.Controls)
        {
            nodes.Add(BuildControl(control, expressionByTransform));
        }

        return nodes;
    }

    private static MenuFolderPlan? BuildFolder(
        FolderNode folder,
        IReadOnlyDictionary<Transform, ExpressionItem> expressionByTransform)
    {
        var children = BuildChildren(folder.Children, expressionByTransform);
        if (children.Count == 0)
        {
            return null;
        }

        return new MenuFolderPlan(
            GetDisplayName(folder.Source),
            BuildIcon(folder.Source.Menu.Icon, folder.Source, expressionByTransform),
            children);
    }

    private static MenuControlPlan BuildControl(
        MenuComponent menu,
        IReadOnlyDictionary<Transform, ExpressionItem> expressionByTransform)
    {
        return new MenuControlPlan(
            GetDisplayName(menu),
            BuildIcon(menu.Menu.Icon, menu, expressionByTransform),
            menu.MenuKind,
            menu.ParameterName,
            menu.MenuKind == MenuComponent.Kind.Toggle ? menu.SelectedValue : 1f);
    }

    private static MenuIconPlan BuildIcon(
        MenuIconSettings settings,
        Component owner,
        IReadOnlyDictionary<Transform, ExpressionItem> expressionByTransform)
    {
        if (settings.Mode == MenuIconSettings.Kind.None)
        {
            return new MenuIconPlan.Manual(null);
        }

        if (settings.Mode == MenuIconSettings.Kind.Manual)
        {
            return new MenuIconPlan.Manual(settings.ManualIcon.DestroyedAsNull());
        }

        var target = settings.PreviewExpression.DestroyedAsNull();
        var expressionOwner = (owner as ExpressionComponent).DestroyedAsNull();
        if (target == null && expressionOwner != null)
        {
            target = expressionOwner.transform.DestroyedAsNull();
        }

        if (target == null)
        {
            return new MenuIconPlan.ExpressionPreview(null);
        }

        expressionByTransform.TryGetValue(target, out var expression);
        return new MenuIconPlan.ExpressionPreview(expression);
    }

    private static NodeCollection ResolveDestination(
        MenuComponent menu,
        IReadOnlyDictionary<MenuComponent, FolderNode> folders,
        ISet<Transform> existingFolders,
        IDictionary<Transform, NodeCollection> existingFolderChildren,
        NodeCollection root)
    {
        for (var current = menu.transform.parent; current != null; current = current.parent)
        {
            var folder = current.GetComponent<MenuComponent>();
            if (folder != null && folders.TryGetValue(folder, out var parent))
            {
                return parent.Children;
            }

            if (existingFolders.Contains(current))
            {
                return existingFolderChildren.GetOrAdd(current, _ => new NodeCollection());
            }
        }

        return root;
    }

    private static string GetDisplayName(MenuComponent menu)
    {
        return string.IsNullOrWhiteSpace(menu.Menu.MenuName)
            ? menu.name
            : menu.Menu.MenuName;
    }

    private sealed class NodeCollection
    {
        public List<FolderNode> Folders { get; } = new();
        public List<MenuComponent> Controls { get; } = new();
    }

    private sealed record FolderNode(MenuComponent Source)
    {
        public NodeCollection Children { get; } = new();
    }
}
