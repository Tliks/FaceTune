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

internal record class SortedExpressionPatterns
{
    public List<ExpressionPattern?> Patterns { get; private set; } = new();

    public SortedExpressionPatterns(IEnumerable<(ExpressionPattern pattern, int priority)> patterns)
    {
        foreach (var (pattern, priority) in patterns)
        {
            Add(pattern, priority);
        }
    }

    public void Add(ExpressionPattern pattern, int priority)
    {
        while (Patterns.Count <= priority)
        {
            Patterns.Add(null);
        }

        if (Patterns[priority] != null)
        {
            Patterns[priority]!.Merge(pattern);
        }
        else
        {
            Patterns[priority] = pattern;
        }
    }
}

internal record class Preset
{
    public string PresetName { get; private set; }
    public SortedExpressionPatterns SortedPatterns { get; private set; }

    public Preset(string presetName, SortedExpressionPatterns sortedExpressionPatterns)
    {
        PresetName = presetName;
        SortedPatterns = sortedExpressionPatterns;
    }

    public IEnumerable<Expression> GetAllExpressions()
    {
        return SortedPatterns.Patterns
            .OfType<ExpressionPattern>()
            .SelectMany(p => p.ExpressionWithConditions)
            .SelectMany(e => e.Expressions);
    }
}