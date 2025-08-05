using System.Threading.Tasks;
using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune.Preview;

internal class EditingShapesPreview : AbstractFaceTunePreview<EditingShapesPreview>
{
    private static readonly PublishedValue<SkinnedMeshRenderer?> _target = new(null);
    public override bool IsEnabled(ComputeContext context) => context.Observe(_target, t => t != null, (a, b) => a == b);
    private static BlendShapePreviewNode? _previewNode = null;
    private static BlendShapeSet _currentSet = new();

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
        if (_previewNode == null) return;
        _previewNode.RefreshDirectly(_currentSet);
    }

    protected override async Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
    {
        var node = await base.Instantiate(group, proxyPairs, context);
        _previewNode = (BlendShapePreviewNode)node;
        _previewNode.RefreshDirectly(_currentSet);
        return node;
    }

    public static void Stop()
    {
        _target.Value = null;
        _previewNode = null;
        _currentSet.Clear();
        SelectedShapesPreview.MayEnable();
    }

    protected override void QueryBlendShapes(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, GameObject root, ComputeContext context, BlendShapeSet result)
    {
    }
}