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
        var components = context.GetComponentsInChildren<DefaultFacialExpressionComponent>(sessionContext.Root, false);
        if (components.Length == 0) return null;

        // Todo
        var defaultExpressionComponent = components.Last();

        var defaultBlendShapes = defaultExpressionComponent.GetDefaultExpression(new NDMFPreviewObserveContext(context));
        if (defaultBlendShapes == null) return null;
        
        return defaultBlendShapes.BlendShapeSet;
    }
}