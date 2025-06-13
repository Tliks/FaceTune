namespace com.aoyon.facetune;

internal class Expression : IEqualityComparer<Expression>
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

    public bool Equals(Expression x, Expression y)
    {
        return x.Name == y.Name && x.Animations.SequenceEqual(y.Animations) && x.FacialSettings == y.FacialSettings;
    }

    public int GetHashCode(Expression obj)
    {
        return Name.GetHashCode() ^ Animations.GetHashCode() ^ (FacialSettings?.GetHashCode() ?? 0);
    }
}