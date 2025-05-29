namespace com.aoyon.facetune;

internal static class SessionContextBuilder
{
    public static bool TryGet(GameObject target, [NotNullWhen(true)] out SessionContext? sessionContext, IOberveContext? context = null)
    {
        sessionContext = null;

        context ??= new NonObserveContext();

        var root = context.GetAvatarRoot(target);
        if (root == null) return false;

        var faceRenderer = GetFaceRenderer(root, context);
        if (faceRenderer == null) return false;

        var faceMesh = context.Observe(faceRenderer, r => r.sharedMesh, (a, b) => a == b);
        if (faceMesh == null) return false;

        var defaultExpression = GetDefaultExpression(root, faceRenderer, faceMesh);
        if (defaultExpression == null) return false;

        sessionContext = new SessionContext(root.gameObject, faceRenderer, faceMesh, defaultExpression);
        return true;
    }

    public static SkinnedMeshRenderer? GetFaceRenderer(GameObject root, IOberveContext? context = null)
    {
        context ??= new NonObserveContext();

        var overrideFaceRenderers = context.GetComponents<OverrideFaceRendererComponent>(root.gameObject);
        if (overrideFaceRenderers.Length > 1)
        {
            Debug.LogWarning($"Found {overrideFaceRenderers.Length} OverrideFaceRendererComponent on {root.name}. Only one is allowed.");
        }

        // LastOrNullなのはhierarchy上で一番下のものを取りたいから
        var faceObjects = overrideFaceRenderers.Select(c => context.Observe(c, c => c?.gameObject)).UnityOfType<GameObject>();
        var faceRenderer = faceObjects.Select(c => context.GetComponentNullable<SkinnedMeshRenderer>(c)).LastOrNull(r => r != null);
        if (faceRenderer == null)
        {
            return platform.PlatformSupport.GetFaceRenderer(root.transform);
        }
        else
        {
            return faceRenderer;
        }
    }

    public static FacialExpression GetDefaultExpression(GameObject root, SkinnedMeshRenderer faceRenderer, Mesh mesh, IOberveContext? context = null)
    {
        context ??= new NonObserveContext();

        var components = context.GetComponentsInChildren<DefaultFacialExpressionComponent>(root.gameObject, false);
        if (components.Length > 1)
        {
            Debug.LogWarning($"Found {components.Length} DefaultExpressionComponent on {root.name}. Only one is allowed.");
        }

        // Todo
        // FirstOrNullなのは最初のプリセットのものを取るワークアラウンド
        // Presetごとに異なる可能性があるのでどうにかする必要がある
        var defaultExpression = components.Select(c => c.GetDefaultExpression(context)).FirstOrNull(e => e != null);
        var sceneShapes = faceRenderer.GetBlendShapes(mesh);
        if (defaultExpression == null)
        {
            // defaultExpressionがない場合はシーン上の値をそのまま使う
            return new FacialExpression(new BlendShapeSet(sceneShapes), TrackingPermission.Allow, TrackingPermission.Allow, "Default");
        }
        else
        {
            // defaultExpressionが一部のShapeのみを指定している場合、シーン上の値に追加する
            var defaultShapes = sceneShapes.ToSet().Add(defaultExpression.BlendShapeSet);
            defaultExpression.ReplaceShapeSet(defaultShapes);
            return defaultExpression;
        }
    }
}