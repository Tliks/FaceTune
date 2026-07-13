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

// Folder配置  
//  InstallContainerOverrideが有効なら、Hierarchy上の親よりoverride先を優先                                                                                    
//  override先がFaceTuneのFolderなら、そのFolderの子になる                                                                                                     
//  それ以外の対象ならexternal installになる                                                                                                                   
//  override未指定なら、Hierarchy上で最も近いFaceTune Folderの子になる                                                                                         
//  親Folderがなければroot menuになる

// 空Folder
//  Controlも有効な子Folderも持たないFolderは出力しない                                                                                                         
//  空Folderしか含まないFolderも連鎖的に出力しない
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
                    node.Parent = targetNode;
                }
                else
                {
                    GetExternalChildren(externalChildren, target).Folders.Add(node);
                }
                continue;
            }

            var parent = GetParentFolder(folder.transform.parent, root, folderNodes);
            if (parent != null)
            {
                parent.Children.Folders.Add(node);
                node.Parent = parent;
            }
            else
            {
                rootChildren.Folders.Add(node);
            }
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

        MenuIconPlan CompileIcon(MenuIconSettings settings, Component owner)
        {
            if (settings.Mode is MenuIconMode.Manual or MenuIconMode.None)
                return new MenuIconPlan.Manual(settings.Mode == MenuIconMode.None ? null : settings.ManualIcon);

            var transform = settings.PreviewExpression != null
                ? settings.PreviewExpression.transform
                : owner is FaceTuneComponent
                    ? owner.transform
                    : null;
            ExpressionItem? expression = null;
            if (transform != null) expressionByTransform.TryGetValue(transform, out expression);
            return new MenuIconPlan.ExpressionPreview(expression);
        }

        MenuControlPlan CompileControl(MutableControl control)
        {
            var source = control.Source;
            var value = source.Kind == MenuItemKind.Toggle && source.ExclusiveToggleGroup.IsEnabled
                ? source.ExclusiveToggleGroup.Value
                : 1f;
            return new MenuControlPlan(
                ResolveName(source.MenuName, source.name),
                CompileIcon(source.Icon, source),
                source.Kind,
                source.ParameterName,
                value);
        }

        IReadOnlyList<MenuNodePlan> CompileChildren(MutableChildren children)
        {
            return children.Folders
                .Select(folder => folder.Compiled)
                .Where(plan => plan != null)
                .Cast<MenuNodePlan>()
                .Concat(children.Controls.Select(control => (MenuNodePlan)CompileControl(control)))
                .ToArray();
        }

        var pending = new Queue<MutableFolder>();
        foreach (var folder in folderNodes.Values)
        {
            folder.RemainingChildren = folder.Children.Folders.Count;
            if (folder.RemainingChildren == 0) pending.Enqueue(folder);
        }

        var compiledFolderCount = 0;
        while (pending.Count != 0)
        {
            var folder = pending.Dequeue();
            var children = CompileChildren(folder.Children);
            if (children.Count != 0)
            {
                folder.Compiled = new MenuFolderPlan(
                    ResolveName(folder.Source.MenuName, folder.Source.name),
                    CompileIcon(folder.Source.Icon, folder.Source),
                    children);
            }

            compiledFolderCount++;
            if (folder.Parent == null) continue;

            folder.Parent.RemainingChildren--;
            if (folder.Parent.RemainingChildren == 0) pending.Enqueue(folder.Parent);
        }

        if (compiledFolderCount != folderNodes.Count)
        {
            throw new InvalidOperationException("Menu folder install overrides contain a cycle.");
        }

        var rootPlans = CompileChildren(rootChildren);
        var externalPlans = externalChildren
            .Select(pair => new ExternalMenuInstallRequest(
                pair.Key,
                CompileChildren(pair.Value)))
            .Where(install => install.Children.Count != 0)
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
            var defaultValue = menu.Kind switch
            {
                MenuItemKind.Radial => menu.FloatDefaultValue,
                MenuItemKind.Toggle when menu.ExclusiveToggleGroup.IsEnabled
                    => exclusiveDefaults.GetValueOrDefault(menu.ParameterName, 0f),
                MenuItemKind.Toggle when menu.DefaultSelected => 1f,
                _ => 0f
            };
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
        return GetParentFolder(start, root, folders)?.Children ?? rootChildren;
    }

    private static MutableFolder? GetParentFolder(
        Transform? start,
        GameObject root,
        IReadOnlyDictionary<MenuFolderComponent, MutableFolder> folders)
    {
        var current = start;
        while (current != null && current.gameObject != root)
        {
            var folder = current.GetComponent<MenuFolderComponent>();
            if (folder != null && folders.TryGetValue(folder, out var node)) return node;
            current = current.parent;
        }
        return null;
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
    }

    private sealed record MutableFolder(MenuFolderComponent Source)
    {
        public MutableChildren Children { get; } = new();
        public MutableFolder? Parent { get; set; }
        public MenuFolderPlan? Compiled { get; set; }
        public int RemainingChildren { get; set; }
    }

    private sealed record MutableControl(MenuComponent Source);
}
