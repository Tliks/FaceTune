using System.Threading.Tasks;
using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune.Preview;

internal sealed class DirectBlendShapePreviewContext
{
    private readonly Func<ComputeContext, AvatarContext[]> _getTargets;
    private readonly Action<SkinnedMeshRenderer, BlendShapeApply> _write;

    internal DirectBlendShapePreviewContext(
        Func<ComputeContext, AvatarContext[]> getTargets,
        Action<SkinnedMeshRenderer, BlendShapeApply> write)
    {
        _getTargets = getTargets;
        _write = write;
    }

    internal AvatarContext[] GetTargets(ComputeContext context)
    {
        return _getTargets(context);
    }

    internal void Set(SkinnedMeshRenderer renderer, BlendShapeApply apply)
    {
        _write(renderer, apply);
    }

    internal void Clear(SkinnedMeshRenderer renderer)
    {
        Set(renderer, BlendShapeApply.Empty);
    }

    internal IDisposable ApplyAnimation(
        SkinnedMeshRenderer renderer,
        BlendShapeApply baseApply,
        IReadOnlyList<BlendShapeWeightAnimation> animations,
        bool isLooping)
    {
        IDisposable? multiFrame = null;
        if (animations.Any(animation => animation.IsMultiFrame))
        {
            multiFrame = new BlendShapeMultiFramePreview(
                baseApply,
                animations,
                isLooping,
                frame => Set(renderer, frame));
        }
        else
        {
            Set(renderer, baseApply with
            {
                Set = new ImmutableBlendShapeWeightSet(animations.ToFirstFrameBlendShapes())
            });
        }

        return new PreviewHandle(this, renderer, multiFrame);
    }

    private sealed class PreviewHandle : IDisposable
    {
        private readonly DirectBlendShapePreviewContext _preview;
        private readonly SkinnedMeshRenderer _renderer;
        private readonly IDisposable? _multiFrame;
        private bool _disposed;

        internal PreviewHandle(
            DirectBlendShapePreviewContext preview,
            SkinnedMeshRenderer renderer,
            IDisposable? multiFrame)
        {
            _preview = preview;
            _renderer = renderer;
            _multiFrame = multiFrame;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _multiFrame?.Dispose();
            _preview.Clear(_renderer);
        }
    }
}

internal sealed class DirectBlendShapePreview : IRenderFilter
{
    internal static DirectBlendShapePreview Instance { get; } = new();

    private const int SourceCount = 2;

    private readonly Dictionary<SkinnedMeshRenderer, BlendShapePreviewNode> _currentNodes = new();
    private readonly Dictionary<SkinnedMeshRenderer, BlendShapeApply[]> _directStates = new();
    private readonly PropCache<int, AvatarContext[]> _targets = new( $"{nameof(DirectBlendShapePreview)}:{nameof(_targets)}",
        CollectTargets, (a, b) => a.SequenceEqual(b));
    private readonly PublishedValue<int> _instantiatingTrigger = new(0, $"{nameof(DirectBlendShapePreview)}.{nameof(_instantiatingTrigger)}");

    public SelectedShapesPreview Selected { get; }
    public EditingShapesPreview Editing { get; }

    private DirectBlendShapePreview()
    {
        Selected = new SelectedShapesPreview(CreateContext(0));
        Editing = new EditingShapesPreview(CreateContext(1));
    }

    private DirectBlendShapePreviewContext CreateContext(int sourceIndex)
    {
        return new DirectBlendShapePreviewContext(
            GetTargets,
            (renderer, apply) => Write(sourceIndex, renderer, apply));
    }

    private void Write(int sourceIndex, SkinnedMeshRenderer renderer, BlendShapeApply apply)
    {
        var state = GetOrCreateState(renderer);
        state[sourceIndex] = apply;

        if (TryGetNode(renderer, out var node))
        {
            node.SetDirectly(sourceIndex, apply);
            SceneView.RepaintAll();
        }
        else
        {
            _instantiatingTrigger.Value++;
        }
    }

    private BlendShapeApply[] GetOrCreateState(SkinnedMeshRenderer renderer)
    {
        if (_directStates.TryGetValue(renderer, out var state)) return state;

        state = new BlendShapeApply[SourceCount];
        Array.Fill(state, BlendShapeApply.Empty);
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
