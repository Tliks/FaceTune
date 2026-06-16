using System.Threading.Tasks;
using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune.Preview;

// early
internal class RealTimeExpressionPreview : IRenderFilter
{
    ImmutableList<RenderGroup> IRenderFilter.GetTargetGroups(ComputeContext context)
    {
        var observeContext = new NDMFPreviewObserveContext(context);
        var builder = ImmutableList.CreateBuilder<RenderGroup>();
        foreach (var root in context.GetAvatarRoots())
        {
            if (!AvatarContextBuilder.TryGetFaceRenderer(root, out var faceRenderer, out var facePath, null, observeContext)) continue;
            
            var faceMesh = context.Observe(faceRenderer, r => r.sharedMesh, (a, b) => a == b);
            if (faceMesh == null) continue;

            var component = _targetComponent.Get(context, root);
            if (component == null) continue;

            var data = new PassingData(root, component, facePath);
            builder.Add(RenderGroup.For(faceRenderer).WithData(data, (a, b) => a.Equals(b)));
        }
        return builder.ToImmutable();
    }

    // ExpressionComponent増減時の再計算の範囲を縮小するためのPropCache
    private static readonly PropCache<GameObject, ExpressionComponent?> _targetComponent = new(
        $"{nameof(RealTimeExpressionPreview)}:TargetComponent", GetTargetComponent, (a, b) => a == b
    );
    
    private static ExpressionComponent? GetTargetComponent(ComputeContext context, GameObject root)
    {
        using var _ = ListPool<ExpressionComponent>.Get(out var components);
        context.GetComponentsInChildren<ExpressionComponent>(root, true, components);

        ExpressionComponent? target = null;
        foreach (var component in components)
        {
            var enabled = context.Observe(component, c => c.EnableRealTimePreview, (a, b) => a == b);
            if (!enabled) continue;
            var isEditorOnly = context.EditorOnlyInHierarchy(component.gameObject);
            if (isEditorOnly) continue;
            if (target != null) LocalizedLog.Warning("RealTimeExpressionPreview:Log:warning:MultipleExpressionComponentWithEnableRealTimePreview");
            target = component;
        }
        
        return target;
    }

    record PassingData(GameObject Root, ExpressionComponent Component, string FacePath);

    Task<IRenderFilterNode> IRenderFilter.Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
    {
        try
        {
            var pair = proxyPairs.First();
            if (pair.Item2 is not SkinnedMeshRenderer proxy) throw new Exception("SkinnedMeshRenderer not found");

            var data = group.GetData<PassingData>();

            using var _set = BlendShapeSetPool.Get(out var set);

            var defaultValue = 0f; // 明示されないブレンドシェイプは0で初期化し、他の影響を打ち消す
            GetBlendShapes(context, set, data.Component, data.Root, data.FacePath);

            var node = new BlendShapePreviewNode(proxy, set.AsReadOnly(), defaultValue);
            return Task.FromResult<IRenderFilterNode>(node);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return Task.FromResult<IRenderFilterNode>(new EmptyNode(0));
        }
    }

    private void GetBlendShapes(ComputeContext context, BlendShapeWeightSet result, ExpressionComponent target, GameObject root, string bodyPath)
    {
        var observeContext = new NDMFPreviewObserveContext(context);

        using var _3 = ListPool<BlendShapeWeightAnimation>.Get(out var facialStyleAnimations);
        FacialStyleContext.TryGetFacialStyleAnimationsAndObserve(target.gameObject, facialStyleAnimations, root, observeContext);
        result.AddRange(facialStyleAnimations.ToFirstFrameBlendShapes());

        using var _4 = ListPool<ExpressionDataComponent>.Get(out var dataComponents);
        context.GetComponentsInChildren<ExpressionDataComponent>(target.gameObject, true, dataComponents);
        foreach (var dataComponent in dataComponents)
        {
            dataComponent.GetBlendShapes(result, facialStyleAnimations, bodyPath, observeContext);
        }
    }
}