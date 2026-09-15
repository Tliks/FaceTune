using System.Threading.Tasks;
using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune.Preview;

internal sealed class DirectBlendShapePreviewLayer
{
    private readonly Func<ComputeContext, AvatarContext[]> _getTargets;
    private readonly Action<SkinnedMeshRenderer, BlendShapePreviewLayerState> _write;

    internal DirectBlendShapePreviewLayer(
        Func<ComputeContext, AvatarContext[]> getTargets,
        Action<SkinnedMeshRenderer, BlendShapePreviewLayerState> write)
    {
        _getTargets = getTargets;
        _write = write;
    }

    internal AvatarContext[] GetTargets(ComputeContext context)
    {
        return _getTargets(context);
    }

    internal void Set(
        SkinnedMeshRenderer renderer,
        BlendShapeApply apply,
        float opacity = 1f)
    {
        _write(renderer, new BlendShapePreviewLayerState(apply, opacity));
    }

    internal void Clear(SkinnedMeshRenderer renderer)
    {
        Set(renderer, BlendShapeApply.Empty);
    }
}

internal sealed class DirectBlendShapePreview : IRenderFilter
{
    internal static DirectBlendShapePreview Instance { get; } = new();

    private readonly Dictionary<SkinnedMeshRenderer, BlendShapePreviewNode> _currentNodes = new();
    private readonly Dictionary<SkinnedMeshRenderer, BlendShapePreviewLayerState[]> _directStates = new();
    private readonly PropCache<int, AvatarContext[]> _targets = new( $"{nameof(DirectBlendShapePreview)}:{nameof(_targets)}",
        CollectTargets, (a, b) => a.SequenceEqual(b));
    private readonly PublishedValue<int> _instantiatingTrigger = new(0, $"{nameof(DirectBlendShapePreview)}.{nameof(_instantiatingTrigger)}");
    private int _layerCount;

    public SelectedShapesPreview Selected { get; }
    public EditingShapesPreview Editing { get; }

    private DirectBlendShapePreview()
    {
        var expression = CreateLayer();
        var eyeBlink = CreateLayer();
        var lipSyncCanceller = CreateLayer();
        var lipSyncViseme = CreateLayer();
        var editing = CreateLayer();

        Selected = new SelectedShapesPreview(
            expression,
            eyeBlink,
            lipSyncCanceller,
            lipSyncViseme);
        Editing = new EditingShapesPreview(editing, Selected);
    }

    private DirectBlendShapePreviewLayer CreateLayer()
    {
        var layerIndex = _layerCount++;
        return new DirectBlendShapePreviewLayer(
            GetTargets,
            (renderer, apply) => Write(layerIndex, renderer, apply));
    }

    private void Write(
        int layerIndex,
        SkinnedMeshRenderer renderer,
        BlendShapePreviewLayerState layer)
    {
        var state = GetOrCreateState(renderer);
        if (state[layerIndex].Equals(layer)) return;
        state[layerIndex] = layer;

        if (TryGetNode(renderer, out var node))
        {
            node.SetDirectly(layerIndex, layer);
            SceneView.RepaintAll();
        }
        else
        {
            _instantiatingTrigger.Value++;
        }
    }

    private BlendShapePreviewLayerState[] GetOrCreateState(SkinnedMeshRenderer renderer)
    {
        if (_directStates.TryGetValue(renderer, out var state)) return state;

        state = new BlendShapePreviewLayerState[_layerCount];
        Array.Fill(state, new BlendShapePreviewLayerState(BlendShapeApply.Empty));
        _directStates.Add(renderer, state);
        return state;
    }

    private bool TryGetNode(SkinnedMeshRenderer renderer, [NotNullWhen(true)] out BlendShapePreviewNode? node)
    {
        node = null;
        if (!_currentNodes.TryGetValue(renderer, out node)) return false;
        if (node.Disposed)
        {
            _currentNodes.Remove(renderer);
            node = null;
            return false;
        }
        return true;
    }

    private AvatarContext[] GetTargets(ComputeContext context)
    {
        return _targets.Get(context, 0);
    }

    // FaceTuneのコンポーネントがあれば常に対象とする
    private static AvatarContext[] CollectTargets(ComputeContext context, int fixedValue)
    {
        using var _targets = ListPool<AvatarContext>.Get(out var targets);
        foreach (var root in context.GetAvatarRoots())
        {
            if (!AvatarContext.TryGet(root, out var avatar, out _, context)) continue;
            if (context.GetComponentsInChildren<FaceTuneTagComponent>(root, true).Length == 0) continue;
            targets.Add(avatar);
        }
        return targets.ToArray();
    }

    ImmutableList<RenderGroup> IRenderFilter.GetTargetGroups(ComputeContext context)
    {
        _currentNodes.Clear();
        var targets = GetTargets(context);

        foreach (var renderer in _directStates.Keys.ToList())
        {
            if (targets.All(target => target.FaceRenderer != renderer))
            {
                _directStates.Remove(renderer);
            }
        }

        return targets.Select(t => RenderGroup.For(t.FaceRenderer)).ToImmutableList();
    }

    Task<IRenderFilterNode> IRenderFilter.Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
    {
        var pair = proxyPairs.First();
        if (pair.Item1 is not SkinnedMeshRenderer original) throw new Exception("SkinnedMeshRenderer not found");
        if (pair.Item2 is not SkinnedMeshRenderer proxy) throw new Exception("SkinnedMeshRenderer not found");

        context.Observe(_instantiatingTrigger, _ => _instantiatingTrigger.Value, (a, b) => a == b);

        var applies = GetOrCreateState(original);
        var node = new BlendShapePreviewNode(proxy, applies);
        _currentNodes[original] = node;

        return Task.FromResult<IRenderFilterNode>(node);
    }
}
