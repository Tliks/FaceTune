using Aoyon.FaceTune.Build;
using nadena.dev.ndmf;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static class VRChatMenuThumbnailFeature
{
    private const string MarkerPrefix = "[FaceTune:";

    public static string ResolveEmittedName(
        BuildContext context,
        string displayName,
        ExpressionItem? expression)
    {
        if (expression == null) return displayName;

        var state = context.GetState(_ => new State());
        var marker = $"{MarkerPrefix}{Guid.NewGuid():N}]";
        state.Requests.Add(marker, new Request(displayName, expression));
        return marker;
    }

    public static void Finish(FaceTuneContext context)
    {
        var buildContext = context.BuildContext;
        var state = buildContext.GetState(_ => new State());
        if (!state.HasRequests
            || !buildContext.AvatarRootTransform.TryGetComponent<VRCAvatarDescriptor>(out var descriptor)
            || descriptor.expressionsMenu == null) return;

        var controls = new List<(VRCExpressionsMenu.Control Control, Request Request)>();
        CollectControls(descriptor.expressionsMenu, state, new HashSet<VRCExpressionsMenu>(), controls);
        if (controls.Count == 0) return;

        try
        {
            var avatar = context.AvatarContext;
            var settings = context.RequireSettings();
            var managedZeroes = new BlendShapeWeightSet(settings.GetManagedZeroBlendShapes());
            var generatedTextures = new List<Texture2D>(controls.Count);
            var textureCache = new Dictionary<BlendShapeWeightSet, Texture2D>();
            var animator = descriptor.GetComponent<Animator>().DestroyedAsNull()
                ?? throw new InvalidOperationException("Avatar animator was not found.");
            using var capture = new BlendShapeThumbnailCapture(
                avatar.FaceRenderer,
                ThumbnailFraming.FromRenderer(avatar.FaceRenderer, avatar.Root.transform, animator));
            foreach (var (control, request) in controls)
            {
                using var _ = BlendShapeSetPool.Get(out var blendShapes);
                blendShapes.AddRange(managedZeroes);
                blendShapes.AddRange(
                    request.Expression.IncomingFacialAnimations.ToFirstFrameBlendShapes());
                blendShapes.AddRange(
                    request.Expression.LocalFacialAnimations.ToFirstFrameBlendShapes());

                var cacheKey = new BlendShapeWeightSet(blendShapes);
                control.icon = textureCache.GetOrAdd(cacheKey, _ =>
                {
                    var texture = capture.Capture(blendShapes);
                    texture.name = $"{FaceTuneConstants.Name} Thumbnail {generatedTextures.Count + 1}";
                    generatedTextures.Add(texture);
                    return texture;
                });
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
            var textureName = firstTexture.name;
            try
            {
                firstTexture.name = Guid.NewGuid().ToString("N");
                assetSaver.SaveAsset(firstTexture);
            }
            finally
            {
                firstTexture.name = textureName;
            }
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

    private static void CollectControls(
        VRCExpressionsMenu menu,
        State state,
        ISet<VRCExpressionsMenu> visited,
        ICollection<(VRCExpressionsMenu.Control Control, Request Request)> controls)
    {
        if (!visited.Add(menu)) return;

        foreach (var control in menu.controls)
        {
            if (state.Requests.TryGetValue(control.name, out var request)) controls.Add((control, request));
            if (control.subMenu != null) CollectControls(control.subMenu, state, visited, controls);
        }
    }

    private sealed class State
    {
        public Dictionary<string, Request> Requests { get; } = new();

        public bool HasRequests => Requests.Count != 0;
    }

    private sealed record Request(string DisplayName, ExpressionItem Expression);
}
