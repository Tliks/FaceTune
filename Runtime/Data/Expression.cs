namespace aoyon.facetune;

internal class Expression : IEquatable<Expression> // 可変
{
    public string Name { get; private set; }
    public IReadOnlyList<GenericAnimation> Animations
    {
        get => _animationIndex.Animations;
        private set => _animationIndex = new AnimationIndex(value);
    }
    private ExpressionSettings _expressionSettings;
    public ExpressionSettings ExpressionSettings { get => _expressionSettings; private set => _expressionSettings = value; }
    private FacialSettings _facialSettings;
    public FacialSettings FacialSettings { get => _facialSettings; private set => _facialSettings = value; }

    private AnimationIndex _animationIndex;
    public AnimationIndex AnimationIndex => _animationIndex;

    public Expression(string name, IEnumerable<GenericAnimation> animations, ExpressionSettings expressionSettings, FacialSettings? settings = null)
    {
        Name = name;
        _animationIndex = new AnimationIndex(animations);
        _expressionSettings = expressionSettings;
        _facialSettings = settings ?? FacialSettings.Keep;
    }
    
    public void MergeExpression(Expression other)
    {
        MergeAnimation(other.Animations);
        MergeExpressionSettings(other.ExpressionSettings);
        MergeFacialSettings(other.FacialSettings);
    }
    public void MergeAnimation(IEnumerable<GenericAnimation> others) => _animationIndex.MergeAnimation(others);
    public void MergeExpressionSettings(ExpressionSettings other) => _expressionSettings = _expressionSettings.Merge(other);
    public void MergeFacialSettings(FacialSettings other) => _facialSettings = _facialSettings.Merge(other);

    public bool Equals(Expression other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name && Animations.SequenceEqual(other.Animations) && FacialSettings == other.FacialSettings;
    }

    public override bool Equals(object? obj)
    {
        return obj is Expression expression && Equals(expression);
    }

    public override int GetHashCode()
    {
        var hash = Name.GetHashCode();
        
        foreach (var animation in Animations)
        {
            hash ^= animation.GetHashCode();
        }
        
        hash ^= FacialSettings.GetHashCode();
        hash ^= ExpressionSettings.GetHashCode();
        
        return hash;
    }
}