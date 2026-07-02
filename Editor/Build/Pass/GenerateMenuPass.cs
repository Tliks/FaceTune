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
        var rootItems = new List<MenuComponent>();
        var rootFolders = new List<MenuFolderNode>();

        foreach (var folder in folderComponents)
        {
            var node = folderNodes[folder];
            var parentFolder = FindParentFolder(folder.transform.parent, root, folderNodes);
            if (parentFolder != null)
            {
                folderNodes[parentFolder].Folders.Add(node);
            }
            else
            {
                rootFolders.Add(node);
            }
        }

        foreach (var menu in menuComponents)
        {
            var parentFolder = FindParentFolder(menu.transform.parent, root, folderNodes);
            if (parentFolder != null)
            {
                folderNodes[parentFolder].Items.Add(menu);
            }
            else
            {
                rootItems.Add(menu);
            }
        }

        rootFolders.RemoveAll(folder => !folder.HasMenuItems);
        if (rootItems.Count == 0 && rootFolders.Count == 0) return;

        var generatedRoot = new GameObject("FaceTune Generated Menu");
        generatedRoot.transform.SetParent(root.transform, false);
        generatedRoot.AddComponent<ModularAvatarMenuInstaller>();

        var parameters = generatedRoot.AddComponent<ModularAvatarParameters>();
        AddParameters(parameters, menuComponents);

        foreach (var folder in rootFolders)
        {
            CreateFolderMenuItem(folder, generatedRoot.transform);
        }

        foreach (var item in rootItems)
        {
            CreateMenuItem(item, generatedRoot.transform);
        }
    }

    private static MenuFolderComponent? FindParentFolder(
        Transform? start,
        GameObject root,
        IReadOnlyDictionary<MenuFolderComponent, MenuFolderNode> folders)
    {
        var current = start;
        while (current != null && current.gameObject != root)
        {
            var folder = current.GetComponent<MenuFolderComponent>();
            if (folder != null && folders.ContainsKey(folder)) return folder;
            current = current.parent;
        }

        return null;
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

    private static void CreateFolderMenuItem(MenuFolderNode folder, Transform parent)
    {
        if (!folder.HasMenuItems) return;

        var obj = new GameObject(folder.Component.name);
        obj.transform.SetParent(parent, false);

        var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
        menuItem.PortableControl.Type = PortableControlType.SubMenu;
        menuItem.PortableControl.Icon = ResolveIcon(folder.Component.Icon);
        menuItem.MenuSource = SubmenuSource.Children;

        foreach (var childFolder in folder.Folders.Where(child => child.HasMenuItems))
        {
            CreateFolderMenuItem(childFolder, obj.transform);
        }

        foreach (var item in folder.Items)
        {
            CreateMenuItem(item, obj.transform);
        }
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

    private sealed class MenuFolderNode
    {
        public MenuFolderComponent Component { get; }
        public List<MenuFolderNode> Folders { get; } = new();
        public List<MenuComponent> Items { get; } = new();
        public bool HasMenuItems => Items.Count != 0 || Folders.Any(folder => folder.HasMenuItems);

        public MenuFolderNode(MenuFolderComponent component)
        {
            Component = component;
        }
    }
}
