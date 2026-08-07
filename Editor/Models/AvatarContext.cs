using Aoyon.FaceTune.Platforms;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune;

internal record AvatarContext(
    GameObject Root,
    SkinnedMeshRenderer FaceRenderer,
    Mesh FaceMesh,
    string BodyPath)
{
    public static bool TryGet(
        GameObject target,
        [NotNullWhen(true)] out AvatarContext? avatarContext,
        out BuildResult result,
        ComputeContext? context = null)
    {
        avatarContext = null;
        context ??= ComputeContext.NullContext;

        var root = context.GetAvatarRoot(target);
        if (root == null)
        {
            result = BuildResult.NotFoundAvatarRoot;
            return false;
        }

        var platformSupports = MetabasePlatformSupport.GetForAvatar(root.transform);
        return TryGet(target, platformSupports, out avatarContext, out result, context);
    }

    public static bool TryGet(
        GameObject target,
        IMetabasePlatformSupport platformSupport,
        [NotNullWhen(true)] out AvatarContext? avatarContext,
        out BuildResult result,
        ComputeContext? context = null)
    {
        return TryGet(target, new[] { platformSupport }, out avatarContext, out result, context);
    }

    public static bool TryGet(
        GameObject target,
        IReadOnlyList<IMetabasePlatformSupport> platformSupports,
        [NotNullWhen(true)] out AvatarContext? avatarContext,
        out BuildResult result,
        ComputeContext? context = null)
    {
        avatarContext = null;
        context ??= ComputeContext.NullContext;

        var root = context.GetAvatarRoot(target);
        if (root == null)
        {
            result = BuildResult.NotFoundAvatarRoot;
            return false;
        }

        SkinnedMeshRenderer? faceRenderer = null;
        using var _settingsComponents = ListPool<SettingsComponent>.Get(out var settingsComponents);
        context.GetComponents<SettingsComponent>(root, settingsComponents);
        if (settingsComponents.Count > 1)
        {
            LocalizedLog.Warning("Log:warning:AvatarContext:MultipleSettingsComponent", null, settingsComponents);
        }
        if (settingsComponents.Count > 0)
        {
            var settingsComponent = settingsComponents[0];
            var faceObject = settingsComponent.Settings.FaceMeshSelection == FaceMeshSelectionMode.Manual
                ? context.Observe(
                    settingsComponent,
                    c => c.Settings.FaceObjectReference.Get(c),
                    (a, b) => a == b).DestroyedAsNull()
                : null;
            faceRenderer = faceObject != null
                ? context.GetComponent<SkinnedMeshRenderer>(faceObject).DestroyedAsNull()
                : null;
        }

        faceRenderer ??= ResolveFaceRenderer(root.transform, platformSupports);
        if (faceRenderer == null)
        {
            result = BuildResult.NotFoundFaceRenderer;
            return false;
        }

        var faceMesh = context.Observe(faceRenderer, r => r.sharedMesh, (a, b) => a == b);
        if (faceMesh == null)
        {
            result = BuildResult.NotFoundFaceMesh;
            return false;
        }

        var bodyPath = RuntimeUtil.RelativePath(root, faceRenderer.gameObject)!;
        avatarContext = new AvatarContext(root.gameObject, faceRenderer, faceMesh, bodyPath);
        result = BuildResult.Success;
        return true;
    }

    private static SkinnedMeshRenderer? ResolveFaceRenderer(
        Transform root,
        IEnumerable<IMetabasePlatformSupport> platformSupports)
    {
        var candidates = platformSupports
            .Select(support => support.GetFaceRenderer())
            .Where(renderer => renderer != null)
            .Distinct()
            .ToArray();
        if (candidates.Length == 1) return candidates[0];

        return new FallbackSupport(root).GetFaceRenderer();
    }

    public enum BuildResult
    {
        Success,
        NotFoundAvatarRoot,
        NotFoundFaceRenderer,
        NotFoundFaceMesh,
    }
}
