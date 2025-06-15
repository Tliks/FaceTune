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
        
        // context.Observe(faceRenderer, r => r.GetBlendShapes(faceMesh).ToSet(), (a, b) => a == b);
        var sceneShapes = faceRenderer.GetBlendShapes(faceMesh).ToSet();
        var dec = BuildDefaultExpressionContext(root, bodyPath, sceneShapes, context);

        sessionContext = new SessionContext(root.gameObject, faceRenderer, faceMesh, bodyPath, dec);
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

    public static DefaultExpressionContext BuildDefaultExpressionContext(GameObject root, string bodyPath, BlendShapeSet sceneShapes, IObserveContext? context = null)
    {
        context ??= new NonObserveContext();

        var defaultExpressionComponents = context.GetComponentsInChildren<DefaultFacialExpressionComponent>(root.gameObject, true);
        var presetComponents = context.GetComponentsInChildren<PresetComponent>(root.gameObject, true);

        var presetExpressions = new Dictionary<PresetComponent, Expression?>();
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
                var presetDefaultExpression = OverrideDefaultExpressionComponent.GetDefaultExpression(bodyPath, context);
                EnsureHasAllShapes(presetDefaultExpression, sceneShapes, bodyPath);
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
            .Select(c => c.GetDefaultExpression(bodyPath, context))
            .FirstOrNull();
        if (defaultExpression == null) 
        {
            var defaultAnimations = sceneShapes.Select(shape => BlendShapeAnimation.SingleFrame(shape.Name, shape.Weight).GetGeneric(bodyPath)).ToList();
            defaultExpression = new Expression("Default", defaultAnimations, new FacialSettings());
        }
        else
        {
            EnsureHasAllShapes(defaultExpression, sceneShapes, bodyPath);
        }

        return new DefaultExpressionContext(defaultExpression, presetExpressions);

        static void EnsureHasAllShapes(Expression expression, BlendShapeSet fallback, string bodyPath)
        {
            var shapes = expression.AnimationIndex.GetAllFirstFrameBlendShapeSet();
            foreach (var fallbackShape in fallback)
            {
                if (!shapes.TryGetValue(fallbackShape.Name, out _))
                {
                    expression.AnimationIndex.AddSingleFrameBlendShapeAnimation(bodyPath, fallbackShape.Name, fallbackShape.Weight);
                }
            }
        }
    }
}