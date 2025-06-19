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
        var sceneShapes = new BlendShapeSet(faceRenderer.GetBlendShapes(faceMesh));
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
        
        using var _defaultExpressionComponents = ListPool<DefaultFacialExpressionComponent>.Get(out var defaultExpressionComponents);
        defaultExpressionComponents.AddRange(context.GetComponentsInChildren<DefaultFacialExpressionComponent>(root.gameObject, true)); // Todo: NDMF

        using var _presetComponents = ListPool<PresetComponent>.Get(out var presetComponents);
        presetComponents.AddRange(context.GetComponentsInChildren<PresetComponent>(root.gameObject, true)); // Todo: NDMF


        var presetExpressions = new Dictionary<PresetComponent, Expression?>();
        var usedExpressionComponents = new HashSet<DefaultFacialExpressionComponent>();

        // presetExpression
        // PresetComponentの子にあるpresetは無視する
        for (int i = presetComponents.Count - 1; i >= 0; i--)
        {
            var presetComponent = presetComponents[i];
            using var _parentPresetComponents = ListPool<PresetComponent>.Get(out var parentPresetComponents);
            presetComponent.gameObject.GetComponentsInParent<PresetComponent>(true, parentPresetComponents);
            if (parentPresetComponents.Any(pc => pc == presetComponent))
            {
                presetComponents.RemoveAt(i);
            }
        }
        var validPresetComponents = presetComponents;

        foreach (var presetComponent in presetComponents)
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
            var defaultAnimations = sceneShapes.Select(shape => BlendShapeAnimation.SingleFrame(shape.Name, shape.Weight).ToGeneric(bodyPath)).ToList();
            defaultExpression = new Expression("Default", defaultAnimations, new ExpressionSettings(), new FacialSettings());
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


    private static readonly BlendShapeSet EmptyBlendShapeSet = new();
    private static readonly IObserveContext NonObserveContext = new NonObserveContext();

    public static DefaultBlendShapesContext BuildDefaultBlendShapeSetContext(GameObject root, SkinnedMeshRenderer renderer, IObserveContext? context = null)
    {
        context ??= NonObserveContext;
        
        using var _defaultExpressionComponents = ListPool<DefaultFacialExpressionComponent>.Get(out var defaultExpressionComponents);
        defaultExpressionComponents.AddRange(context.GetComponentsInChildren<DefaultFacialExpressionComponent>(root.gameObject, true)); // Todo: NDMF

        using var _presetComponents = ListPool<PresetComponent>.Get(out var presetComponents);
        presetComponents.AddRange(context.GetComponentsInChildren<PresetComponent>(root.gameObject, true)); // Todo: NDMF

        var presetBlendShapeSets = DictionaryPool<PresetComponent, PooledObject<BlendShapeSet>?>.Get(out _); // ctr
        using var _usedExpressionComponents = HashSetPool<DefaultFacialExpressionComponent>.Get(out var usedExpressionComponents);

        // presetExpression
        // PresetComponentの子にあるpresetは無視する
        for (int i = presetComponents.Count - 1; i >= 0; i--)
        {
            var presetComponent = presetComponents[i];
            using var _parentPresetComponents = ListPool<PresetComponent>.Get(out var parentPresetComponents);
            presetComponent.gameObject.GetComponentsInParent<PresetComponent>(true, parentPresetComponents);
            if (parentPresetComponents.Any(pc => pc == presetComponent))
            {
                presetComponents.RemoveAt(i);
            }
        }

        foreach (var presetComponent in presetComponents)
        {
            var OverrideDefaultExpressionComponent = context.Observe(presetComponent, pc => pc.OverrideDefaultExpressionComponent, (a, b) => a == b);
            if (OverrideDefaultExpressionComponent != null)
            {
                var presetDefaultBlendShapeSet = BlendShapeSetPool.Get(out _); // ctr
                (OverrideDefaultExpressionComponent as IHasBlendShapes)!.GetBlendShapes(presetDefaultBlendShapeSet.Value, EmptyBlendShapeSet);
                presetBlendShapeSets.Value.Add(presetComponent, presetDefaultBlendShapeSet);
                usedExpressionComponents.Add(OverrideDefaultExpressionComponent);
            }
            else
            {
                presetBlendShapeSets.Value.Add(presetComponent, null);
            }
        }

        using var _sceneShapes = BlendShapeSetPool.Get(out var sceneShapes);
        renderer.GetBlendShapes(sceneShapes);

        var defaultBlendShapes = BlendShapeSetPool.Get(out _); // ctr

        foreach (var defaultExpressionComponent in defaultExpressionComponents)
        {
            if (!usedExpressionComponents.Contains(defaultExpressionComponent))
            {
                (defaultExpressionComponent as IHasBlendShapes)!.GetBlendShapes(defaultBlendShapes.Value, EmptyBlendShapeSet);

                // defaultBlendShapesに全ブレンドシェイプを持たせる 
                foreach (var fallbackShape in sceneShapes)
                {
                    if (!defaultBlendShapes.Value.Contains(fallbackShape.Name))
                    {
                        defaultBlendShapes.Value.Add(fallbackShape);
                    }
                }
                break;
            }
        }
        if (defaultBlendShapes.Value.Count == 0)
        {
            defaultBlendShapes.Value.AddRange(sceneShapes);
        }

        return new DefaultBlendShapesContext(defaultBlendShapes, presetBlendShapeSets);
    }
}