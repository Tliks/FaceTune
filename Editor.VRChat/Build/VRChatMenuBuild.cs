using nadena.dev.modular_avatar.core;
using Aoyon.FaceTune.Build;
using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static class VRChatMenuBuilder
{
    public static void Build(BuildContext context, MenuPlan plan)
    {
        foreach (var installation in plan.Installations)
        {
            CreateNode(context, installation, installation.HierarchyAnchor, installRoot: true);
        }

        foreach (var (folder, children) in plan.ExistingFolderChildren)
        {
            CreateExistingFolderChildren(context, children, folder);
        }
    }

    private static void CreateExistingFolderChildren(
        BuildContext context,
        IReadOnlyList<MenuNodePlan> children,
        Transform parent)
    {
        var lastGeneratedByAnchor = new Dictionary<Transform, Transform>();
        foreach (var child in children)
        {
            var generated = CreateNode(context, child, parent, installRoot: false);
            var anchor = FindDirectChild(parent, child.HierarchyAnchor);
            if (anchor == null) continue;

            var predecessor = lastGeneratedByAnchor.GetValueOrDefault(anchor, anchor);
            generated.SetSiblingIndex(predecessor.GetSiblingIndex() + 1);
            lastGeneratedByAnchor[anchor] = generated;
        }
    }

    private static Transform? FindDirectChild(Transform parent, Transform hierarchyAnchor)
    {
        for (var current = hierarchyAnchor; current != null && current != parent; current = current.parent)
        {
            if (current.parent == parent)
                return current;
        }
        return null;
    }

    private static void CreateChildren(
        BuildContext context,
        IEnumerable<MenuNodePlan> nodes,
        Transform parent,
        bool installRoots = false)
    {
        foreach (var node in nodes)
            CreateNode(context, node, parent, installRoots);
    }

    private static Transform CreateNode(
        BuildContext context,
        MenuNodePlan node,
        Transform parent,
        bool installRoot)
    {
        return node switch
        {
            MenuFolderPlan folder => CreateFolder(context, folder, parent, installRoot),
            MenuControlPlan control => CreateControl(context, control, parent, installRoot),
            _ => throw new InvalidOperationException($"Unknown menu node type: {node.GetType()}")
        };
    }

    private static Transform CreateFolder(
        BuildContext context,
        MenuFolderPlan folder,
        Transform parent,
        bool installRoot)
    {
        var obj = new GameObject(ResolveEmittedName(context, folder));
        obj.transform.SetParent(parent, false);

        if (installRoot) obj.AddComponent<ModularAvatarMenuInstaller>();

        var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
        menuItem.PortableControl.Type = PortableControlType.SubMenu;
        menuItem.PortableControl.Icon = ResolveIcon(folder.Icon);
        menuItem.MenuSource = SubmenuSource.Children;

        CreateChildren(context, folder.Children, obj.transform);
        return obj.transform;
    }

    private static Transform CreateControl(
        BuildContext context,
        MenuControlPlan control,
        Transform parent,
        bool installRoot)
    {
        var obj = new GameObject(ResolveEmittedName(context, control));
        obj.transform.SetParent(parent, false);

        if (installRoot) obj.AddComponent<ModularAvatarMenuInstaller>();

        var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
        menuItem.PortableControl.Type = control.Kind switch
        {
            MenuComponent.Kind.Toggle => PortableControlType.Toggle,
            MenuComponent.Kind.Radial => PortableControlType.RadialPuppet,
            _ => throw new InvalidOperationException($"Unknown menu item kind: {control.Kind}")
        };
        menuItem.PortableControl.Parameter = control.ParameterName;
        menuItem.PortableControl.Value = control.Value;
        menuItem.PortableControl.Icon = ResolveIcon(control.Icon);
        return obj.transform;
    }

    private static string ResolveEmittedName(BuildContext context, MenuNodePlan node)
    {
        var expression = node.Icon is MenuIconPlan.ExpressionPreview preview
            ? preview.Expression
            : null;
        return VRChatMenuThumbnailFeature.ResolveEmittedName(context, node.DisplayName, expression);
    }

    private static Texture2D? ResolveIcon(MenuIconPlan icon)
    {
        return icon switch
        {
            MenuIconPlan.Manual manual => manual.Texture,
            MenuIconPlan.ExpressionPreview => null,
            _ => null
        };
    }

    public static void Finish(FaceTuneContext context)
    {
        VRChatMenuThumbnailFeature.Finish(context);
    }
}
