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
        var folderChildren = menus
            .Where(menu => menu.MenuKind == MenuComponent.Kind.Folder)
            .ToDictionary(menu => menu, _ => new List<MenuComponent>());
        var existingFolders = context.PlatformSupport.GetMenuFolderObjects()
            .SkipDestroyed()
            .Select(folder => folder.transform)
            .ToHashSet();
        var menuResolver = new FaceTuneMenuResolver(context.AvatarContext.Root, existingFolders);
        var installations = new List<MenuComponent>();
        var existingFolderChildren = new Dictionary<Transform, List<MenuComponent>>();

        foreach (var menu in menus)
        {
            var destination = GetDestinationCollection(
                menu,
                folderChildren,
                existingFolders,
                existingFolderChildren,
                installations,
                menuResolver);
            destination.Add(menu);
        }

        var builtExistingFolderChildren = new Dictionary<Transform, IReadOnlyList<MenuNodePlan>>();
        foreach (var (folder, children) in existingFolderChildren)
        {
            var nodes = BuildChildren(children, folderChildren, expressionByTransform);
            if (nodes.Count != 0)
                builtExistingFolderChildren.Add(folder, nodes);
        }

        return new MenuPlan(
            BuildChildren(installations, folderChildren, expressionByTransform),
            builtExistingFolderChildren);
    }

    private static IReadOnlyList<MenuNodePlan> BuildChildren(
        IReadOnlyList<MenuComponent> children,
        IReadOnlyDictionary<MenuComponent, List<MenuComponent>> folderChildren,
        IReadOnlyDictionary<Transform, ExpressionItem> expressionByTransform)
    {
        var nodes = new List<MenuNodePlan>();
        foreach (var menu in children)
        {
            MenuNodePlan? built = menu.MenuKind == MenuComponent.Kind.Folder
                ? BuildFolder(menu, folderChildren, expressionByTransform)
                : BuildControl(menu, expressionByTransform);
            if (built != null)
                nodes.Add(built);
        }
        return nodes;
    }

    private static MenuFolderPlan? BuildFolder(
        MenuComponent folder,
        IReadOnlyDictionary<MenuComponent, List<MenuComponent>> folderChildren,
        IReadOnlyDictionary<Transform, ExpressionItem> expressionByTransform)
    {
        var builtChildren = BuildChildren(
            folderChildren[folder],
            folderChildren,
            expressionByTransform);
        if (builtChildren.Count == 0)
            return null;

        return new MenuFolderPlan(
            GetHierarchyAnchor(folder),
            FaceTuneMenuResolver.GetDisplayName(folder.Menu.MenuName, folder.name),
            BuildIcon(folder.Menu.Icon, folder, expressionByTransform),
            builtChildren);
    }

    private static MenuControlPlan BuildControl(
        MenuComponent menu,
        IReadOnlyDictionary<Transform, ExpressionItem> expressionByTransform)
    {
        return new MenuControlPlan(
            GetHierarchyAnchor(menu),
            FaceTuneMenuResolver.GetDisplayName(menu.Menu.MenuName, menu.name),
            BuildIcon(menu.Menu.Icon, menu, expressionByTransform),
            menu.MenuKind,
            menu.ParameterName,
            menu.MenuKind == MenuComponent.Kind.Toggle ? menu.SelectedValue : 1f);
    }

    private static Transform GetHierarchyAnchor(MenuComponent menu)
        => menu.Menu.InstallContainer.DestroyedAsNull() ?? menu.transform;

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

        var target = FaceTuneMenuResolver.ResolvePreviewTarget(settings.PreviewExpression, owner);

        if (target == null)
        {
            return new MenuIconPlan.ExpressionPreview(null);
        }

        expressionByTransform.TryGetValue(target, out var expression);
        return new MenuIconPlan.ExpressionPreview(expression);
    }

    private static List<MenuComponent> GetDestinationCollection(
        MenuComponent menu,
        IReadOnlyDictionary<MenuComponent, List<MenuComponent>> folderChildren,
        ISet<Transform> existingFolders,
        IDictionary<Transform, List<MenuComponent>> existingFolderChildren,
        List<MenuComponent> installations,
        FaceTuneMenuResolver menuResolver)
    {
        var configuredTarget = menu.Menu.InstallContainer.DestroyedAsNull();
        if (configuredTarget != null)
            menuResolver.ValidateInstallTarget(configuredTarget, menu);

        var destination = menuResolver.ResolveDestination(menu, configuredTarget);
        var folder = destination != null ? destination.GetComponent<MenuComponent>() : null;
        if (folder != null && folderChildren.TryGetValue(folder, out var children))
            return children;
        if (destination != null
            && (destination != menuResolver.Root || existingFolders.Contains(destination)))
        {
            return existingFolderChildren.GetOrAdd(destination, _ => new List<MenuComponent>());
        }

        return installations;
    }
}
