using Aoyon.FaceTune.Platforms;
using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune;

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

        var platformSupport = Platforms.MetabasePlatformSupport.GetSupport(root.transform);

        if (!TryGetFaceRenderer(root, out var faceRenderer, out var bodyPath, platformSupport, context))
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

        var zeroBlendShapes = new BlendShapeSet();
        faceRenderer.GetBlendShapesAndSetWeightToZero(zeroBlendShapes);

        var trackedBlendShapes = new HashSet<string>(platformSupport.GetTrackedBlendShape());

        var safeZeroBlendShapes = new BlendShapeSet(zeroBlendShapes.Where(shape => !trackedBlendShapes.Contains(shape.Name)));

        sessionContext = new SessionContext(root.gameObject, faceRenderer, faceMesh, bodyPath, zeroBlendShapes, trackedBlendShapes, safeZeroBlendShapes);
        result = SessionContextBuildResult.Success;
        return true;
    }

    public static bool TryGetFaceRenderer(GameObject root, [NotNullWhen(true)] out SkinnedMeshRenderer? faceRenderer, [NotNullWhen(true)] out string? bodyPath, IMetabasePlatformSupport? platformSupport = null, IObserveContext? context = null)
    {
        context ??= new NonObserveContext();
        platformSupport ??= MetabasePlatformSupport.GetSupport(root.transform);

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
            bodyPath = RuntimeUtil.RelativePath(root, faceRenderer.gameObject)!;
            return true;
        }
        bodyPath = null;
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

