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

    public bool IsEnabled(ComputeContext context)
    {
        if (ControlNode == null) return true;
        return context.Observe(ControlNode.IsEnabled);
    }

    // FaceTuneTagComponentが一つでもあれば対象に加える
    ImmutableList<RenderGroup> IRenderFilter.GetTargetGroups(ComputeContext context)
    {
        var groups = new List<RenderGroup>();
        foreach (var root in context.GetAvatarRoots())
        {
            if (!context.ActiveInHierarchy(root)) continue;

            var components = context.GetComponentsInChildren<FaceTuneTagComponent>(root, true);
            if (components.Count() == 0) continue;

            if (!SessionContextBuilder.TryGet(root, out var sessionContext)) continue;

            var faceRenderer = sessionContext.FaceRenderer;
            groups.Add(RenderGroup.For(faceRenderer).WithData(sessionContext));
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

        var sessionContext = group.GetData<SessionContext>();
        if (sessionContext == null) return Error();

        var blendShapeSet = QueryBlendShapeSet(original, proxy, sessionContext, context);
        // プレビューするブレンドシェイプが存在しない場合は空のプレビューを行う
        if (blendShapeSet == null || blendShapeSet.BlendShapes.Count() == 0) return Task.FromResult<IRenderFilterNode>(new EmptyNode());

        var blendShapeWeights = blendShapeSet.ToArrayForMesh(proxyMesh, _ => -1) // 対象外の場合はフラグとして-1を代入しNode側で除外
            .Select(b => b.Weight).ToArray();

        return Task.FromResult<IRenderFilterNode>(new BlendShapePreviewNode(blendShapeWeights));

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
}

internal class EmptyNode : IRenderFilterNode
{
    public RenderAspects WhatChanged => 0;
    public void OnFrame(Renderer original, Renderer proxy) { }
}
