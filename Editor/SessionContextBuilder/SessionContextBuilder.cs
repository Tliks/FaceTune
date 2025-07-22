using aoyon.facetune.platform;
using nadena.dev.ndmf.runtime;

namespace aoyon.facetune;

internal static class SessionContextBuilder
{
    public static bool TryBuild(GameObject target, [NotNullWhen(true)] out SessionContext? sessionContext, out SessionContextBuildResult result, IObserveContext? context = null)
    {
        sessionContext = null;

        context ??= new NonObserveContext();

        var root = context.GetAvatarRoot(target);
        if (root == null)
        {
            result = SessionContextBuildResult.NotFoundAvatarRoot;
            return false;
        }

        var platformSupport = platform.PlatformSupport.GetSupport(root.transform);

        if (!TryGetFaceRenderer(root, out var faceRenderer, platformSupport, context))
        {
            result = SessionContextBuildResult.NotFoundFaceRenderer;
            return false;
        }

        var faceMesh = context.Observe(faceRenderer, r => r.sharedMesh, (a, b) => a == b);
        if (faceMesh == null)
        {
            result = SessionContextBuildResult.NotFoundFaceMesh;
            return false;
        }

        var bodyPath = RuntimeUtil.RelativePath(root, faceRenderer.gameObject)!;

        var zeroBlendShapes = new BlendShapeSet();
        faceRenderer.GetBlendShapesAndSetZeroWeight(zeroBlendShapes);

        var trackedBlendShapes = new HashSet<string>(platformSupport.GetTrackedBlendShape());

        var safeZeroBlendShapes = new BlendShapeSet(zeroBlendShapes.Where(shape => !trackedBlendShapes.Contains(shape.Name)));

        sessionContext = new SessionContext(root.gameObject, faceRenderer, faceMesh, bodyPath, zeroBlendShapes, trackedBlendShapes, safeZeroBlendShapes);
        result = SessionContextBuildResult.Success;
        return true;
    }

    public static bool TryGetFaceRenderer(GameObject root, [NotNullWhen(true)] out SkinnedMeshRenderer? faceRenderer, IPlatformSupport? platformSupport = null, IObserveContext? context = null)
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
        faceRenderer = faceObjects.Select(c => context.GetComponentNullable<SkinnedMeshRenderer>(c)).LastOrNull(r => r != null) ?? platformSupport.GetFaceRenderer();
        if (faceRenderer != null)
        {
            return true;
        }
        return false;
    }

    public enum SessionContextBuildResult
    {
        Success,
        NotFoundAvatarRoot,
        NotFoundFaceRenderer,
        NotFoundFaceMesh,
    }
}

