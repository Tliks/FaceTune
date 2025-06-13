using System.Threading.Tasks;
using nadena.dev.ndmf.preview;

namespace com.aoyon.facetune.preview;

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
        var pair = proxyPairs.First();
        if (pair.Item1 is not SkinnedMeshRenderer original) return Error();
        if (pair.Item2 is not SkinnedMeshRenderer proxy) return Error();

        var originalMesh = original.sharedMesh;
        if (originalMesh == null) return Error();
        var proxyMesh = proxy.sharedMesh;
        if (proxyMesh == null) return Error();

        var root = group.GetData<GameObject>();
        if (root == null) return Error();

        var observeContext = new NDMFPreviewObserveContext(context);
        if (!SessionContextBuilder.TryBuild(root, out var sessionContext, observeContext)) return Empty();

        var blendShapeSet = QueryBlendShapeSet(original, proxy, sessionContext, context);
        // プレビューするブレンドシェイプが存在しない場合は空のプレビューを行う
        if (blendShapeSet == null || blendShapeSet.BlendShapes.Count() == 0) return Empty();

        var blendShapeWeights = FloatArrayPool.Get(proxyMesh.blendShapeCount);
        for (int i = 0; i < proxyMesh.blendShapeCount; i++)
        {
            var name = proxyMesh.GetBlendShapeName(i);
            if (blendShapeSet.GetMapping().TryGetValue(name, out var blendShape))
            {
                blendShapeWeights[i] = blendShape.Weight;
            }
            else
            {
                blendShapeWeights[i] = -1; // 対象外の場合はフラグとして-1を代入しNode側で除外
            }
        }

        return Task.FromResult<IRenderFilterNode>(new BlendShapePreviewNode(blendShapeWeights));
        
        static Task<IRenderFilterNode> Empty()
        {
            return Task.FromResult<IRenderFilterNode>(new EmptyNode());
        }

        // 現状nullや例外を返すとNDMF側で永続的なエラーを引き起こすのでこれはそのワークアラウンド
        static Task<IRenderFilterNode> Error()
        {
            Debug.LogError("Failed to instantiate preview filter");
            return Task.FromResult<IRenderFilterNode>(new EmptyNode());
        }
    }

    /// <summary>
    /// プレビューするブレンドシェイプを取得する
    /// IRenderFilter.Instantiate内で呼ばれるので適時Observe
    /// </summary>
    protected abstract BlendShapeSet? QueryBlendShapeSet(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, SessionContext sessionContext, ComputeContext context);
}

internal class BlendShapePreviewNode : IRenderFilterNode
{
    public RenderAspects WhatChanged => RenderAspects.Shapes;
    private readonly float[] _blendShapeWeights; 

    public BlendShapePreviewNode(float[] blendShapeWeights)
    {
        _blendShapeWeights = blendShapeWeights;
    }
    
    public void OnFrame(Renderer original, Renderer proxy)
    {
        var smr = proxy as SkinnedMeshRenderer;
        if (smr == null) return;

        var mesh = smr.sharedMesh;
        if (mesh == null) return;

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (_blendShapeWeights[i] == -1) continue; // 対象外
            smr.SetBlendShapeWeight(i, _blendShapeWeights[i]);
        }
    }

    public void Dispose()
    {
        FloatArrayPool.Return(_blendShapeWeights);
    }
}

internal class EmptyNode : IRenderFilterNode
{
    public RenderAspects WhatChanged => 0;
    public void OnFrame(Renderer original, Renderer proxy) { }
}
