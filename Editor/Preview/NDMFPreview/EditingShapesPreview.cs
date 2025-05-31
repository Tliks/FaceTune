using nadena.dev.ndmf.preview;

namespace com.aoyon.facetune.preview;

internal class EditingShapesPreview : AbstractFaceTunePreview
{
    private static readonly PublishedValue<SkinnedMeshRenderer?> _target = new(null);
    private static PublishedValue<BlendShapeSet>? _previewShapes = null;
    public override bool IsEnabled(ComputeContext context) => context.Observe(_target, t => t != null, (a, b) => a == b);

    public static void Start(SkinnedMeshRenderer target, PublishedValue<BlendShapeSet> previewShapes)
    {
        // 既存のプレビューは上書き
        _target.Value = target;
        _previewShapes = previewShapes;
        // OnSelectedなプレビューとは共存させる意味がないので停止させる
        SelectedShapesPreview.Disable();
    }

    public static void Stop()
    {
        _target.Value = null;
        _previewShapes = null;
        SelectedShapesPreview.MayEnable();
    }

    protected override BlendShapeSet? QueryBlendShapeSet(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, SessionContext sessionContext, ComputeContext context)
    {
        if (!IsEnabled(context)) return null;
        var target = context.Observe(_target, t => t, (a, b) => a == b);
        if (target != original) return null;
        return context.Observe(_previewShapes, s => s, (a, b) => false);
    }
}
