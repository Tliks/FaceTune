namespace com.aoyon.facetune;

internal struct ExpressionWithCondition
{
    public List<Condition> Conditions;
    public List<Expression> Expressions;

    public ExpressionWithCondition(List<Condition> conditions, List<Expression> expressions)
    {
        Conditions = conditions;
        Expressions = expressions;
    }
}

internal struct ExpressionPattern
{
    public List<ExpressionWithCondition> Expressions;

    public ExpressionPattern(List<ExpressionWithCondition> expressions)
    {
        Expressions = expressions;
    }

    public void Merge(ExpressionPattern other)
    {
        Expressions.AddRange(other.Expressions);
    }
}

internal readonly struct SortedExpressionPatterns
{
    private readonly List<(ExpressionPattern Pattern, int Priority)> _patternsWithPriority;

    public SortedExpressionPatterns(List<(ExpressionPattern Pattern, int Priority)> patternsWithPriority)
    {
        _patternsWithPriority = new();
        foreach (var (pattern, priority) in patternsWithPriority)
        {
            Add(pattern, priority);
        }
    }

    public readonly void Add(ExpressionPattern pattern, int priority)
    {
        if (_patternsWithPriority.TryGetFirst(x => x.Priority == priority, out var existingPattern))
        {
            existingPattern.Pattern.Merge(pattern);
        }
        else
        {
            _patternsWithPriority.Add((pattern, priority));
        }
    }

    public readonly List<ExpressionPattern> GetPatternsInPriorityOrder()
    {
        return _patternsWithPriority.OrderBy(x => x.Priority).Select(x => x.Pattern).ToList();
    }
}

internal struct Preset
{
    public PresetInfo Info;
    public SortedExpressionPatterns SortedExpressionPatterns;

    public Preset(PresetInfo info, SortedExpressionPatterns sortedExpressionPatterns)
    {
        Info = info;
        SortedExpressionPatterns = sortedExpressionPatterns;
    }
}