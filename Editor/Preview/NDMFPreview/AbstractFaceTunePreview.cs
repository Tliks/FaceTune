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

            var faceRenderer = SessionContextBuilder.GetFaceRenderer(root, observeContext);
            if (faceRenderer == null) continue;

            groups.Add(RenderGroup.For(faceRenderer).WithData(root));
        }
        return groups.ToImmutableList();
    }

    Task<IRenderFilterNode> IRenderFilter.Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
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

            // プレビューするブレンドシェイプが存在しない場合は空のプレビューを行う
            if (set.Count == 0) return Empty();

            var _blendShapeWeights = ListPool<float>.Get(out var blendShapeWeights);
            for (int i = 0; i < proxyMesh.blendShapeCount; i++)
            {
                var name = proxyMesh.GetBlendShapeName(i);
                if (set.TryGetValue(name, out var blendShape))
                {
                    blendShapeWeights.Add(blendShape.Weight);
                }
                else
                {
                    blendShapeWeights.Add(-1); // 対象外の場合はフラグとして-1を代入しNode側で除外
                }
            }

            return Task.FromResult<IRenderFilterNode>(new BlendShapePreviewNode(_blendShapeWeights));
        }
        catch (Exception e)
        {
            return Error(e.Message);
        }
        
        static Task<IRenderFilterNode> Empty()
        {
            return Task.FromResult<IRenderFilterNode>(new EmptyNode());
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
    private readonly PooledObject<List<float>> _blendShapeWeights; 

    public BlendShapePreviewNode(PooledObject<List<float>> blendShapeWeights)
    {
        _blendShapeWeights = blendShapeWeights;
    }
    
    public void OnFrame(Renderer original, Renderer proxy)
    {
        var smr = proxy as SkinnedMeshRenderer;
        if (smr == null) return;

        var mesh = smr.sharedMesh;
        if (mesh == null) return;

        var weights = _blendShapeWeights.Value;
        var count = weights.Count;

        for (int i = 0; i < count; i++)
        {
            if (weights[i] == -1) continue; // 対象外
            smr.SetBlendShapeWeight(i, weights[i]);
        }
    }

    public void Dispose()
    {
        _blendShapeWeights.Dispose();
    }
}

internal class EmptyNode : IRenderFilterNode
{
    public RenderAspects WhatChanged => 0;
    public void OnFrame(Renderer original, Renderer proxy) { }
}
