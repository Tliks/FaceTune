using System.Threading.Tasks;
using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune.Preview;

internal class BlendShapePreviewNode : IRenderFilterNode
{
    public RenderAspects WhatChanged => RenderAspects.Shapes;

    private readonly int _blendShapeCount;
    private readonly PooledObject<List<string>> _blendShapeNames;
    private readonly PooledObject<List<float>> _blendShapeWeights;
    private readonly PooledObject<List<bool>> _shouldApply;

    public bool Disposed { get; private set; }

    public BlendShapePreviewNode(SkinnedMeshRenderer smr, BlendShapeApply apply)
    {
        var mesh = smr.sharedMesh.DestroyedAsNull()
            ?? throw new ArgumentException("Renderer has no mesh.", nameof(smr));
        _blendShapeCount = mesh.blendShapeCount;
        _blendShapeNames = ListPool<string>.Get(out var names);
        for (int i = 0; i < _blendShapeCount; i++)
        {
            names.Add(mesh.GetBlendShapeName(i));
        }
        _blendShapeWeights = ListPool<float>.Get(out _);
        _shouldApply = ListPool<bool>.Get(out _);
        SetInternal(apply);
    }

    private void SetInternal(BlendShapeApply apply)
    {
        if (Disposed) return;
        var names = _blendShapeNames.Value;
        var current = _blendShapeWeights.Value;
        var shouldApply = _shouldApply.Value;
        current.Clear();
        shouldApply.Clear();
        for (int i = 0; i < _blendShapeCount; i++)
        {
            if (apply.TryGetWeight(names[i], out var weight))
            {
                current.Add(weight);
                shouldApply.Add(true);
            }
            else
            {
                current.Add(default);
                shouldApply.Add(false);
            }
        }
    }

    // Nodeを再生成せず、高頻度な編集内容を次回のOnFrameへ反映する。
    public void SetDirectly(BlendShapeApply apply)
    {
        SetInternal(apply);
    }

    public void OnFrame(Renderer original, Renderer proxy)
    {
        var smr = proxy as SkinnedMeshRenderer;
        if (smr == null) return;
        OnFrameInternal(smr);
    }

    private void OnFrameInternal(SkinnedMeshRenderer proxy)
    {
        if (Disposed) return;
        if (!proxy.enabled) return;

        var weights = _blendShapeWeights.Value;
        var count = weights.Count;

        var shouldApply = _shouldApply.Value;
        for (int i = 0; i < count; i++)
        {
            if (!shouldApply[i]) continue;
            var weight = weights[i];
            // if (proxy.GetBlendShapeWeight(i) == weight) continue;
            proxy.SetBlendShapeWeight(i, weight);
        }
    }

    public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context, RenderAspects updatedAspects)
    {
        if (updatedAspects != 0 && (updatedAspects & RenderAspects.Mesh) == 0)
        {
            return Task.FromResult<IRenderFilterNode>(this);
        }
        return Task.FromResult<IRenderFilterNode>(null!);
    }
    
    public void Dispose()
    {
        _blendShapeNames.Dispose();
        _blendShapeWeights.Dispose();
        _shouldApply.Dispose();
        Disposed = true;
    }
}

internal class EmptyNode : IRenderFilterNode
{
    public RenderAspects WhatChanged { get; private set;}
    public EmptyNode(RenderAspects aspects)
    {
        WhatChanged = aspects;
    }
    
    public void OnFrame(Renderer original, Renderer proxy) { }
}
