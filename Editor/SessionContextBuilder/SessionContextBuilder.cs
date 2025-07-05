using aoyon.facetune.platform;
using nadena.dev.ndmf.runtime;

namespace aoyon.facetune;

internal static class SessionContextBuilder
{
    public static bool TryBuild(GameObject target, [NotNullWhen(true)] out SessionContext? sessionContext, IObserveContext? context = null)
    {
        sessionContext = null;

        context ??= new NonObserveContext();

        var root = context.GetAvatarRoot(target);
        if (root == null) return false;

        var platformSupport = platform.PlatformSupport.GetSupport(root.transform);

        var faceRenderer = GetFaceRenderer(root, platformSupport, context);
        if (faceRenderer == null) return false;

        var faceMesh = context.Observe(faceRenderer, r => r.sharedMesh, (a, b) => a == b);
        if (faceMesh == null) return false;

        var bodyPath = RuntimeUtil.RelativePath(root, faceRenderer.gameObject)!;

        var zeroWeightBlendShapes = new List<BlendShape>();
        faceRenderer.GetBlendShapesAndSetZeroWeight(zeroWeightBlendShapes);

        var trackedBlendShapes = platformSupport.GetTrackedBlendShape().ToHashSet();

        sessionContext = new SessionContext(root.gameObject, faceRenderer, faceMesh, bodyPath, zeroWeightBlendShapes, trackedBlendShapes);
        return true;
    }

    public static SkinnedMeshRenderer? GetFaceRenderer(GameObject root, IPlatformSupport? platformSupport = null, IObserveContext? context = null)
    {
        context ??= new NonObserveContext();
        platformSupport ??= platform.PlatformSupport.GetSupport(root.transform);

        using var _overrideFaceRenderers = ListPool<OverrideFaceRendererComponent>.Get(out var overrideFaceRenderers);
        context.GetComponents<OverrideFaceRendererComponent>(root.gameObject, overrideFaceRenderers);
        if (overrideFaceRenderers.Count > 1)
        {
            Debug.LogWarning($"Found {overrideFaceRenderers.Count} OverrideFaceRendererComponent on {root.name}. Only one is allowed.");
        }

        // LastOrNullなのはhierarchy上で一番下のものを取りたいから
        var faceObjects = overrideFaceRenderers.Select(c => context.Observe(c, c => c.FaceObject)).OfType<GameObject>();
        var faceRenderer = faceObjects.Select(c => context.GetComponentNullable<SkinnedMeshRenderer>(c)).LastOrNull(r => r != null);
        if (faceRenderer == null)
        {
            return platformSupport.GetFaceRenderer();
        }
        else
        {
            return faceRenderer;
        }
    }
}

