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

    protected override BlendShapeSet? QueryBlendShapeSet(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, SessionContext sessionContext, ComputeContext context)
    {
        if (!IsEnabled(context)) return null;
        return sessionContext.DEC.GetGlobalDefaultBlendShapeSet();
    }
}