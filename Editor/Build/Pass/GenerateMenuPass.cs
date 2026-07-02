using nadena.dev.modular_avatar.core;

namespace Aoyon.FaceTune.Build;

/// <summary>
/// Generates Modular Avatar menu components from FaceTune menu authoring components.
/// </summary>
internal class GenerateMenuPass : FaceTunePass<GenerateMenuPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.generate-menu";
    public override string DisplayName => "Generate Menu";

    protected override void Execute(FaceTuneContext context)
    {
        GenerateMenus(context.AvatarContext.Root);
    }

    private static void GenerateMenus(GameObject root)
    {
        var menuComponents = root.GetComponentsInChildren<MenuComponent>(true);
        if (menuComponents.Length == 0) return;

        var folderComponents = root.GetComponentsInChildren<MenuFolderComponent>(true);
        var folderNodes = folderComponents.ToDictionary(folder => folder, folder => new MenuFolderNode(folder));
        var rootChildren = new MenuChildren();
        var externalChildren = new Dictionary<Transform, MenuChildren>();

        foreach (var folder in folderComponents)
        {
            var node = folderNodes[folder];
            if (TryGetInstallContainerOverride(folder.InstallSettings, folder, out var target))
            {
                if (TryResolveInstallChildren(target, folder, folderNodes, externalChildren, out var children))
                {
                    children.Add(node);
                }
                continue;
            }

            GetParentChildren(folder.transform.parent, root, folderNodes, rootChildren).Add(node);
        }

        foreach (var menu in menuComponents)
        {
            if (TryGetInstallContainerOverride(menu.InstallSettings, menu, out var target))
            {
                if (TryResolveInstallChildren(target, menu, folderNodes, externalChildren, out var children))
                {
                    children.Add(menu);
                }
                continue;
            }

            GetParentChildren(menu.transform.parent, root, folderNodes, rootChildren).Add(menu);
        }

        var hasExternalMenus = externalChildren.Values.Any(children => children.HasMenuItems);
        if (!rootChildren.HasMenuItems && !hasExternalMenus) return;

        var generatedRoot = new GameObject("FaceTune Generated Menu");
        generatedRoot.transform.SetParent(root.transform, false);

        var parameters = generatedRoot.AddComponent<ModularAvatarParameters>();
        AddParameters(parameters, menuComponents);

        if (rootChildren.HasMenuItems)
        {
            generatedRoot.AddComponent<ModularAvatarMenuInstaller>();
            var group = generatedRoot.AddComponent<ModularAvatarMenuGroup>();
            group.targetObject = generatedRoot;

            CreateMenuChildren(rootChildren, generatedRoot.transform);
        }

        foreach (var (target, children) in externalChildren)
        {
            if (!children.HasMenuItems) continue;
            CreateMenuChildren(children, target);
        }
    }

    private static bool TryGetInstallContainerOverride(
        MenuInstallSettings settings,
        Component owner,
        [NotNullWhen(true)] out GameObject? target)
    {
        target = settings.InstallContainerOverride.Get(owner);
        return target != null;
    }

    private static bool TryResolveInstallChildren(
        GameObject target,
        Component owner,
        IReadOnlyDictionary<MenuFolderComponent, MenuFolderNode> folderNodes,
        Dictionary<Transform, MenuChildren> externalChildren,
        [NotNullWhen(true)] out MenuChildren? children)
    {
        if (target.TryGetComponent<MenuFolderComponent>(out var targetFolder))
        {
            if (folderNodes.TryGetValue(targetFolder, out var folderNode))
            {
                children = folderNode.Children;
                return true;
            }

            children = null;
            LocalizedLog.Warning("Log:warning:GenerateMenuPass:InvalidInstallContainer", owner.ToString());
            return false;
        }

        if (target.TryGetComponent<ModularAvatarMenuGroup>(out var group))
        {
            var targetObject = group.targetObject != null ? group.targetObject : group.gameObject;
            children = GetExternalChildren(externalChildren, targetObject.transform);
            return true;
        }

        if (target.TryGetComponent<ModularAvatarMenuItem>(out var menuItem) &&
            menuItem.PortableControl.Type == PortableControlType.SubMenu &&
            menuItem.MenuSource == SubmenuSource.Children)
        {
            var targetObject = menuItem.menuSource_otherObjectChildren != null
                ? menuItem.menuSource_otherObjectChildren
                : menuItem.gameObject;
            children = GetExternalChildren(externalChildren, targetObject.transform);
            return true;
        }

        children = null;
        LocalizedLog.Warning("Log:warning:GenerateMenuPass:InvalidInstallContainer", owner.ToString());
        return false;
    }

    private static MenuChildren GetParentChildren(
        Transform? start,
        GameObject root,
        IReadOnlyDictionary<MenuFolderComponent, MenuFolderNode> folderNodes,
        MenuChildren rootChildren)
    {
        var current = start;
        while (current != null && current.gameObject != root)
        {
            var folder = current.GetComponent<MenuFolderComponent>();
            if (folder != null && folderNodes.TryGetValue(folder, out var node)) return node.Children;
            current = current.parent;
        }

        return rootChildren;
    }

    private static MenuChildren GetExternalChildren(Dictionary<Transform, MenuChildren> installs, Transform target)
    {
        if (installs.TryGetValue(target, out var children)) return children;
        children = new MenuChildren();
        installs.Add(target, children);
        return children;
    }

    private static void AddParameters(ModularAvatarParameters parameters, IEnumerable<MenuComponent> menus)
    {
        var menuList = menus.ToList();
        var exclusiveDefaults = ResolveExclusiveDefaults(menuList);
        var configs = new Dictionary<string, ParameterConfig>();
        foreach (var menu in menuList)
        {
            var parameterName = menu.ParameterName;
            if (string.IsNullOrWhiteSpace(parameterName)) continue;

            var syncType = menu.Kind switch
            {
                MenuItemKind.Radial => ParameterSyncType.Float,
                MenuItemKind.Toggle when menu.ExclusiveToggleGroup.IsEnabled => ParameterSyncType.Int,
                MenuItemKind.Toggle => ParameterSyncType.Bool,
                _ => ParameterSyncType.NotSynced
            };

            if (configs.ContainsKey(parameterName)) continue;
            configs.Add(parameterName, new ParameterConfig
            {
                nameOrPrefix = parameterName,
                syncType = syncType,
                saved = true,
                defaultValue = exclusiveDefaults.GetValueOrDefault(parameterName, 0f),
                hasExplicitDefaultValue = true
            });
        }

        parameters.parameters.AddRange(configs.Values);
    }

    private static Dictionary<string, float> ResolveExclusiveDefaults(IEnumerable<MenuComponent> menus)
    {
        var result = new Dictionary<string, float>();
        foreach (var group in menus
            .Where(menu => menu.ExclusiveToggleGroup.IsEnabled && menu.ExclusiveToggleGroup.DefaultSelected)
            .GroupBy(menu => menu.ParameterName))
        {
            var defaults = group.ToArray();
            if (defaults.Length > 1)
            {
                LocalizedLog.Warning("Log:warning:GenerateMenuPass:MultipleDefaultSelectedMenu", null, defaults);
            }

            result[group.Key] = defaults[0].ExclusiveToggleGroup.Value;
        }

        return result;
    }

    private static void CreateMenuChildren(MenuChildren children, Transform parent)
    {
        foreach (var folder in children.Folders.Where(folder => folder.HasMenuItems))
        {
            CreateFolderMenuItem(folder, parent);
        }

        foreach (var item in children.Items)
        {
            CreateMenuItem(item, parent);
        }
    }

    private static void CreateFolderMenuItem(MenuFolderNode folder, Transform parent)
    {
        if (!folder.HasMenuItems) return;

        var obj = new GameObject(folder.Component.name);
        obj.transform.SetParent(parent, false);

        var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
        menuItem.PortableControl.Type = PortableControlType.SubMenu;
        menuItem.PortableControl.Icon = ResolveIcon(folder.Component.Icon);
        menuItem.MenuSource = SubmenuSource.Children;

        CreateMenuChildren(folder.Children, obj.transform);
    }

    private static void CreateMenuItem(MenuComponent source, Transform parent)
    {
        var obj = new GameObject(source.name);
        obj.transform.SetParent(parent, false);

        var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
        menuItem.PortableControl.Type = source.Kind switch
        {
            MenuItemKind.Toggle => PortableControlType.Toggle,
            MenuItemKind.Radial => PortableControlType.RadialPuppet,
            _ => throw new InvalidOperationException($"Unknown menu item kind: {source.Kind}")
        };
        menuItem.PortableControl.Parameter = source.ParameterName;
        menuItem.PortableControl.Value = source.ExclusiveToggleGroup.IsEnabled
            ? source.ExclusiveToggleGroup.Value
            : 1f;
        menuItem.PortableControl.Icon = ResolveIcon(source.Icon);
    }

    private static Texture2D? ResolveIcon(MenuIconSettings settings)
    {
        return settings.Mode switch
        {
            MenuIconMode.Manual => settings.ManualIcon,
            MenuIconMode.ExpressionPreview => null, // TODO: generate expression preview thumbnails.
            _ => null
        };
    }

    private sealed class MenuChildren
    {
        public List<MenuFolderNode> Folders { get; } = new();
        public List<MenuComponent> Items { get; } = new();
        public bool HasMenuItems => Items.Count != 0 || Folders.Any(folder => folder.HasMenuItems);

        public void Add(MenuFolderNode folder)
        {
            Folders.Add(folder);
        }

        public void Add(MenuComponent item)
        {
            Items.Add(item);
        }
    }

    private sealed class MenuFolderNode
    {
        public MenuFolderComponent Component { get; }
        public MenuChildren Children { get; } = new();
        public bool HasMenuItems => Children.HasMenuItems;

        public MenuFolderNode(MenuFolderComponent component)
        {
            Component = component;
        }
    }
}
