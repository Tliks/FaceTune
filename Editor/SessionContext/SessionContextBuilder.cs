using nadena.dev.ndmf.runtime;

namespace com.aoyon.facetune;

internal static class SessionContextBuilder
{
    public static bool TryGet(GameObject target, [NotNullWhen(true)] out SessionContext? context)
    {
        context = null;

        var root = RuntimeUtil.FindAvatarInParents(target.transform);
        if (root == null) return false;

        var faceRenderer = GetFaceRenderer(root);
        if (faceRenderer == null) return false;

        var faceMesh = faceRenderer.sharedMesh;
        if (faceMesh == null) return false;

        var defaultExpression = GetDefaultExpression(root, faceRenderer, faceMesh);
        if (defaultExpression == null) return false;

        context = new SessionContext(root.gameObject, faceRenderer, faceMesh, defaultExpression);
        return true;
    }

    public static SkinnedMeshRenderer? GetFaceRenderer(Transform root)
    {
        var overrideFaceRenderers = root.GetComponentsInChildren<OverrideFaceRendererComponent>(false);
        if (overrideFaceRenderers.Length > 1)
        {
            Debug.LogWarning($"Found {overrideFaceRenderers.Length} OverrideFaceRendererComponent on {root.name}. Only one is allowed.");
        }

        // LastOrNullなのはhierarchy上で一番下のものを取りたいから
        var faceRenderer = overrideFaceRenderers.Select(c => c.FaceObject.NullCast()?.GetComponentNullable<SkinnedMeshRenderer>()).LastOrNull(r => r != null);
        if (faceRenderer == null)
        {
            return platform.PlatformSupport.GetFaceRenderer(root.transform);
        }
        else
        {
            return faceRenderer;
        }
    }

    public static FacialExpression GetDefaultExpression(Transform root, SkinnedMeshRenderer faceRenderer, Mesh mesh)
    {
        var components = root.GetComponentsInChildren<DefaultFacialExpressionComponent>(false);
        if (components.Length > 1)
        {
            Debug.LogWarning($"Found {components.Length} DefaultExpressionComponent on {root.name}. Only one is allowed.");
        }

        // Todo
        // FirstOrNullなのは最初のプリセットのものを取るワークアラウンド
        // Presetごとに異なる可能性があるのでどうにかする必要がある
        var defaultExpression = components.Select(c => c.GetDefaultExpression()).FirstOrNull(e => e != null);
        if (defaultExpression == null)
        {
            return new FacialExpression(new BlendShapeSet(faceRenderer.GetBlendShapes(mesh)), TrackingPermission.Allow, TrackingPermission.Allow, "Default");
        }
        else
        {
            return defaultExpression;
        }
    }
}