namespace com.aoyon.facetune;

internal class Expression : IEquatable<Expression>
{
    public string Name { get; private set; }
    public IReadOnlyList<GenericAnimation> Animations
    {
        get => _animationIndex.Animations;
        private set => _animationIndex = new AnimationIndex(value);
    }
    private FacialSettings? _facialSettings;
    public FacialSettings? FacialSettings { get => _facialSettings; private set => _facialSettings = value; }

    private AnimationIndex _animationIndex;
    public AnimationIndex AnimationIndex => _animationIndex;

    public Expression(string name, List<GenericAnimation> animations, FacialSettings? settings = null)
    {
        Name = name;
        _animationIndex = new AnimationIndex(animations);
        _facialSettings = settings;
    }
    
    public void MergeExpression(Expression other)
    {
        MergeAnimation(other.Animations);
        MergeFacialSettings(other.FacialSettings);
    }
    public void MergeAnimation(IEnumerable<GenericAnimation> others) => _animationIndex.MergeAnimation(others);
    public void MergeFacialSettings(FacialSettings? other)
    {
        if (other == null) return;
        
        if (_facialSettings == null) _facialSettings = FacialSettings.Keep;

        if (other.AllowEyeBlink != TrackingPermission.Keep)
        {
            _facialSettings.AllowEyeBlink = other.AllowEyeBlink;
        }
        if (other.AllowLipSync != TrackingPermission.Keep)
        {
            _facialSettings.AllowLipSync = other.AllowLipSync;
        }
        if (other.BlendingPermission != BlendingPermission.Keep)
        {
            _facialSettings.BlendingPermission = other.BlendingPermission;
        }
    }

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
        return Name.GetHashCode() ^ Animations.GetHashCode() ^ (FacialSettings?.GetHashCode() ?? 0);
    }
}