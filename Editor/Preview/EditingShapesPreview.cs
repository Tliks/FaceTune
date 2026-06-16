using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune.Preview;

internal class EditingShapesPreview : DirectBlendShapePreview<EditingShapesPreview>
{
    private static readonly PublishedValue<SkinnedMeshRenderer?> _target = new(null);

    public static void Start(SkinnedMeshRenderer? target)
    {
        // 既存のプレビューは上書き
        _target.Value = target;
        // OnSelectedなプレビューとは共存させる意味がないので停止させる
        SelectedShapesPreview.Disable();
    }

    public static void Refresh(IReadOnlyBlendShapeSet set, float defaultValue)
    {
        if (_target.Value == null) return;
        if (NDMFPreview.DisablePreviewDepth != 0) return;
        SetCurrentNodeDirectly(_target.Value, set, defaultValue);
    }

    public static void Stop()
    {
        if (_target.Value != null)
        {
            ClearCurrentNodeDirectly(_target.Value);
        }
        _target.Value = null;
        SelectedShapesPreview.MayEnable();
    }

    protected override void GetTargetRenderers(ComputeContext context, List<SkinnedMeshRenderer> targetRenderers)
    {
        var target = context.Observe(_target, t => t, (a, b) => a == b);
        if (target == null) return;

        targetRenderers.Add(target);
    }
}
