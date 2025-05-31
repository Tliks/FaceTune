namespace com.aoyon.facetune;

internal record SessionContext
{
    public GameObject Root { get; }
    public SkinnedMeshRenderer FaceRenderer { get; }
    public Mesh FaceMesh { get; }

    public DefaultExpressionContext DEC { get; }

    public SessionContext(
        GameObject root,
        SkinnedMeshRenderer faceRenderer,
        Mesh faceMesh,
        DefaultExpressionContext dec
    )
    {
        Root = root;
        FaceRenderer = faceRenderer;
        FaceMesh = faceMesh;
        DEC = dec;
    }
}

internal record DefaultExpressionContext
{
    private readonly FacialExpression defaultExpression;
    private readonly Dictionary<PresetComponent, FacialExpression?> presetDefaultExpressions;
    private readonly HashSet<PresetComponent> presetComponents;

    public DefaultExpressionContext(FacialExpression defaultExpression, Dictionary<PresetComponent, FacialExpression?> presetDefaultExpressions)
    {
        this.defaultExpression = defaultExpression;
        this.presetDefaultExpressions = presetDefaultExpressions;
        presetComponents = presetDefaultExpressions.Keys.ToHashSet();
    }

    public FacialExpression GetGlobalDefaultExpression()
    {
        return defaultExpression;
    }

    public BlendShapeSet GetGlobalDefaultBlendShapeSet()
    {
        return defaultExpression.BlendShapeSet;
    }

    public FacialExpression GetPresetDefaultExpression(PresetComponent preset)
    {
        if (presetDefaultExpressions.TryGetValue(preset, out var expression) && expression != null)
        {
            return expression;
        }
        return defaultExpression;
    }

    public BlendShapeSet GetPresetDefaultBlendShapeSet(PresetComponent preset)
    {
        return GetPresetDefaultExpression(preset).BlendShapeSet;
    }

    public FacialExpression GetDefaultExpression(GameObject target)
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
        return GetDefaultExpression(target).BlendShapeSet;
    }

    public IEnumerable<FacialExpression> GetAllExpressions()
    {
        return presetDefaultExpressions.Values
            .Where(expr => expr != null)
            .Concat(new[] { defaultExpression })!;
    }
}