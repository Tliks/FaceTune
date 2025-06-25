using nadena.dev.ndmf.runtime;

namespace com.aoyon.facetune;

internal static class SessionContextBuilder
{
    public static bool TryBuild(GameObject target, [NotNullWhen(true)] out SessionContext? sessionContext, IObserveContext? context = null)
    {
        sessionContext = null;

        context ??= new NonObserveContext();

        var root = context.GetAvatarRoot(target);
        if (root == null) return false;

        var faceRenderer = GetFaceRenderer(root, context);
        if (faceRenderer == null) return false;

        var faceMesh = context.Observe(faceRenderer, r => r.sharedMesh, (a, b) => a == b);
        if (faceMesh == null) return false;

        var bodyPath = RuntimeUtil.RelativePath(root, faceRenderer.gameObject)!;

        sessionContext = new SessionContext(root.gameObject, faceRenderer, faceMesh, bodyPath);
        return true;
    }

    public static SkinnedMeshRenderer? GetFaceRenderer(GameObject root, IObserveContext? context = null)
    {
        context ??= new NonObserveContext();

        var overrideFaceRenderers = context.GetComponents<OverrideFaceRendererComponent>(root.gameObject);
        if (overrideFaceRenderers.Length > 1)
        {
            Debug.LogWarning($"Found {overrideFaceRenderers.Length} OverrideFaceRendererComponent on {root.name}. Only one is allowed.");
        }

        // LastOrNullなのはhierarchy上で一番下のものを取りたいから
        var faceObjects = overrideFaceRenderers.Select(c => context.Observe(c, c => c?.gameObject)).SkipDestroyed();
        var faceRenderer = faceObjects.Select(c => context.GetComponentNullable<SkinnedMeshRenderer>(c)).LastOrNull(r => r != null);
        if (faceRenderer == null)
        {
            var platformSupport = platform.PlatformSupport.GetSupport(root.transform);
            return platformSupport.GetFaceRenderer();
        }
        else
        {
            return faceRenderer;
        }
    }

    public static bool TryBuildWithDEC(GameObject target, [NotNullWhen(true)] out SessionContext? sessionContext, [NotNullWhen(true)] out DefaultExpressionContext? dec, IObserveContext? context = null)
    {
        dec = null;
        if (!TryBuild(target, out sessionContext, context)) return false;
        dec = DefaultExpressionContextBuilder.BuildDefaultExpressionContext(sessionContext, context);
        return dec != null;
    }
}

