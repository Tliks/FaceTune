namespace com.aoyon.facetune;

// Todo
internal record DefaultExpressionContext
{
    private readonly Expression defaultExpression;
    private readonly Dictionary<PresetComponent, Expression?> presetDefaultExpressions;
    private readonly HashSet<PresetComponent> presetComponents;

    public DefaultExpressionContext(Expression defaultExpression, Dictionary<PresetComponent, Expression?> presetDefaultExpressions)
    {
        this.defaultExpression = defaultExpression;
        this.presetDefaultExpressions = presetDefaultExpressions;
        presetComponents = presetDefaultExpressions.Keys.ToHashSet();
    }

    public Expression GetGlobalDefaultExpression()
    {
        return defaultExpression;
    }

    public BlendShapeSet GetGlobalDefaultBlendShapeSet()
    {
        return defaultExpression.AnimationIndex.GetAllFirstFrameBlendShapeSet();
    }

    public Expression GetPresetDefaultExpression(PresetComponent preset)
    {
        if (presetDefaultExpressions.TryGetValue(preset, out var expression) && expression != null)
        {
            return expression;
        }
        return defaultExpression;
    }

    public BlendShapeSet GetPresetDefaultBlendShapeSet(PresetComponent preset)
    {
        return GetPresetDefaultExpression(preset).AnimationIndex.GetAllFirstFrameBlendShapeSet();
    }

    public Expression GetDefaultExpression(GameObject target)
    {
        if (presetComponents.Contains(target.GetComponent<PresetComponent>()))
        {
            return GetPresetDefaultExpression(target.GetComponent<PresetComponent>());
        }
        else
        {
            var presets = target.GetComponentsInParent<PresetComponent>(true);
            foreach (var preset in presets)
            {
                if (presetDefaultExpressions.TryGetValue(preset, out var expression) && expression != null)
                {
                    return expression;
                }
            }
            return defaultExpression;
        }
    }

    public BlendShapeSet GetDefaultBlendShapeSet(GameObject target)
    {
        return GetDefaultExpression(target).AnimationIndex.GetAllFirstFrameBlendShapeSet();
    }

    public IEnumerable<Expression> GetAllExpressions()
    {
        return presetDefaultExpressions.Values
            .Where(expr => expr != null)
            .Concat(new[] { defaultExpression })!;
    }
}

// プレビュー用
internal class DefaultBlendShapesContext : IDisposable
{
    private readonly PooledObject<BlendShapeSet> defaultBlendShapes;
    private readonly PooledObject<Dictionary<PresetComponent, PooledObject<BlendShapeSet>?>> presetDefaultBlendShapes;
    private readonly PooledObject<HashSet<PresetComponent>> presetComponents;
    public bool Disposed { private set; get; } = false;

    public DefaultBlendShapesContext(PooledObject<BlendShapeSet> defaultBlendShapes, PooledObject<Dictionary<PresetComponent, PooledObject<BlendShapeSet>?>> presetDefaultBlendShapes)
    {
        this.defaultBlendShapes = defaultBlendShapes;
        this.presetDefaultBlendShapes = presetDefaultBlendShapes;
        presetComponents = HashSetPool<PresetComponent>.Get(out _);
        presetComponents.Value.UnionWith(presetDefaultBlendShapes.Value.Keys);
    }

    public BlendShapeSet GetGlobalDefaultBlendShapes()
    {
        return defaultBlendShapes.Value;
    }

    public BlendShapeSet GetPresetDefaultBlendShapes(PresetComponent preset)
    {
        if (presetDefaultBlendShapes.Value.TryGetValue(preset, out var pooledBlendShapes) && pooledBlendShapes != null)
        {
            return pooledBlendShapes.Value;
        }
        return defaultBlendShapes.Value;
    }

    public BlendShapeSet GetDefaultBlendShapes(GameObject target)
    {
        if (target.TryGetComponent<PresetComponent>(out var preset) && presetComponents.Value.Contains(preset))
        {
            return GetPresetDefaultBlendShapes(preset);
        }
        else
        {
            using var _ = ListPool<PresetComponent>.Get(out var parentPresets);
            target.GetComponentsInParent<PresetComponent>(true, parentPresets);
            foreach (var parentPreset in parentPresets)
            {
                if (presetDefaultBlendShapes.Value.TryGetValue(parentPreset, out var blendShapes) && blendShapes != null)
                {
                    return blendShapes.Value;
                }
            }
            return defaultBlendShapes.Value;
        }
    }

    public void Dispose()
    {
        if (!Disposed)
        {
            defaultBlendShapes.Dispose();
            foreach (var presetDefaultBlendShapes in presetDefaultBlendShapes.Value.Values)
            {
                presetDefaultBlendShapes?.Dispose();
            }
            presetDefaultBlendShapes.Dispose();
            presetComponents.Dispose();
            Disposed = true;
        }
    }
}