namespace Aoyon.FaceTune.Build;

internal sealed class CompileMenuProgramPass : FaceTunePass<CompileMenuProgramPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.compile-menu-program";
    public override string DisplayName => "Compile Menu Program";

    protected override void Execute(FaceTuneContext context)
    {
        context.SetMenuProgram(MenuProgramCompiler.Compile(context));
    }
}

internal static class MenuProgramCompiler
{
    public static MenuProgram Compile(FaceTuneContext context)
    {
        var root = context.AvatarContext.Root;
        var expressionProgram = context.RequireExpressionProgram();
        var expressionByTransform = expressionProgram.Items
            .GroupBy(item => item.SourceTransform)
            .ToDictionary(group => group.Key, group => group.First());
        var folders = root.GetComponentsInChildren<MenuFolderComponent>(true);
        var folderNodes = folders.ToDictionary(
            folder => folder,
            folder => new MutableFolder(folder));
        var rootChildren = new MutableChildren();
        var externalChildren = new Dictionary<GameObject, MutableChildren>();

        foreach (var folder in folders)
        {
            var node = folderNodes[folder];
            var target = folder.InstallSettings.InstallContainerOverride.Get(folder);
            if (target != null)
            {
                if (target.TryGetComponent<MenuFolderComponent>(out var targetFolder)
                    && folderNodes.TryGetValue(targetFolder, out var targetNode))
                {
                    targetNode.Children.Folders.Add(node);
                }
                else
                {
                    GetExternalChildren(externalChildren, target).Folders.Add(node);
                }
                continue;
            }

            GetParentChildren(folder.transform.parent, root, folderNodes, rootChildren).Folders.Add(node);
        }

        foreach (var menu in root.GetComponentsInChildren<MenuComponent>(true))
        {
            var node = new MutableControl(menu);
            var target = menu.InstallSettings.InstallContainerOverride.Get(menu);
            if (target != null)
            {
                if (target.TryGetComponent<MenuFolderComponent>(out var targetFolder)
                    && folderNodes.TryGetValue(targetFolder, out var targetNode))
                {
                    targetNode.Children.Controls.Add(node);
                }
                else
                {
                    GetExternalChildren(externalChildren, target).Controls.Add(node);
                }
                continue;
            }

            GetParentChildren(menu.transform.parent, root, folderNodes, rootChildren).Controls.Add(node);
        }

        MenuIconPlan CompileIcon(MenuIconSettings settings)
        {
            if (settings.Mode == MenuIconMode.Manual) return new MenuIconPlan.Manual(settings.ManualIcon);

            ExpressionItem? expression = null;
            if (settings.PreviewExpression != null)
            {
                expressionByTransform.TryGetValue(settings.PreviewExpression.transform, out expression);
            }

            return new MenuIconPlan.ExpressionPreview(expression);
        }

        MenuFolderPlan CompileFolder(MutableFolder folder)
        {
            var children = CompileChildren(folder.Children);
            return new MenuFolderPlan(
                ResolveName(folder.Source.MenuName, folder.Source.name),
                CompileIcon(folder.Source.Icon),
                children);
        }

        MenuControlPlan CompileControl(MutableControl control)
        {
            var source = control.Source;
            var value = source.Kind == MenuItemKind.Toggle && source.ExclusiveToggleGroup.IsEnabled
                ? source.ExclusiveToggleGroup.Value
                : 1f;
            return new MenuControlPlan(
                ResolveName(source.MenuName, source.name),
                CompileIcon(source.Icon),
                source.Kind,
                source.ParameterName,
                value);
        }

        IReadOnlyList<MenuNodePlan> CompileChildren(MutableChildren children)
        {
            return children.Folders
                .Where(folder => folder.Children.HasItems)
                .Select(folder => (MenuNodePlan)CompileFolder(folder))
                .Concat(children.Controls.Select(control => (MenuNodePlan)CompileControl(control)))
                .ToArray();
        }

        var rootPlans = CompileChildren(rootChildren);
        var externalPlans = externalChildren
            .Where(pair => pair.Value.HasItems)
            .Select(pair => new ExternalMenuInstallRequest(
                pair.Key,
                CompileChildren(pair.Value)))
            .ToArray();

        return new MenuProgram(
            rootPlans,
            externalPlans,
            CompileParameters(root.GetComponentsInChildren<MenuComponent>(true)));
    }

    private static IReadOnlyList<MenuParameterPlan> CompileParameters(IEnumerable<MenuComponent> menus)
    {
        var menuArray = menus.ToArray();
        var exclusiveDefaults = new Dictionary<string, float>();
        foreach (var group in menuArray
                     .Where(menu => menu.Kind == MenuItemKind.Toggle
                                    && menu.ExclusiveToggleGroup.IsEnabled
                                    && menu.DefaultSelected)
                     .GroupBy(menu => menu.ParameterName))
        {
            var defaults = group.ToArray();
            if (defaults.Length > 1)
            {
                LocalizedLog.Warning(
                    "Log:warning:GenerateMenuPass:MultipleDefaultSelectedMenu",
                    null,
                    defaults);
            }

            exclusiveDefaults[group.Key] = defaults[0].ExclusiveToggleGroup.Value;
        }

        var result = new Dictionary<string, MenuParameterPlan>();
        foreach (var menu in menuArray)
        {
            if (string.IsNullOrWhiteSpace(menu.ParameterName) || result.ContainsKey(menu.ParameterName)) continue;

            var type = menu.Kind switch
            {
                MenuItemKind.Radial => MenuParameterType.Float,
                MenuItemKind.Toggle when menu.ExclusiveToggleGroup.IsEnabled => MenuParameterType.Int,
                MenuItemKind.Toggle => MenuParameterType.Bool,
                _ => throw new InvalidOperationException($"Unknown menu item kind: {menu.Kind}")
            };
            var defaultValue = menu.Kind == MenuItemKind.Toggle && menu.ExclusiveToggleGroup.IsEnabled
                ? exclusiveDefaults.GetValueOrDefault(menu.ParameterName, 0f)
                : menu.Kind == MenuItemKind.Toggle && menu.DefaultSelected ? 1f : 0f;
            result.Add(menu.ParameterName, new MenuParameterPlan(menu.ParameterName, type, defaultValue, true));
        }

        return result.Values.ToArray();
    }

    private static string ResolveName(string configuredName, string objectName)
    {
        return string.IsNullOrWhiteSpace(configuredName) ? objectName : configuredName;
    }

    private static MutableChildren GetParentChildren(
        Transform? start,
        GameObject root,
        IReadOnlyDictionary<MenuFolderComponent, MutableFolder> folders,
        MutableChildren rootChildren)
    {
        var current = start;
        while (current != null && current.gameObject != root)
        {
            var folder = current.GetComponent<MenuFolderComponent>();
            if (folder != null && folders.TryGetValue(folder, out var node)) return node.Children;
            current = current.parent;
        }
        return rootChildren;
    }

    private static MutableChildren GetExternalChildren(
        IDictionary<GameObject, MutableChildren> installs,
        GameObject target)
    {
        if (installs.TryGetValue(target, out var children)) return children;
        children = new MutableChildren();
        installs.Add(target, children);
        return children;
    }

    private sealed class MutableChildren
    {
        public List<MutableFolder> Folders { get; } = new();
        public List<MutableControl> Controls { get; } = new();
        public bool HasItems => Controls.Count != 0 || Folders.Any(folder => folder.Children.HasItems);
    }

    private sealed record MutableFolder(MenuFolderComponent Source)
    {
        public MutableChildren Children { get; } = new();
    }

    private sealed record MutableControl(MenuComponent Source);
}
