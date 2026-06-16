using System.Threading.Tasks;
using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune.Preview;

internal class BlendShapePreviewNode : IRenderFilterNode
{
    public RenderAspects WhatChanged => RenderAspects.Shapes;

    private readonly int _blendShapeCount;
    private readonly PooledObject<List<string>> _blendShapeNames;
    private readonly PooledObject<List<float>> _blendShapeWeights;

    private SkinnedMeshRenderer? _latestProxy;

    public bool Disposed { get; private set; }

    public BlendShapePreviewNode(SkinnedMeshRenderer smr, IReadOnlyBlendShapeSet set, float defaultValue = -1)
    {
        var mesh = smr.sharedMesh;
        _blendShapeCount = mesh.blendShapeCount;
        _blendShapeNames = ListPool<string>.Get(out var names);
        for (int i = 0; i < _blendShapeCount; i++)
        {
            names.Add(mesh.GetBlendShapeName(i));
        }
        _blendShapeWeights = ListPool<float>.Get(out _);
        SetInternal(set, defaultValue);
    }

    /// defaultValueはsetになかった場合のハンドリング
    /// プレビューしない場合は-1
    private void SetInternal(IReadOnlyBlendShapeSet set, float defaultValue)
    {
        if (Disposed) return;
        var names = _blendShapeNames.Value;
        var current = _blendShapeWeights.Value;
        current.Clear();
        for (int i = 0; i < _blendShapeCount; i++)
        {
            if (set.TryGetValue(names[i], out var blendShape))
            {
                current.Add(blendShape.Weight);
            }
            else
            {
                current.Add(defaultValue);
            }
        }
    }

    // 外部から直接書き換えることで再生成を伴うことなく、高速にプレビューを更新する
    // 他のNodeの更新を必要しない下流かつ一時的な、また高頻度な更新を必要とするプレビュー用(EditingShapesPreview)
    // パフォーマンスは良いものの、NDMF Previewの設計から外れてていると思われ、将来の動作は保証されない
    // Todo: 今後のAPI変更に伴って書き換える
    public void SetDirectly(IReadOnlyBlendShapeSet set, float defaultValue)
    {
        SetInternal(set, defaultValue);
        if (_latestProxy != null)
        {
            // OnFrameがCamera.onPreCullを購読しているため、GUI等の一部の操作で発火しない
            // そのため明示的にOnFrameを呼び更新する
            OnFrameInternal(_latestProxy);
        }
    }

    public void OnFrame(Renderer original, Renderer proxy)
    {
        var smr = proxy as SkinnedMeshRenderer;
        if (smr == null) return;
        _latestProxy = smr;

        OnFrameInternal(smr);
    }

    private void OnFrameInternal(SkinnedMeshRenderer proxy)
    {
        if (Disposed) return;
        if (!proxy.enabled) return;

        var weights = _blendShapeWeights.Value;
        var count = weights.Count;

        for (int i = 0; i < count; i++)
        {
            var weight = weights[i];
            if (weight == -1) continue; // 対象外
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
