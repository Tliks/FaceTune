using System.Threading.Tasks;
using nadena.dev.ndmf.preview;

namespace aoyon.facetune.preview;

internal class EditingShapesPreview : AbstractFaceTunePreview
{
    private static readonly PublishedValue<SkinnedMeshRenderer?> _target = new(null);
    public override bool IsEnabled(ComputeContext context) => context.Observe(_target, t => t != null, (a, b) => a == b);
    private static BlendShapePreviewNode? _previewNode = null;
    private static IReadOnlyBlendShapeSet? _defaultSet = null;

    public static void Start(SkinnedMeshRenderer target, IReadOnlyBlendShapeSet? defaultSet = null)
    {
        // 既存のプレビューは上書き
        _target.Value = target;
        // OnSelectedなプレビューとは共存させる意味がないので停止させる
        SelectedShapesPreview.Disable();
        _defaultSet = defaultSet;
    }

    public static void Refresh(IReadOnlyBlendShapeSet set)
    {
        if (_target.Value == null) return;
        if (NDMFPreview.DisablePreviewDepth != 0) return;
        if (_previewNode == null)
        {
            Debug.LogError("preview node not found. failed to refresh editing shapes preview");
            return;
        }
        _previewNode.RefreshDirectly(set);
    }

    protected override async Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
    {
        var node = await base.Instantiate(group, proxyPairs, context);
        if (node == null) throw new Exception("Failed to instantiate preview node");
        _previewNode = node as BlendShapePreviewNode;
        if (_previewNode == null) throw new Exception("Failed to cast preview node");
        if (_defaultSet != null) _previewNode.RefreshDirectly(_defaultSet);
        return node;
    }

    public static void Stop()
    {
        _target.Value = null;
        SelectedShapesPreview.MayEnable();
    }

    protected override void QueryBlendShapes(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, GameObject root, ComputeContext context, BlendShapeSet result)
    {
        if (!IsEnabled(context)) return;
    }
}