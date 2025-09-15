namespace Aoyon.FaceTune;

// Immutable

internal sealed class FloatCondition : IBaseCondition
{
    public string ParameterName { get; }
    public float Value { get; }
    public ComparisonType ComparisonType { get; }

    public FloatCondition(string parameterName, float value, ComparisonType comparisonType)
    {
        if (comparisonType != ComparisonType.GreaterThan && comparisonType != ComparisonType.LessThan)
        {
            throw new InvalidOperationException($"Invalid comparison type: {comparisonType}");
        }
        ParameterName = parameterName;
        Value = value;
        ComparisonType = comparisonType;
    }

    private const float FloatTolerance = 0.00001f;
    public ICondition ToNegation()
    {
        return ComparisonType switch
        {
            ComparisonType.GreaterThan => new FloatCondition(ParameterName, Value + FloatTolerance, ComparisonType.LessThan),
            ComparisonType.LessThan => new FloatCondition(ParameterName, Value - FloatTolerance, ComparisonType.GreaterThan),
            // EqualやNotEqualをここで弾く(既に弾いているはず)
            // なお別にAND条件で表現は可能ではある。
            // Todo: このハンドリングを変えるか考える。
            _ => throw new InvalidOperationException($"Invalid comparison type: {ComparisonType}"), 
        };
    }

    TResult ICondition.Accept<TResult>(IConditionVisitor<TResult> visitor)
    {
        return visitor.Visit(this);
    }
}

internal sealed class IntCondition : IBaseCondition
{
    public string ParameterName { get; }
    public int Value { get; }
    public ComparisonType ComparisonType { get; }

    public IntCondition(string parameterName, int value, ComparisonType comparisonType)
    {
        ParameterName = parameterName;
        Value = value;
        ComparisonType = comparisonType;
    }

    public ICondition ToNegation()
    {
        return ComparisonType switch
        {
            ComparisonType.Equal => new IntCondition(ParameterName, Value, ComparisonType.NotEqual),
            ComparisonType.NotEqual => new IntCondition(ParameterName, Value, ComparisonType.Equal),
            ComparisonType.GreaterThan => new IntCondition(ParameterName, Value - 1, ComparisonType.LessThan),
            ComparisonType.LessThan => new IntCondition(ParameterName, Value + 1, ComparisonType.GreaterThan),
            _ => throw new InvalidOperationException($"Invalid comparison type: {ComparisonType}"),
        };
    }

    TResult ICondition.Accept<TResult>(IConditionVisitor<TResult> visitor)
    {
        return visitor.Visit(this);
    }
}

internal sealed class BoolCondition : IBaseCondition
{
    public string ParameterName { get; }
    public bool Value { get; }

    public BoolCondition(string parameterName, bool value)
    {
        ParameterName = parameterName;
        Value = value;
    }

    public ICondition ToNegation()
    {
        return new BoolCondition(ParameterName, !Value);
    }

    TResult ICondition.Accept<TResult>(IConditionVisitor<TResult> visitor)
    {
        return visitor.Visit(this);
    }
}

internal sealed class TrueCondition : IBaseCondition
{
    public ICondition ToNegation() => new FalseCondition();
    TResult ICondition.Accept<TResult>(IConditionVisitor<TResult> visitor)
    {
        return visitor.Visit(this);
    }
}

internal sealed class FalseCondition : IBaseCondition
{
    public ICondition ToNegation() => new TrueCondition();
    TResult ICondition.Accept<TResult>(IConditionVisitor<TResult> visitor)
    {
        return visitor.Visit(this);
    }
}