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

        var defaultBlendShapes = context.Observe(defaultExpressionComponent, c => new List<BlendShape>(c.BlendShapes), (a, b) => a.SequenceEqual(b));
        return new BlendShapeSet(defaultBlendShapes);
    }
}