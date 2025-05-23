namespace com.aoyon.facetune;

internal record class ExpressionWithCondition
{
    public List<Condition> Conditions { get; private set; }
    public List<Expression> Expressions { get; private set; }

    public ExpressionWithCondition(List<Condition> conditions, List<Expression> expressions)
    {
        Conditions = conditions;
        Expressions = expressions;
    }
}

internal record class ExpressionPattern
{
    public List<ExpressionWithCondition> ExpressionWithConditions { get; private set; }

    public ExpressionPattern(List<ExpressionWithCondition> expressionWithConditions)
    {
        ExpressionWithConditions = expressionWithConditions;
    }

    public void Merge(ExpressionPattern other)
    {
        ExpressionWithConditions.AddRange(other.ExpressionWithConditions);
    }
}

internal record class Preset
{
    public string PresetName { get; private set; }
    public List<ExpressionPattern> Patterns { get; private set; }

    public Preset(string presetName, List<ExpressionPattern> patterns)
    {
        PresetName = presetName;
        Patterns = patterns;
    }
}

internal record class PresetData
{
    public List<Preset> Presets { get; private set; }

    public PresetData(List<Preset> presets)
    {
        Presets = presets;
    }

    public IEnumerable<Expression> GetAllExpressions()
    {
        return Presets.SelectMany(p => p.Patterns.SelectMany(p => p.ExpressionWithConditions.SelectMany(e => e.Expressions)));
    }
}
