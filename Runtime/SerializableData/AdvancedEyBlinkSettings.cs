namespace com.aoyon.facetune;

[Serializable]
public record class AdvancedEyBlinkSettings // Immutable
{
    [SerializeField] private bool useAdvancedEyBlink = true;
    public bool UseAdvancedEyBlink { get => useAdvancedEyBlink; init => useAdvancedEyBlink = value; }
    public const string UseAdvancedEyBlinkPropName = nameof(useAdvancedEyBlink);

    [SerializeField] private bool useAnimation = true;
    public bool UseAnimation { get => useAnimation; init => useAnimation = value; }
    public const string UseAnimationPropName = nameof(useAnimation);

    [SerializeField] private List<BlendShapeAnimation> blinkAnimations = new();
    public IReadOnlyList<BlendShapeAnimation> BlinkAnimations { get => blinkAnimations.AsReadOnly(); init => blinkAnimations = value.ToList(); }
    public const string BlinkAnimationsPropName = nameof(blinkAnimations);
    
    [SerializeField] private bool useCanceler = false;
    public bool UseCanceler { get => useCanceler; init => useCanceler = value; }
    public const string UseCancelerPropName = nameof(useCanceler);

    [SerializeField] private List<string> cancelerBlendShapeNames = new();
    public IReadOnlyList<string> CancelerBlendShapeNames { get => cancelerBlendShapeNames.AsReadOnly(); init => cancelerBlendShapeNames = value.ToList(); }
    public const string CancelerBlendShapeNamesPropName = nameof(cancelerBlendShapeNames);


    public AdvancedEyBlinkSettings()
    {
    }

    internal static AdvancedEyBlinkSettings Default() => new();
    internal static AdvancedEyBlinkSettings Animation(List<BlendShapeAnimation> blinkAnimations)
    {
        return Default() with { UseAnimation = true, BlinkAnimations = blinkAnimations };
    }
    internal static AdvancedEyBlinkSettings AnimationWithCanceler(List<BlendShapeAnimation> blinkAnimations, List<string> cancelerBlendShapeNames)
    {
        return Default() with { UseCanceler = true, BlinkAnimations = blinkAnimations, CancelerBlendShapeNames = cancelerBlendShapeNames };
    }
    internal static AdvancedEyBlinkSettings Disabled() => Default() with { UseAdvancedEyBlink = false };


    public virtual bool Equals(AdvancedEyBlinkSettings other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return UseAdvancedEyBlink == other.UseAdvancedEyBlink
         && UseAnimation == other.UseAnimation
         && BlinkAnimations.SequenceEqual(other.BlinkAnimations)
         && UseCanceler == other.UseCanceler
         && CancelerBlendShapeNames.SequenceEqual(other.CancelerBlendShapeNames);
    }
    
    public override int GetHashCode()
    {
        var hash = UseAdvancedEyBlink.GetHashCode() ^ UseAnimation.GetHashCode();
        foreach (var animation in BlinkAnimations)
        {
            hash ^= animation.GetHashCode();
        }
        hash ^= UseCanceler.GetHashCode();
        foreach (var name in CancelerBlendShapeNames)
        {
            hash ^= name.GetHashCode();
        }
        return hash;
    }
}                                                                                                                                                                                                                                                                                                                                                                                            