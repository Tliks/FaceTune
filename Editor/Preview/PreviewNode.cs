using System.Threading.Tasks;
using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune.Preview;

internal readonly record struct BlendShapePreviewLayerState(
    BlendShapeApply Apply,
    float Opacity = 1f);

internal class BlendShapePreviewNode : IRenderFilterNode
{
    public RenderAspects WhatChanged => RenderAspects.Shapes;

    private readonly BlendShapePreviewLayerState[] _layers;
    private readonly int _blendShapeCount;
    private readonly PooledObject<List<string>> _blendShapeNames;
    private readonly PooledObject<List<float>> _blendShapeWeights;
    private readonly PooledObject<List<float>> _blendShapeOpacities;
    private readonly PooledObject<List<bool>> _shouldApply;
    private bool _compositionDirty;

    public bool Disposed { get; private set; }

    public BlendShapePreviewNode(SkinnedMeshRenderer smr, params BlendShapeApply[] applies)
        : this(smr, applies.Select(apply => new BlendShapePreviewLayerState(apply)).ToArray())
    {
    }

    internal BlendShapePreviewNode(
        SkinnedMeshRenderer smr,
        BlendShapePreviewLayerState[] layers)
    {
        _layers = layers.ToArray();

        var mesh = smr.sharedMesh.DestroyedAsNull()
            ?? throw new ArgumentException("Renderer has no mesh.", nameof(smr));

        _blendShapeCount = mesh.blendShapeCount;
        _blendShapeNames = ListPool<string>.Get(out var names);
        for (int i = 0; i < _blendShapeCount; i++)
            names.Add(mesh.GetBlendShapeName(i));
        _blendShapeWeights = ListPool<float>.Get(out _);
        _blendShapeOpacities = ListPool<float>.Get(out _);
        _shouldApply = ListPool<bool>.Get(out _);
        _compositionDirty = true;
    }

    public void SetDirectly(int layerIndex, BlendShapePreviewLayerState layer)
    {
        _layers[layerIndex] = layer;
        _compositionDirty = true;
    }

    private void Recompose()
    {
        var names = _blendShapeNames.Value;
        var weights = _blendShapeWeights.Value;
        var opacities = _blendShapeOpacities.Value;
        var shouldApply = _shouldApply.Value;
        weights.Clear();
        opacities.Clear();
        shouldApply.Clear();

        for (int i = 0; i < _blendShapeCount; i++)
        {
            var weight = 0f;
            var opacity = 0f;
            foreach (var layer in _layers)
            {
                if (!layer.Apply.TryGetWeight(names[i], out var layerWeight)) continue;
                var layerOpacity = Mathf.Clamp01(layer.Opacity);
                if (layerOpacity <= 0f) continue;

                // レイヤー列を単一の Lerp(upstream, weight, opacity) として保持する。
                var remainingWeight = (1f - layerOpacity) * opacity * weight;
                var layerContribution = layerOpacity * layerWeight;
                var combinedOpacity = opacity + layerOpacity * (1f - opacity);
                weight = (remainingWeight + layerContribution) / combinedOpacity;
                opacity = combinedOpacity;
            }
            weights.Add(weight);
            opacities.Add(opacity);
            shouldApply.Add(opacity > 0f);
        }
        _compositionDirty = false;
    }

    public void OnFrame(Renderer original, Renderer proxy)
    {
        if (proxy is SkinnedMeshRenderer smr)
            OnFrameInternal(smr);
    }

    private void OnFrameInternal(SkinnedMeshRenderer proxy)
    {
        if (Disposed || !proxy.enabled) return;
        if (_compositionDirty) Recompose();

        var weights = _blendShapeWeights.Value;
        var opacities = _blendShapeOpacities.Value;
        var shouldApply = _shouldApply.Value;
        for (int i = 0; i < _blendShapeCount; i++)
        {
            if (!shouldApply[i]) continue;
            var opacity = opacities[i];
            var weight = opacity >= 1f
                ? weights[i]
                : Mathf.Lerp(proxy.GetBlendShapeWeight(i), weights[i], opacity);
            proxy.SetBlendShapeWeight(i, weight);
        }
    }

    public Task<IRenderFilterNode> Refresh(
        IEnumerable<(Renderer, Renderer)> proxyPairs,
        ComputeContext context,
        RenderAspects updatedAspects)
    {
        if (updatedAspects != 0 && (updatedAspects & RenderAspects.Mesh) == 0)
            return Task.FromResult<IRenderFilterNode>(this);
        return Task.FromResult<IRenderFilterNode>(null!);
    }

    public void Dispose()
    {
        _blendShapeNames.Dispose();
        _blendShapeWeights.Dispose();
        _blendShapeOpacities.Dispose();
        _shouldApply.Dispose();
        Disposed = true;
    }
}

internal class EmptyNode : IRenderFilterNode
{
    public RenderAspects WhatChanged { get; private set; }

    public EmptyNode(RenderAspects aspects)
    {
        WhatChanged = aspects;
    }

    public void OnFrame(Renderer original, Renderer proxy) { }
}
