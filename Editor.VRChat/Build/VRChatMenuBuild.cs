using Aoyon.FaceTune.Build;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static class VRChatMenuBuilder
{
    private const string GeneratedMenuRootName = FaceTuneConstants.Name + " Generated Menu";

    public static void Emit(BuildContext context, MenuProgram program)
    {
        if (program.RootNodes.Count == 0 && program.ExternalInstalls.Count == 0) return;

        if (program.RootNodes.Count != 0)
        {
            var generatedRoot = new GameObject(GeneratedMenuRootName);
            generatedRoot.transform.SetParent(context.AvatarRootTransform, false);
            generatedRoot.AddComponent<ModularAvatarMenuInstaller>();
            var group = generatedRoot.AddComponent<ModularAvatarMenuGroup>();
            group.targetObject = generatedRoot;
            CreateChildren(context, program.RootNodes, generatedRoot.transform);
        }

        foreach (var install in program.ExternalInstalls)
        {
            if (!TryResolveExternalTarget(install.Target, out var parent))
            {
                LocalizedLog.Warning(
                    "Log:warning:GenerateMenuPass:InvalidInstallContainer",
                    install.Target.ToString());
                continue;
            }

            CreateChildren(context, install.Children, parent);
        }
    }

    private static bool TryResolveExternalTarget(GameObject target, [NotNullWhen(true)] out Transform? parent)
    {
        if (target.TryGetComponent<ModularAvatarMenuGroup>(out var group))
        {
            var targetObject = group.targetObject != null ? group.targetObject : group.gameObject;
            parent = targetObject.transform;
            return true;
        }

        if (target.TryGetComponent<ModularAvatarMenuItem>(out var menuItem)
            && menuItem.PortableControl.Type == PortableControlType.SubMenu
            && menuItem.MenuSource == SubmenuSource.Children)
        {
            var targetObject = menuItem.menuSource_otherObjectChildren != null
                ? menuItem.menuSource_otherObjectChildren
                : menuItem.gameObject;
            parent = targetObject.transform;
            return true;
        }

        parent = null;
        return false;
    }

    private static void CreateChildren(
        BuildContext context,
        IEnumerable<MenuNodePlan> nodes,
        Transform parent)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case MenuFolderPlan folder:
                    CreateFolder(context, folder, parent);
                    break;
                case MenuControlPlan control:
                    CreateControl(context, control, parent);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown menu node type: {node.GetType()}");
            }
        }
    }

    public static void Finalize(FaceTuneContext context)
    {
        var buildContext = context.BuildContext;
        var thumbnails = buildContext.GetState(_ => new VRChatMenuThumbnailState());
        if (!thumbnails.HasRequests
            || !buildContext.AvatarRootTransform.TryGetComponent<VRCAvatarDescriptor>(out var descriptor)
            || descriptor.expressionsMenu == null) return;

        var visited = new HashSet<VRCExpressionsMenu>();
        FinalizeMenu(context, descriptor.expressionsMenu, thumbnails, visited);
    }

    private static void FinalizeMenu(
        FaceTuneContext context,
        VRCExpressionsMenu menu,
        VRChatMenuThumbnailState thumbnails,
        ISet<VRCExpressionsMenu> visited)
    {
        if (!visited.Add(menu)) return;

        foreach (var control in menu.controls)
        {
            if (thumbnails.TryGet(control.name, out var request))
            {
                var thumbnail = Utils.RenderExpressionThumbnail(context, request.Expression);
                if (thumbnail != null) control.icon = thumbnail;
                control.name = request.DisplayName;
            }

            if (control.subMenu != null)
            {
                FinalizeMenu(context, control.subMenu, thumbnails, visited);
            }
        }
    }

    private static void CreateFolder(BuildContext context, MenuFolderPlan folder, Transform parent)
    {
        var obj = new GameObject(ResolveEmittedName(context, folder));
        obj.transform.SetParent(parent, false);

        var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
        menuItem.PortableControl.Type = PortableControlType.SubMenu;
        menuItem.PortableControl.Icon = ResolveIcon(folder.Icon);
        menuItem.MenuSource = SubmenuSource.Children;

        CreateChildren(context, folder.Children, obj.transform);
    }

    private static void CreateControl(BuildContext context, MenuControlPlan control, Transform parent)
    {
        var obj = new GameObject(ResolveEmittedName(context, control));
        obj.transform.SetParent(parent, false);

        var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
        menuItem.PortableControl.Type = control.Kind switch
        {
            MenuItemKind.Toggle => PortableControlType.Toggle,
            MenuItemKind.Radial => PortableControlType.RadialPuppet,
            _ => throw new InvalidOperationException($"Unknown menu item kind: {control.Kind}")
        };
        menuItem.PortableControl.Parameter = control.ParameterName;
        menuItem.PortableControl.Value = control.Value;
        menuItem.PortableControl.Icon = ResolveIcon(control.Icon);
    }

    private static string ResolveEmittedName(BuildContext context, MenuNodePlan node)
    {
        return node.Icon is MenuIconPlan.ExpressionPreview { Expression: { } expression }
            ? context.GetState(_ => new VRChatMenuThumbnailState()).Add(node.DisplayName, expression)
            : node.DisplayName;
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
}

internal sealed class VRChatMenuThumbnailState
{
    private const string MarkerPrefix = "[FaceTune:";
    private readonly Dictionary<string, Request> _requests = new();

    public bool HasRequests => _requests.Count != 0;

    public string Add(string displayName, ExpressionItem expression)
    {
        var marker = $"{MarkerPrefix}{Guid.NewGuid():N}]";
        _requests.Add(marker, new Request(displayName, expression));
        return marker;
    }

    public bool TryGet(string marker, [NotNullWhen(true)] out Request? request)
    {
        return _requests.TryGetValue(marker, out request);
    }

    internal sealed record Request(string DisplayName, ExpressionItem Expression);
}
