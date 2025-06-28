namespace com.aoyon.facetune;

[Serializable]
public record class AdvancedEyBlinkSettings // Immutable
{
    [SerializeField] private bool useAdvancedEyBlink = true;
    public bool UseAdvancedEyBlink { get => useAdvancedEyBlink; init => useAdvancedEyBlink = value; }
    public const string UseAdvancedEyBlinkPropName = nameof(useAdvancedEyBlink);

    [SerializeField] private AdvancedEyBlinkMode mode = AdvancedEyBlinkMode.Animation;
    public AdvancedEyBlinkMode Mode { get => mode; init => mode = value; }
    public const string ModePropName = nameof(mode);

    // Animation
    [SerializeField] private List<BlendShapeAnimation> blendShapeAnimations = new();
    public IReadOnlyList<BlendShapeAnimation> BlendShapeAnimations { get => blendShapeAnimations.AsReadOnly(); init => blendShapeAnimations = value.ToList(); }
    public const string BlendShapeAnimationsPropName = nameof(blendShapeAnimations);

    // SmartEyeBlink
    [SerializeField] private List<string> eyeBlendShapeNames = new();
    public IReadOnlyList<string> EyeBlendShapeNames { get => eyeBlendShapeNames.AsReadOnly(); init => eyeBlendShapeNames = value.ToList(); }
    public const string EyeBlendShapeNamesPropName = nameof(eyeBlendShapeNames);


    public AdvancedEyBlinkSettings()
    {
    }

    internal static AdvancedEyBlinkSettings Default() => new();
    internal static AdvancedEyBlinkSettings Animation(List<BlendShapeAnimation> blendShapeAnimations)
    {
        return Default() with { Mode = AdvancedEyBlinkMode.Animation, BlendShapeAnimations = blendShapeAnimations };
    }
    internal static AdvancedEyBlinkSettings SmartEyeBlink(List<string> eyeBlendShapeNames)
    {
        return Default() with { Mode = AdvancedEyBlinkMode.SmartEyeBlink, EyeBlendShapeNames = eyeBlendShapeNames };
    }
    internal static AdvancedEyBlinkSettings Disabled() => Default() with { UseAdvancedEyBlink = false };


    public virtual bool Equals(AdvancedEyBlinkSettings other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return UseAdvancedEyBlink == other.UseAdvancedEyBlink
         && Mode == other.Mode
         && BlendShapeAnimations.SequenceEqual(other.BlendShapeAnimations)
         && EyeBlendShapeNames.SequenceEqual(other.EyeBlendShapeNames);
    }
    
    public override int GetHashCode()
    {
        var hash = UseAdvancedEyBlink.GetHashCode() ^ Mode.GetHashCode();
        foreach (var animation in BlendShapeAnimations)
        {
            hash ^= animation.GetHashCode();
        }
        foreach (var name in EyeBlendShapeNames)
        {
            hash ^= name.GetHashCode();
        }
        return hash;
    }
}                                                                                                                                                                                                                                                                                                                                                                                            