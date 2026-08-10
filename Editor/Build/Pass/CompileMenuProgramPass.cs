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

/// <summary>Builds the menu tree from normalized MenuComponent values.</summary>
internal static class MenuProgramCompiler
{
    public static MenuProgram Compile(FaceTuneContext context)
    {
        var root = context.AvatarContext.Root;
        var expressionByTransform = context.RequireExpressionProgram().Items
            .GroupBy(item => item.SourceTransform)
            .ToDictionary(group => group.Key, group => group.First());
        var menus = root.GetComponentsInChildren<MenuComponent>(true);
        var folders = menus.Where(menu => menu.MenuKind == MenuComponent.Kind.Folder).ToArray();
        var folderNodes = folders.ToDictionary(folder => folder, folder => new MutableFolder(folder));
        var rootChildren = new MutableChildren();
        var installationChildren = new Dictionary<Transform, MutableChildren>();

        foreach (var folder in folders)
            AddNode(folder, folderNodes[folder], isFolder: true);
        foreach (var menu in menus.Where(menu => menu.MenuKind != MenuComponent.Kind.Folder))
            AddNode(menu, new MutableControl(menu), isFolder: false);

        MenuIconPlan CompileIcon(MenuIconSettings settings, Component owner)
        {
            if (settings.Mode is MenuIconSettings.Kind.Manual or MenuIconSettings.Kind.None)
                return new MenuIconPlan.Manual(settings.Mode == MenuIconSettings.Kind.None ? null : settings.ManualIcon);

            var target = settings.PreviewExpression ?? (owner as ExpressionComponent)?.transform;
            expressionByTransform.TryGetValue(target, out var expression);
            return new MenuIconPlan.ExpressionPreview(expression);
        }

        MenuControlPlan CompileControl(MutableControl control)
        {
            var menu = control.Source;
            return new MenuControlPlan(
                ResolveName(menu.Menu.MenuName, menu.name),
                CompileIcon(menu.Menu.Icon, menu),
                menu.MenuKind,
                menu.Name,
                menu.MenuKind == MenuComponent.Kind.Toggle ? menu.SelectedValue : 1f);
        }

        IReadOnlyList<MenuNodePlan> CompileChildren(MutableChildren children)
            => children.Folders
                .Select(folder => folder.Compiled)
                .Where(folder => folder != null)
                .Cast<MenuNodePlan>()
                .Concat(children.Controls.Select(control => (MenuNodePlan)CompileControl(control)))
                .ToArray();

        foreach (var folder in folderNodes.Values)
            folder.RemainingFolders = folder.Children.Folders.Count;
        var pending = new Queue<MutableFolder>(folderNodes.Values.Where(folder => folder.RemainingFolders == 0));
        var compiled = 0;
        while (pending.Count != 0)
        {
            var folder = pending.Dequeue();
            var children = CompileChildren(folder.Children);
            if (children.Count != 0)
                folder.Compiled = new MenuFolderPlan(
                    ResolveName(folder.Source.Menu.MenuName, folder.Source.name),
                    CompileIcon(folder.Source.Menu.Icon, folder.Source),
                    children);

            compiled++;
            if (folder.Parent == null) continue;
            if (--folder.Parent.RemainingFolders == 0) pending.Enqueue(folder.Parent);
        }

        if (compiled != folderNodes.Count)
            throw new InvalidOperationException("Menu folder installation contains a cycle.");

        var installations = installationChildren
            .Select(pair => new MenuInstallationPlan(pair.Key, CompileChildren(pair.Value)))
            .Append(new MenuInstallationPlan(null, CompileChildren(rootChildren)))
            .Where(plan => plan.Nodes.Count != 0)
            .ToArray();
        return new MenuProgram(installations, context.RequireMenuParameters());

        void AddNode(MenuComponent source, object node, bool isFolder)
        {
            var parent = GetParent(source, folderNodes);
            if (parent != null)
            {
                if (isFolder)
                {
                    var folder = (MutableFolder)node;
                    parent.Children.Folders.Add(folder);
                    folder.Parent = parent;
                }
                else
                {
                    parent.Children.Controls.Add((MutableControl)node);
                }
                return;
            }

            var children = GetInstallationChildren(source, root.transform, rootChildren, installationChildren);
            if (isFolder) children.Folders.Add((MutableFolder)node);
            else children.Controls.Add((MutableControl)node);
        }
    }

    private static MutableFolder? GetParent(
        MenuComponent menu,
        IReadOnlyDictionary<MenuComponent, MutableFolder> folders)
    {
        if (menu.Menu.InstallContainer != null)
        {
            var installedMenu = menu.Menu.InstallContainer.GetComponent<MenuComponent>();
            return installedMenu != null && folders.TryGetValue(installedMenu, out var installedFolder)
                ? installedFolder
                : null;
        }

        for (var current = menu.transform.parent; current != null; current = current.parent)
        {
            var folder = current.GetComponent<MenuComponent>();
            if (folder != null && folders.TryGetValue(folder, out var parent)) return parent;
        }
        return null;
    }

    private static MutableChildren GetInstallationChildren(
        MenuComponent menu,
        Transform root,
        MutableChildren rootChildren,
        IDictionary<Transform, MutableChildren> installations)
    {
        var target = menu.Menu.InstallContainer;
        if (target == null || target == root) return rootChildren;
        if (!installations.TryGetValue(target, out var children))
        {
            children = new MutableChildren();
            installations.Add(target, children);
        }
        return children;
    }

    private static string ResolveName(string configuredName, string objectName)
        => string.IsNullOrWhiteSpace(configuredName) ? objectName : configuredName;

    private sealed class MutableChildren
    {
        public List<MutableFolder> Folders { get; } = new();
        public List<MutableControl> Controls { get; } = new();
    }

    private sealed record MutableFolder(MenuComponent Source)
    {
        public MutableChildren Children { get; } = new();
        public MutableFolder? Parent { get; set; }
        public MenuFolderPlan? Compiled { get; set; }
        public int RemainingFolders { get; set; }
    }

    private sealed record MutableControl(MenuComponent Source);
}
