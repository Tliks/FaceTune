using nadena.dev.ndmf.preview;

namespace aoyon.facetune.preview;

// early
internal class RealTimeExpressionPreview : AbstractFaceTunePreview<RealTimeExpressionPreview>
{
    protected override void QueryBlendShapes(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, GameObject root, ComputeContext context, BlendShapeSet result)
    {
        using var _ = ListPool<ExpressionComponent>.Get(out var components);
        context.GetComponentsInChildren<ExpressionComponent>(root, true, components);
        foreach (var component in components) context.Observe(component);
        var enabledComponents = components.Where(c => c.EnableRealTimePreview).ToList();

        ExpressionComponent target;
        if (enabledComponents.Count == 0)
        {
            return;
        }
        if (enabledComponents.Count == 1)
        {
            target = enabledComponents[0];
        }
        else
        {
            Debug.LogWarning("RealTimeExpressionPreview: Multiple ExpressionComponent with EnableRealTimePreview are found");
            target = enabledComponents.Last();
        }

        var observeContext = new NDMFPreviewObserveContext(context);

        using var _2 = BlendShapeSetPool.Get(out var zeroWeightBlendShapes);
        proxy.GetBlendShapesAndSetWeightToZero(zeroWeightBlendShapes);
        result.AddRange(zeroWeightBlendShapes);

        using var _3 = ListPool<BlendShapeAnimation>.Get(out var facialStyleAnimations);
        FacialStyleContext.TryGetFacialStyleAnimationsAndObserve(target.gameObject, facialStyleAnimations, root, observeContext);
        result.AddRange(facialStyleAnimations.ToFirstFrameBlendShapes());

        using var _4 = ListPool<AbstractDataComponent>.Get(out var dataComponents);
        context.GetComponentsInChildren<AbstractDataComponent>(target.gameObject, true, dataComponents);
        foreach (var dataComponent in dataComponents)
        {
            dataComponent.GetBlendShapes(result, facialStyleAnimations, observeContext);
        }
    }
}