using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune.Preview;

// early
internal class RealTimeExpressionPreview : AbstractFaceTunePreview<RealTimeExpressionPreview>
{
    protected override void QueryBlendShapes(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, GameObject root, string bodyPath, ComputeContext context, BlendShapeSet result, ref float defaultValue)
    {
        using var _ = ListPool<ExpressionComponent>.Get(out var components);
        context.GetComponentsInChildren<ExpressionComponent>(root, true, components);

        ExpressionComponent? target = null;
        foreach (var component in components)
        {
            var enabled = context.Observe(component, c => c.EnableRealTimePreview, (a, b) => a == b);
            if (!enabled) continue;
            var isEditorOnly = context.EditorOnlyInHierarchy(component.gameObject);
            if (isEditorOnly) continue;
            if (target != null) Debug.LogWarning("RealTimeExpressionPreview: Multiple ExpressionComponent with EnableRealTimePreview are found");
            target = component;
        }
        if (target == null) return;

        var observeContext = new NDMFPreviewObserveContext(context);

        defaultValue = 0; // 0で初期化し他の影響を打ち消す

        using var _3 = ListPool<BlendShapeWeightAnimation>.Get(out var facialStyleAnimations);
        FacialStyleContext.TryGetFacialStyleAnimationsAndObserve(target.gameObject, facialStyleAnimations, root, observeContext);
        result.AddRange(facialStyleAnimations.ToFirstFrameBlendShapes());

        using var _4 = ListPool<ExpressionDataComponent>.Get(out var dataComponents);
        context.GetComponentsInChildren<ExpressionDataComponent>(target.gameObject, true, dataComponents);
        foreach (var dataComponent in dataComponents)
        {
            dataComponent.GetBlendShapes(result, facialStyleAnimations, bodyPath, observeContext);
        }
    }
}