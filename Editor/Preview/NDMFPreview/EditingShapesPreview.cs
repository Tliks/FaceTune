using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune.Preview;

internal class EditingShapesPreview : AbstractFaceTunePreview<EditingShapesPreview>
{
    private static readonly PublishedValue<SkinnedMeshRenderer?> _target = new(null);
    public override bool IsEnabled(ComputeContext context) => context.Observe(_target, t => t != null, (a, b) => a == b);
    private static BlendShapeWeightSet _currentSet = new();

    public static void Start(SkinnedMeshRenderer? target, IReadOnlyBlendShapeSet? defaultSet = null)
    {
        // 既存のプレビューは上書き
        _target.Value = target;
        // OnSelectedなプレビューとは共存させる意味がないので停止させる
        SelectedShapesPreview.Disable();
        defaultSet?.CloneTo(_currentSet);
    }

    public static void Refresh(IReadOnlyBlendShapeSet set)
    {
        set.CloneTo(_currentSet);
        if (_target.Value == null) return;
        if (NDMFPreview.DisablePreviewDepth != 0) return;
        SetCurrentNodeDirectly(_currentSet, 0);
    }

    public static void Stop()
    {
        _target.Value = null;
        _currentSet.Clear();
        SelectedShapesPreview.MayEnable();
    }

    protected override void QueryBlendShapes(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, GameObject root, string bodyPath, ComputeContext context, BlendShapeWeightSet result, ref float defaultValue)
    {
        defaultValue = 0;
        result.AddRange(_currentSet);
    }
}