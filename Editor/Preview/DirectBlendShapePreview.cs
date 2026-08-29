using System.Threading.Tasks;
using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune.Preview;

internal abstract class DirectBlendShapePreview<TFilter> : IRenderFilter where TFilter : IRenderFilter
{
    private static readonly List<SkinnedMeshRenderer> _targetRenderers = new();

    private static readonly Dictionary<SkinnedMeshRenderer, BlendShapePreviewNode> _currentNodes = new();
    private static readonly Dictionary<SkinnedMeshRenderer, DirectPreviewState> _directStates = new();

    private static readonly PublishedValue<int> _instantiatingTrigger = new(0, $"{nameof(DirectBlendShapePreview<TFilter>)}.{nameof(_instantiatingTrigger)}");

    /// <summary>現在のNodeの内容を直接置き換える。</summary>
    protected internal static void SetCurrentNodeDirectly(BlendShapeApply apply)
    {
        var state = GetOrCreateState(apply.Renderer);
        apply.Set.CloneTo(state.Set);
        var applied = apply with { Set = state.Set };
        state.Apply = applied;

        if (TryGetNode(apply.Renderer, out var node))
        {
            node.SetDirectly(applied);
            RequestRepaint();
        }
        else
        {
            RequestInstantiate();
        }
    }

    protected internal static void ClearCurrentNodeDirectly(SkinnedMeshRenderer renderer)
    {
        _directStates.Remove(renderer);

        if (TryGetNode(renderer, out var node))
        {
            node.SetDirectly(new BlendShapeApply(renderer, new BlendShapeWeightSet()));
            RequestRepaint();
        }
        else
        {
            RequestInstantiate();
        }
    }

    private static DirectPreviewState GetOrCreateState(SkinnedMeshRenderer renderer)
        => _directStates.GetOrAdd(renderer, _ => new DirectPreviewState());

    private static bool TryGetNode(SkinnedMeshRenderer renderer, [NotNullWhen(true)] out BlendShapePreviewNode? node)
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

    ImmutableList<RenderGroup> IRenderFilter.GetTargetGroups(ComputeContext context)
    {
        _targetRenderers.Clear();
        _currentNodes.Clear();

        GetTargetRenderers(context, _targetRenderers);

        foreach (var renderer in _directStates.Keys.ToList())
        {
            if (!_targetRenderers.Contains(renderer))
            {
                _directStates.Remove(renderer);
            }
        }

        return _targetRenderers.Select(RenderGroup.For).ToImmutableList();
    }

    protected abstract void GetTargetRenderers(ComputeContext context, List<SkinnedMeshRenderer> targetRenderers);

    Task<IRenderFilterNode> IRenderFilter.Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
    {
        var pair = proxyPairs.First();
        if (pair.Item1 is not SkinnedMeshRenderer original) throw new Exception("SkinnedMeshRenderer not found");
        if (pair.Item2 is not SkinnedMeshRenderer proxy) throw new Exception("SkinnedMeshRenderer not found");

        context.Observe(_instantiatingTrigger, _ => _instantiatingTrigger.Value, (a, b) => a == b);

        var apply = new BlendShapeApply(original, new BlendShapeWeightSet());
        if (_directStates.TryGetValue(original, out var directState)
            && directState.Apply is { } storedApply)
            apply = storedApply;

        var node = new BlendShapePreviewNode(proxy, apply);
        _currentNodes[original] = node;

        return Task.FromResult<IRenderFilterNode>(node);
    }

    private static void RequestInstantiate()
    {
        _instantiatingTrigger.Value++;
    }

    private static void RequestRepaint()
    {
        SceneView.RepaintAll();
    }

    private sealed class DirectPreviewState
    {
        public BlendShapeApply? Apply { get; set; }
        public BlendShapeWeightSet Set { get; } = new();
    }
}
