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
        using var _settingsComponents = ListPool<AvatarSettingsComponent>.Get(out var settingsComponents);
        context.GetComponents<AvatarSettingsComponent>(root, settingsComponents);
        if (settingsComponents.Count > 1)
        {
            LocalizedLog.Warning("Log:warning:AvatarContext:MultipleSettingsComponent", null, settingsComponents);
        }
        if (settingsComponents.Count > 0)
        {
            var settingsComponent = settingsComponents[0];
            var faceObject = context.Observe(
                settingsComponent,
                c => c.FaceObjectReference.Get(c),
                (a, b) => a == b);
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
            .SkipDestroyed()
            .Distinct()
            .ToArray();
        if (candidates.Length == 1) return candidates[0];

        return new FallbackSupport(root).GetFaceRenderer();
    }

    public static ImmutableHashSet<string> GetUnavailableBlendShapeNames(
        GameObject root,
        FaceTuneWriteKind writeKind,
        ComputeContext? context = null)
    {
        var prohibited = MetabasePlatformSupport.GetForAvatar(root.transform)
            .SelectMany(support => support.GetProhibitedBlendShapeNames(writeKind));
        return prohibited
            .Concat(GetExplicitlyExcludedBlendShapeNames(root, context))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToImmutableHashSet(StringComparer.Ordinal);
    }

    public static ImmutableHashSet<string> GetExplicitlyExcludedBlendShapeNames(
        GameObject root,
        ComputeContext? context = null)
    {
        context ??= ComputeContext.NullContext;
        using var _ = ListPool<AvatarSettingsComponent>.Get(out var settings);
        context.GetComponentsInChildren<AvatarSettingsComponent>(root, true, settings);
        if (settings.FirstOrDefault().DestroyedAsNull() is not { } component)
            return ImmutableHashSet.Create<string>(StringComparer.Ordinal);
        return context.Observe(
                component,
                value => value.ExcludedBlendShapeNames.ToArray(),
                (left, right) => left.SequenceEqual(right))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToImmutableHashSet(StringComparer.Ordinal);
    }

    public enum BuildResult
    {
        Success,
        NotFoundAvatarRoot,
        NotFoundFaceRenderer,
        NotFoundFaceMesh,
    }
}
