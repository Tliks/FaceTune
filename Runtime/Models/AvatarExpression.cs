namespace Aoyon.FaceTune;

internal class AvatarExpression : IEquatable<AvatarExpression> // 可変
{
    public string Name { get; private set; }
    public AnimationSet AnimationSet;
    
    private ExpressionSettings _expressionSettings;
    public ExpressionSettings ExpressionSettings { get => _expressionSettings; private set => _expressionSettings = value; }
    private FacialSettings _facialSettings;
    public FacialSettings FacialSettings { get => _facialSettings; private set => _facialSettings = value; }


    public AvatarExpression(string name, IEnumerable<GenericAnimation> animations, ExpressionSettings expressionSettings, FacialSettings? settings = null)
    {
        Name = name;
        AnimationSet = new AnimationSet(animations);
        _expressionSettings = expressionSettings;
        _facialSettings = settings ?? FacialSettings.Keep;
    }
    
    public void MergeExpression(AvatarExpression other)
    {
        MergeAnimation(other.AnimationSet);
        MergeExpressionSettings(other.ExpressionSettings);
        MergeFacialSettings(other.FacialSettings);
    }
    public void MergeAnimation(IEnumerable<GenericAnimation> others) => AnimationSet.MergeAnimation(others);
    public void MergeExpressionSettings(ExpressionSettings other) => _expressionSettings = _expressionSettings.Merge(other);
    public void MergeFacialSettings(FacialSettings other) => _facialSettings = _facialSettings.Merge(other);

    public bool Equals(AvatarExpression other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name && AnimationSet.SequenceEqual(other.AnimationSet) && FacialSettings == other.FacialSettings;
    }

    public override bool Equals(object? obj)
    {
        return obj is AvatarExpression expression && Equals(expression);
    }

    public override int GetHashCode()
    {
        var hash = Name.GetHashCode();
        hash ^= AnimationSet.GetSequenceHashCode();
        hash ^= FacialSettings.GetHashCode();
        hash ^= ExpressionSettings.GetHashCode();
        return hash;
    }
}