using nadena.dev.ndmf.preview;

namespace aoyon.facetune.preview;

// early
internal class FacialStylePreview : AbstractFaceTunePreview
{
    protected override void QueryBlendShapes(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, GameObject root, ComputeContext context, BlendShapeSet result)
    {
        using var _ = ListPool<FacialStyleComponent>.Get(out var components);
        context.GetComponentsInChildren<FacialStyleComponent>(root, true, components);
        foreach (var component in components) context.Observe(component);
        var enabledComponents = components.Where(c => c.EnableRealTimePreview).ToList();

        FacialStyleComponent target;
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
            Debug.LogWarning("FacialStylePreview: Multiple FacialStyleComponent with EnableRealTimePreview are found");
            target = enabledComponents.Last();
        }

        using var _2 = BlendShapeSetPool.Get(out var zeroWeightBlendShapes);
        proxy.GetBlendShapesAndSetZeroWeight(zeroWeightBlendShapes);
        result.AddRange(zeroWeightBlendShapes);

        target.GetBlendShapes(result, new NDMFPreviewObserveContext(context));
    }
}