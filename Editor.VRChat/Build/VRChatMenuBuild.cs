using nadena.dev.modular_avatar.core;
using Aoyon.FaceTune.Build;
using nadena.dev.ndmf;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static class VRChatMenuBuilder
{
    public static void Emit(BuildContext context, MenuProgram program)
    {
        foreach (var installation in program.Installations)
        {
            CreateChildren(
                context,
                installation.Nodes,
                installation.Anchor ?? context.AvatarRootTransform,
                installation.Anchor == null);
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

    public static void Finalize(FaceTuneContext context)
    {
        var buildContext = context.BuildContext;
        var thumbnails = buildContext.GetState(_ => new VRChatMenuThumbnailState());
        if (!thumbnails.HasRequests
            || !buildContext.AvatarRootTransform.TryGetComponent<VRCAvatarDescriptor>(out var descriptor)
            || descriptor.expressionsMenu == null) return;

        var controls = new List<(VRCExpressionsMenu.Control Control, VRChatMenuThumbnailState.Request Request)>();
        CollectThumbnailControls(descriptor.expressionsMenu, thumbnails, new HashSet<VRCExpressionsMenu>(), controls);
        if (controls.Count == 0) return;

        try
        {
            var avatar = context.AvatarContext;
            var settings = context.RequireSettings();
            var managedZeroes = new BlendShapeWeightSet(avatar.FaceRenderer
                .GetBlendShapeWeights(avatar.FaceMesh)
                .Where(shape => !settings.ExcludedBlendShapeNames.Contains(shape.Name))
                .Select(shape => shape with { Weight = 0f }));
            var generatedTextures = new List<Texture2D>(controls.Count);
            var textureCache = new Dictionary<BlendShapeWeightSet, Texture2D>();
            var animator = descriptor.GetComponent<Animator>()
                ?? throw new InvalidOperationException("Avatar animator was not found.");
            using var capture = new BlendShapeThumbnailCapture(
                avatar.FaceRenderer,
                ThumbnailFraming.FromRenderer(avatar.FaceRenderer, avatar.Root.transform, animator));
            foreach (var (control, request) in controls)
            {
                using var _ = BlendShapeSetPool.Get(out var blendShapes);
                blendShapes.AddRange(managedZeroes);
                blendShapes.AddRange(request.Expression.FacialAnimationSet.ToFirstFrameBlendShapes());
                blendShapes.AddRange(request.Expression.AnimationSet.ToFirstFrameBlendShapes());

                // managedZeroes is common to every thumbnail, so only values that differ from
                // that common zero baseline are needed to identify the rendered expression.
                var cacheKey = new BlendShapeWeightSet(blendShapes.Where(shape => shape.Weight != 0f));
                textureCache.TryGetValue(cacheKey, out var texture);
                if (texture == null)
                {
                    texture = capture.Capture(blendShapes);
                    texture.name = $"{FaceTuneConstants.Name} Thumbnail {generatedTextures.Count + 1}";
                    textureCache.Add(cacheKey, texture);
                    generatedTextures.Add(texture);
                }
                control.icon = texture;
            }
            SaveThumbnails(buildContext.AssetSaver, generatedTextures);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            foreach (var (control, request) in controls)
            {
                control.name = request.DisplayName;
            }
        }
    }

    // NDMFはテクスチャを必ず別のCOntainerに分けるが、表情の数だけこれが走るとImportがかなり重い
    // ここで生成するテクスチャは十分小さいので、Containerを1つにまとめる
    // Todo: 設計として正しいか確認 / NDMF側のAPI追加(containerの追加)を検討
    private static void SaveThumbnails(IAssetSaver assetSaver, IReadOnlyList<Texture2D> textures)
    {
        using var _ = new Utils.ProfilingSampleScope("FaceTune.Thumbnail.Save");
        if (textures.Count == 0) return;
        if (assetSaver.CurrentContainer == null)
        {
            assetSaver.SaveAssets(textures);
            return;
        }

        try
        {
            AssetDatabase.StartAssetEditing();
            var firstTexture = textures[0];
            assetSaver.SaveAsset(firstTexture);
            var containerPath = AssetDatabase.GetAssetPath(firstTexture);
            for (var index = 1; index < textures.Count; index++)
            {
                AssetDatabase.AddObjectToAsset(textures[index], containerPath);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }

    private static void CollectThumbnailControls(
        VRCExpressionsMenu menu,
        VRChatMenuThumbnailState thumbnails,
        ISet<VRCExpressionsMenu> visited,
        ICollection<(VRCExpressionsMenu.Control Control, VRChatMenuThumbnailState.Request Request)> controls)
    {
        if (!visited.Add(menu)) return;

        foreach (var control in menu.controls)
        {
            if (thumbnails.TryGet(control.name, out var request)) controls.Add((control, request));
            if (control.subMenu != null) CollectThumbnailControls(control.subMenu, thumbnails, visited, controls);
        }
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
