using nadena.dev.ndmf.preview;

namespace aoyon.facetune.preview;

// early
internal class DefaultShapesPreview : AbstractFaceTunePreview
{
    public static TogglablePreviewNode ToggleNode = TogglablePreviewNode.Create(
        () => "DefaultShapesPreview",
        qualifiedName: "aoyon.facetune.default-shapes-preview",
        true
    );

    protected override TogglablePreviewNode? ControlNode => ToggleNode;

    protected override void QueryBlendShapes(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, GameObject root, ComputeContext context, BlendShapeSet result)
    {
        if (!IsEnabled(context)) return;

        using var _ = ListPool<FacialStyleComponent>.Get(out var components);
        context.GetComponentsInChildren<FacialStyleComponent>(root, true, components);
        if (components.Count == 0) return;
        
        using var _2 = BlendShapeSetPool.Get(out var zeroWeightBlendShapes);
        proxy.GetBlendShapesAndSetZeroWeight(zeroWeightBlendShapes);
        result.AddRange(zeroWeightBlendShapes);

        components[0].GetBlendShapes(result, new NDMFPreviewObserveContext(context));
    }
}