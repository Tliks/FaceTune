using System.Threading.Tasks;
using nadena.dev.ndmf.preview;

namespace aoyon.facetune.preview;

internal abstract class AbstractFaceTunePreview : IRenderFilter
{
    protected virtual TogglablePreviewNode? ControlNode { get; }

    public IEnumerable<TogglablePreviewNode> GetPreviewControlNodes()
    {
        if (ControlNode == null) yield break;
        yield return ControlNode;
    }

    public virtual bool IsEnabled(ComputeContext context)
    {
        if (ControlNode == null) return true;
        return context.Observe(ControlNode.IsEnabled);
    }

    // FaceTuneTagComponentが一つでもあれば対象に加える or 全て対象
    // => 全て対象にする
    ImmutableList<RenderGroup> IRenderFilter.GetTargetGroups(ComputeContext context)
    {
        var groups = new List<RenderGroup>();
        var observeContext = new NDMFPreviewObserveContext(context);
        foreach (var root in context.GetAvatarRoots())
        {
            if (!context.ActiveInHierarchy(root)) continue;

            var faceRenderer = SessionContextBuilder.GetFaceRenderer(root, null, observeContext);
            if (faceRenderer == null) continue;

            groups.Add(RenderGroup.For(faceRenderer).WithData(root));
        }
        return groups.ToImmutableList();
    }

    Task<IRenderFilterNode> IRenderFilter.Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
    {
        return Instantiate(group, proxyPairs, context);
    }

    protected virtual Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
    {
        try
        {
            var pair = proxyPairs.First();
            if (pair.Item1 is not SkinnedMeshRenderer original) return Error("SkinnedMeshRenderer not found");
            if (pair.Item2 is not SkinnedMeshRenderer proxy) return Error("SkinnedMeshRenderer not found");

            var originalMesh = original.sharedMesh;
            if (originalMesh == null) return Error("SkinnedMeshRenderer.sharedMesh is null");
            var proxyMesh = proxy.sharedMesh;
            if (proxyMesh == null) return Error("SkinnedMeshRenderer.sharedMesh is null");

            var root = group.GetData<GameObject>();
            if (root == null) return Error("GameObject not found");

            using var _set = BlendShapeSetPool.Get(out var set);
            QueryBlendShapes(original, proxy, root, context, set);

            return Task.FromResult<IRenderFilterNode>(new BlendShapePreviewNode(proxy, set));
        }
        catch (Exception e)
        {
            return Error(e.Message);
        }
        
        // 現状nullや例外を返すとNDMF側で永続的なエラーを引き起こすのでこれはそのワークアラウンド
        static Task<IRenderFilterNode> Error(string? message = null)
        {
            if (message != null) Debug.LogError(message);
            Debug.LogError("Failed to instantiate preview filter");
            return Task.FromResult<IRenderFilterNode>(new EmptyNode());
        }
    }

    /// <summary>
    /// プレビューするブレンドシェイプを取得する
    /// IRenderFilter.Instantiate内で呼ばれるので適時Observe
    /// </summary>
    protected abstract void QueryBlendShapes(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, GameObject root, ComputeContext context, BlendShapeSet result);
}

internal class BlendShapePreviewNode : IRenderFilterNode
{
    public RenderAspects WhatChanged => RenderAspects.Shapes;

    private readonly SkinnedMeshRenderer _original;
    private readonly Mesh _originalMesh;
    private readonly int _blendShapeCount;
    private readonly string[] _blendShapeNames;

    public readonly PooledObject<List<float>> BlendShapeWeights;

    private SkinnedMeshRenderer? _latestProxy;

    public BlendShapePreviewNode(SkinnedMeshRenderer original, BlendShapeSet set)
    {
        _original = original;
        _originalMesh = original.sharedMesh;
        _blendShapeCount = _originalMesh.blendShapeCount;
        _blendShapeNames = new string[_blendShapeCount];
        for (int i = 0; i < _blendShapeCount; i++)
        {
            _blendShapeNames[i] = _originalMesh.GetBlendShapeName(i);
        }
        BlendShapeWeights = ListPool<float>.Get(out _);
        RefreshInternal(set);
    }
    
    private void RefreshInternal(BlendShapeSet set)
    {
        var current = BlendShapeWeights.Value;
        current.Clear();

        for (int i = 0; i < _blendShapeCount; i++)
        {
            if (set.TryGetValue(_blendShapeNames[i], out var blendShape))
            {
                current.Add(blendShape.Weight);
            }
            else
            {
                current.Add(-1);
            }
        }
    }

    // 外部から直接書き換えることで再生成を伴うことなく、高速にプレビューを更新する
    // 他のNodeの更新を必要しない下流かつ一時的な、また高頻度な更新を必要とするプレビュー用(EditingShapesPreview)
    // パフォーマンスは良いものの、NDMF Previewの設計から外れてていると思われ、将来の動作は保証されない
    // Todo: 今後のAPI変更に伴って書き換える
    public void RefreshDirectly(BlendShapeSet set)
    {
        RefreshInternal(set);
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
        var mesh = proxy.sharedMesh;
        if (mesh == null) return;

        var weights = BlendShapeWeights.Value;
        var count = weights.Count;

        for (int i = 0; i < count; i++)
        {
            if (weights[i] == -1) continue; // 対象外
            proxy.SetBlendShapeWeight(i, weights[i]);
        }
    }

    public void Dispose()
    {
        BlendShapeWeights.Dispose();
    }
}

internal class EmptyNode : IRenderFilterNode
{
    public RenderAspects WhatChanged => 0;
    public void OnFrame(Renderer original, Renderer proxy) { }
}
