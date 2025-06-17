namespace com.aoyon.facetune;

internal record SessionContext
{
    public GameObject Root { get; }
    public SkinnedMeshRenderer FaceRenderer { get; }
    public DefaultExpressionContext DEC { get; }

    public Mesh FaceMesh { get; }
    public string BodyPath { get; }

    public SessionContext(
        GameObject root,
        SkinnedMeshRenderer faceRenderer,
        Mesh faceMesh,
        string bodyPath,
        DefaultExpressionContext dec
    )
    {
        Root = root;
        FaceRenderer = faceRenderer;
        FaceMesh = faceMesh;
        BodyPath = bodyPath;
        DEC = dec;
    }
}

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