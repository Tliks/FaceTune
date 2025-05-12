using nadena.dev.ndmf.preview;

namespace com.aoyon.facetune.preview;

// early
internal class DefaultShapesPreview : AbstractFaceTunePreview
{
    protected override BlendShapeSet? QueryBlendShapeSet(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, FaceTuneComponent mainComponent, ComputeContext context)
    {
        var components = context.GetComponentsInChildren<PreviewDefaultExpressionComponent>(mainComponent.gameObject, false);
        if (components.Length == 0) return null;

        var defaultExpressionComponent = context.Observe(mainComponent, c => c.DefaultExpressionComponent, (a, b) => a == b);
        if (defaultExpressionComponent == null) return null;

        var defaultBlendShapes = context.Observe(defaultExpressionComponent, c => c.BlendShapes, (a, b) => 
        {
            return false; // compareで何故か同じ値しか返ってこない
        });
        return new BlendShapeSet(defaultBlendShapes);
    }
}