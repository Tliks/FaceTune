using System.Threading.Tasks;
using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune.Preview;

internal abstract class AbstractFaceTunePreview<TFilter> : IRenderFilter where TFilter : IRenderFilter
{
    protected virtual TogglablePreviewNode? ControlNode { get; }
    private static IRenderFilterNode? _currentNode;

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
        using var _ = new ProfilingSampleScope($"{typeof(TFilter).Name}.GetTargetGroups");
        var groups = new List<RenderGroup>();
        var observeContext = new NDMFPreviewObserveContext(context);
        foreach (var root in context.GetAvatarRoots())
        {
            if (!context.ActiveInHierarchy(root)) continue;
            if (!AvatarContextBuilder.TryGetFaceRenderer(root, out var faceRenderer, out var bodyPath, null, observeContext)) continue;
            var faceMesh = context.Observe(faceRenderer, r => r.sharedMesh, (a, b) => a == b);
            if (faceMesh == null) continue;
            groups.Add(RenderGroup.For(faceRenderer).WithData((root, bodyPath)));
        }
        return groups.ToImmutableList();
    }

    Task<IRenderFilterNode> IRenderFilter.Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
    {
        return Instantiate(group, proxyPairs, context);
    }

    protected virtual Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
    {
        using var _ = new ProfilingSampleScope($"{typeof(TFilter).Name}.Instantiate");
        try
        {
            var pair = proxyPairs.First();
            if (pair.Item1 is not SkinnedMeshRenderer original) throw new Exception("SkinnedMeshRenderer not found");
            if (pair.Item2 is not SkinnedMeshRenderer proxy) throw new Exception("SkinnedMeshRenderer not found");

            var originalMesh = original.sharedMesh;
            if (originalMesh == null) throw new Exception("originalMesh is null");
            var proxyMesh = proxy.sharedMesh;
            if (proxyMesh == null) throw new Exception("proxyMesh is null");

            var (root, bodyPath) = group.GetData<(GameObject, string)>();
            if (root == null) throw new Exception("GameObject not found");
            if (bodyPath == null) throw new Exception("bodyPath not found");

            float defaultValue = -1;
            using var _set = BlendShapeSetPool.Get(out var set);
            using var _2 = new ProfilingSampleScope($"{typeof(TFilter).Name}.QueryBlendShapes");
            {
                QueryBlendShapes(original, proxy, root, bodyPath, context, set, ref defaultValue);
            }

            _currentNode = new BlendShapePreviewNode(proxy, set.AsReadOnly(), defaultValue);
            return Task.FromResult(_currentNode);
        }
        catch (Exception e)
        {
            LocalizedLog.Error("Preview:Log:error:failedToInstantiate", e.Message);
            // 現状nullや例外を返すとNDMF側で永続的なエラーを引き起こすのでこれはそのワークアラウンド
            _currentNode = new EmptyNode();
            return Task.FromResult(_currentNode);
        }
    }

    /// <summary>
    /// プレビューするブレンドシェイプを取得する。
    /// IRenderFilter.Instantiate内で呼ばれるので適時Observe。
    /// defaultValueはresultに含まれていない場合の扱い。-1の場合はプレビューしない(デフォルト)
    /// </summary>
    protected abstract void QueryBlendShapes(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, GameObject root, string bodyPath, ComputeContext context, BlendShapeSet result, ref float defaultValue);

    /// <summary>
    ///  現在のNodeの内容を直接置き換える。
    /// </summary>
    /// <param name="set">置き換えるBlendShapeSet</param>
    /// <param name="defaultValue">BlendShapeSetに含まれていない場合の扱い。-1の場合はプレビューしない</param>
    protected static void SetCurrentNodeDirectly(IReadOnlyBlendShapeSet set, float defaultValue = -1)
    {
        if (_currentNode is BlendShapePreviewNode node && !node.Disposed)
        {
            node.SetDirectly(set, defaultValue);
        }
    }

    /// <summary>
    /// 現在のNodeの内容を直接編集する。
    /// BlendShapeSetに含まれていない場合は現在の値を据え置く。
    /// </summary>
    /// <param name="set">編集するBlendShapeSet</param>
    protected static void EditCurrentNodeDirectly(IReadOnlyBlendShapeSet set)
    {
        if (_currentNode is BlendShapePreviewNode node && !node.Disposed)
        {
            node.EditDirectly(set);
        }
    }
}

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

    // setにあるもののみ上書き
    private void EditInternal(IReadOnlyBlendShapeSet set)
    {
        if (Disposed) return;
        var names = _blendShapeNames.Value;
        var current = _blendShapeWeights.Value;
        for (int i = 0; i < _blendShapeCount; i++)
        {
            if (set.TryGetValue(names[i], out var blendShape))
            {
                current[i] = blendShape.Weight;
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

    public void EditDirectly(IReadOnlyBlendShapeSet set)
    {
        EditInternal(set);
        if (_latestProxy != null)
        {
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

        var weights = _blendShapeWeights.Value;
        var count = weights.Count;

        for (int i = 0; i < count; i++)
        {
            var weight = weights[i];
            if (weight == -1) continue; // 対象外
            proxy.SetBlendShapeWeight(i, weight);
        }
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
    public RenderAspects WhatChanged => 0;
    public void OnFrame(Renderer original, Renderer proxy) { }
}
