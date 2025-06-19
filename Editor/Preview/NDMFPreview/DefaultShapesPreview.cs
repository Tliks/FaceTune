using nadena.dev.ndmf.preview;

namespace com.aoyon.facetune.preview;

// early
internal class DefaultShapesPreview : AbstractFaceTunePreview
{
    public static TogglablePreviewNode ToggleNode = TogglablePreviewNode.Create(
        () => "DefaultShapesPreview",
        qualifiedName: "com.aoyon.facetune.default-shapes-preview",
        true
    );

    protected override TogglablePreviewNode? ControlNode => ToggleNode;

    protected override void QueryBlendShapes(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, GameObject root, ComputeContext context, BlendShapeSet result)
    {
        if (!IsEnabled(context)) return;

        using var defaultBlendShapeContext = SessionContextBuilder.BuildDefaultBlendShapeSetContext(root, original, new NDMFPreviewObserveContext(context));
        var blendShapes = defaultBlendShapeContext.GetGlobalDefaultBlendShapes();
        result.AddRange(blendShapes);
    }
}