using nadena.dev.modular_avatar.core;
using Aoyon.FaceTune.Build;
using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static class VRChatMenuBuilder
{
    public static void Build(BuildContext context, MenuPlan plan)
    {
        CreateChildren(
            context,
            plan.RootNodes,
            context.AvatarRootTransform,
            installRoots: true);

        foreach (var (folder, children) in plan.ExistingFolderChildren)
        {
            CreateChildren(context, children, folder);
        }
    }

    private static void CreateChildren(
        BuildContext context,
        IEnumerable<MenuNodePlan> nodes,
        Transform parent,
        bool installRoots = false)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case MenuFolderPlan folder:
                    CreateFolder(context, folder, parent, installRoots);
                    break;
                case MenuControlPlan control:
                    CreateControl(context, control, parent, installRoots);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown menu node type: {node.GetType()}");
            }
        }
    }

    private static void CreateFolder(
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
    }

    private static void CreateControl(
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
