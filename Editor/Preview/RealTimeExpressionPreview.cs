using System.Threading.Tasks;
using nadena.dev.ndmf.preview;


namespace Aoyon.FaceTune.Preview;

// early
internal class RealTimeExpressionPreview : IRenderFilter
{
    ImmutableList<RenderGroup> IRenderFilter.GetTargetGroups(ComputeContext context)
    {
        var builder = ImmutableList.CreateBuilder<RenderGroup>();
        foreach (var root in context.GetAvatarRoots())
        {
            if (!AvatarContext.TryGet(root, out var avatarContext, out _, context)) continue;

            var component = _targetComponent.Get(context, root);
            if (component == null) continue;

            // PassingDataにComponentを入れるとNDMF側でNREが発生するのでIndexに変換する。Todo: NDMF側の修正
            var componentIndex = Array.IndexOf(root.GetComponentsInChildren<ExpressionComponent>(), component);
            var data = new PassingData(root, componentIndex, avatarContext.BodyPath);

            builder.Add(RenderGroup.For(avatarContext.FaceRenderer).WithData(data, (a, b) => a.Equals(b)));
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
            var enabled = context.Observe(component, c => c.AlwaysOnPreviewEnabled, (a, b) => a == b);
            if (!enabled) continue;
            var isEditorOnly = context.EditorOnlyInHierarchy(component.gameObject);
            if (isEditorOnly) continue;
            if (target != null) LocalizedLog.Warning("realTimeExpressionPreview.log.warning.multipleExpressionComponentWithEnableRealTimePreview");
            target = component;
        }
        
        return target;
    }

    record PassingData(GameObject Root, int ComponentIndex, string FacePath);

    Task<IRenderFilterNode> IRenderFilter.Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
    {
        try
        {
            var pair = proxyPairs.First();
            if (pair.Item1 is not SkinnedMeshRenderer renderer
                || pair.Item2 is not SkinnedMeshRenderer proxy)
                throw new Exception("SkinnedMeshRenderer not found");

            var data = group.GetData<PassingData>();
            var component = data.Root.GetComponentsInChildren<ExpressionComponent>()[data.ComponentIndex];

            var apply = _blendShapeApply.Get(context, (renderer, data.Root, component, data.FacePath));
            var node = new BlendShapePreviewNode(proxy, apply);
            
            return Task.FromResult<IRenderFilterNode>(node);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return Task.FromResult<IRenderFilterNode>(new EmptyNode(0));
        }
    }

    // 再計算の範囲を縮小するためのPropCache
    private static readonly PropCache<(SkinnedMeshRenderer, GameObject, Component, string), BlendShapeApply> _blendShapeApply = new(
        $"{nameof(RealTimeExpressionPreview)}:TargetComponent", GetBlendShapeApply, (a, b) => a.Equals(b)
    );

    private static BlendShapeApply GetBlendShapeApply(ComputeContext context, 
        (SkinnedMeshRenderer Renderer, GameObject Root, Component Component, string FacePath) input)
    {
        var (renderer, Root, Component, FacePath) = input;

        using var _ = ListPool<BlendShapeWeightAnimation>.Get(out var animations);
        var facial = new FacialAnimationResolver(Root, context);
        animations.AddRange(facial.ResolveIncoming(Component.transform, FacePath));
        if (facial.TryResolve(Component, FacePath, out var definition))
            animations.AddRange(definition);
        var set = new BlendShapeWeightSet(animations.ToFirstFrameBlendShapes());

        var ignoredNames = AvatarContext.GetExplicitlyExcludedBlendShapeNames(Root, context);

        return new BlendShapeApply(renderer, set.AsReadOnly(), 0f, ignoredNames);
    }
}