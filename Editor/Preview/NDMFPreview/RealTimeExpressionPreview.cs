using nadena.dev.ndmf.preview;

namespace aoyon.facetune.preview;

// early
internal class RealTimeExpressionPreview : AbstractFaceTunePreview
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

        using var _2 = BlendShapeSetPool.Get(out var zeroWeightBlendShapes);
        proxy.GetBlendShapesAndSetZeroWeight(zeroWeightBlendShapes);
        result.AddRange(zeroWeightBlendShapes);

        using var _3 = BlendShapeSetPool.Get(out var facialStyleSet);
        FacialStyleContext.TryGetFacialStyleShapesAndObserve(target.gameObject, facialStyleSet, root, new NDMFPreviewObserveContext(context));
        foreach (var blendShape in facialStyleSet) result.Add(blendShape);

        using var _4 = ListPool<AbstractDataComponent>.Get(out var dataComponents);
        context.GetComponentsInChildren<AbstractDataComponent>(target.gameObject, true, dataComponents);
        var observeContext = new NDMFPreviewObserveContext(context);
        foreach (var dataComponent in dataComponents)
        {
            dataComponent.GetBlendShapes(result, facialStyleSet, observeContext);
        }
    }
}