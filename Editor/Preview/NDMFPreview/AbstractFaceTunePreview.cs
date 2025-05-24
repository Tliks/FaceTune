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

    // mainComponetからFaceRendererを取得できればプレビュー対象には入れる
    // mainComponetに依存しない対象の追加方法が必要か考える
    ImmutableList<RenderGroup> IRenderFilter.GetTargetGroups(ComputeContext context)
    {
        var groups = new List<RenderGroup>();
        foreach (var root in context.GetAvatarRoots())
        {
            if (!context.ActiveInHierarchy(root)) continue;

            var mainComponents = context.GetComponentsInChildren<FaceTuneComponent>(root, true)
                .Where(c => context.ActiveInHierarchy(c.gameObject));
            if (mainComponents.Count() == 0) continue;
            if (mainComponents.Count() > 1) { Debug.LogError("FaceTuneComponent is not unique"); continue; }
            var mainComponent = mainComponents.First();

            var canBuild = context.Observe(mainComponent, c => c.CanBuild(), (a, b) => a == b);
            if (canBuild is false) continue;
            
            var renderer = context.Observe(mainComponent, r => r.FaceObject.GetComponent<SkinnedMeshRenderer>(), (a, b) => a == b)!;

            groups.Add(RenderGroup.For(renderer).WithData(mainComponent));
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

        var mainComponent = group.GetData<FaceTuneComponent>();
        if (mainComponent == null) return Error();

        var blendShapeSet = QueryBlendShapeSet(original, proxy, mainComponent, context);
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
    protected abstract BlendShapeSet? QueryBlendShapeSet(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, FaceTuneComponent mainComponent, ComputeContext context);
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
