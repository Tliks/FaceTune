using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune.Preview;

// early
internal class RealTimeExpressionPreview : AbstractFaceTunePreview<RealTimeExpressionPreview>
{
    protected override RenderGroup? GetTarget(ComputeContext context, GameObject root, SkinnedMeshRenderer faceRenderer)
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
            if (target != null) LocalizedLog.Warning("RealTimeExpressionPreview:Log:warning:MultipleExpressionComponentWithEnableRealTimePreview");
            target = component;
        }
        if (target == null) return null;

        return RenderGroup.For(faceRenderer).WithData((target, root));
    }

    protected override void QueryBlendShapes(RenderGroup group, SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, ComputeContext context, BlendShapeWeightSet result, ref float defaultValue)
    {
        var observeContext = new NDMFPreviewObserveContext(context);

        defaultValue = 0; // 0で初期化し他の影響を打ち消す

        var (target, root) = group.GetData<(ExpressionComponent, GameObject)>();
        var bodyPath = RuntimeUtil.RelativePath(root, original.gameObject)!;

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