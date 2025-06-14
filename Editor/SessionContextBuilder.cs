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
        
        // context.Observe(faceRenderer, r => r.GetBlendShapes(faceMesh).ToSet(), (a, b) => a == b);
        var sceneShapes = faceRenderer.GetBlendShapes(faceMesh).ToSet();
        var dec = BuildDefaultExpressionContext(root, sceneShapes, context);

        sessionContext = new SessionContext(root.gameObject, faceRenderer, faceMesh, dec);
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
            return platform.PlatformSupport.GetFaceRenderer(root.transform);
        }
        else
        {
            return faceRenderer;
        }
    }

    public static DefaultExpressionContext BuildDefaultExpressionContext(GameObject root, BlendShapeSet sceneShapes, IObserveContext? context = null)
    {
        context ??= new NonObserveContext();

        var defaultExpressionComponents = context.GetComponentsInChildren<DefaultFacialExpressionComponent>(root.gameObject, true);
        var presetComponents = context.GetComponentsInChildren<PresetComponent>(root.gameObject, true);

        var presetExpressions = new Dictionary<PresetComponent, FacialExpression?>();
        var usedExpressionComponents = new HashSet<DefaultFacialExpressionComponent>();

        // presetExpression
        // PresetComponentの子にあるpresetは無視する
        var validPresetComponents = presetComponents
            .Where(pc => !pc.gameObject.GetComponentsInParent<PresetComponent>(true).Except(new[] { pc }).Any())
            .ToArray();
        foreach (var presetComponent in validPresetComponents)
        {
            var OverrideDefaultExpressionComponent = context.Observe(presetComponent, pc => pc.OverrideDefaultExpressionComponent, (a, b) => a == b);
            if (OverrideDefaultExpressionComponent != null)
            {
                var presetDefaultExpression = OverrideDefaultExpressionComponent.GetDefaultExpression(context);
                EnsureHasAllShapes(presetDefaultExpression, sceneShapes);
                presetExpressions.Add(presetComponent, presetDefaultExpression);
                usedExpressionComponents.Add(OverrideDefaultExpressionComponent);
            }
            else
            {
                presetExpressions.Add(presetComponent, null);
            }
        }

        // defaultExpression
        var defaultExpression = defaultExpressionComponents
            .Where(c => !usedExpressionComponents.Contains(c))
            .Select(c => c.GetDefaultExpression(context))
            .FirstOrNull();
        if (defaultExpression == null) 
        {
            defaultExpression = new FacialExpression(sceneShapes, TrackingPermission.Allow, TrackingPermission.Allow, "Default");
        }
        else
        {
            EnsureHasAllShapes(defaultExpression, sceneShapes);
        }

        return new DefaultExpressionContext(defaultExpression, presetExpressions);

        static void EnsureHasAllShapes(FacialExpression expression, BlendShapeSet fallback)
        {
            var defaultShapes = fallback.Duplicate().Add(expression.BlendShapeSet);
            expression.ReplaceShapeSet(defaultShapes);
        }
    }
}