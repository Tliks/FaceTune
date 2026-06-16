using System.Threading.Tasks;
using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune.Preview;

internal abstract class DirectBlendShapePreview<TFilter> : IRenderFilter where TFilter : IRenderFilter
{
    private static readonly List<SkinnedMeshRenderer> _targetRenderers = new();

    private static readonly Dictionary<SkinnedMeshRenderer, BlendShapePreviewNode> _currentNodes = new();
    private static readonly Dictionary<SkinnedMeshRenderer, DirectPreviewState> _directStates = new();

    /// <summary>
    ///  現在のNodeの内容を直接置き換える。
    /// </summary>
    /// <param name="set">置き換えるBlendShapeSet</param>
    /// <param name="defaultValue">BlendShapeSetに含まれていない場合の扱い。-1の場合はプレビューしない</param>
    protected internal static void SetCurrentNodeDirectly(SkinnedMeshRenderer renderer, IReadOnlyBlendShapeSet set, float defaultValue = -1)
    {
        var state = GetOrCreateState(renderer);
        set.CloneTo(state.Set);
        state.DefaultValue = defaultValue;

        if (TryGetNode(renderer, out var node))
        {
            node.SetDirectly(state.Set, state.DefaultValue);
        }
    }

    protected internal static void ClearCurrentNodeDirectly(SkinnedMeshRenderer renderer)
    {
        _directStates.Remove(renderer);

        if (TryGetNode(renderer, out var node))
        {
            node.SetDirectly(DirectPreviewState.Empty.Set, DirectPreviewState.Empty.DefaultValue);
        }
    }

    private static DirectPreviewState GetOrCreateState(SkinnedMeshRenderer renderer)
    {
        if (_directStates.TryGetValue(renderer, out var state)) return state;

        state = new DirectPreviewState();
        _directStates[renderer] = state;
        return state;
    }

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

        var state = _directStates.TryGetValue(original, out var directState)
            ? directState
            : DirectPreviewState.Empty;

        var node = new BlendShapePreviewNode(proxy, state.Set, state.DefaultValue);
        _currentNodes[original] = node;

        return Task.FromResult<IRenderFilterNode>(node);
    }

    private sealed class DirectPreviewState
    {
        public static readonly DirectPreviewState Empty = new();

        public BlendShapeWeightSet Set { get; } = new();
        public float DefaultValue { get; set; } = -1;
    }
}
