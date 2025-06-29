namespace aoyon.facetune;

[Serializable]
public record class AdvancedEyBlinkSettings // Immutable
{
    [SerializeField] private bool useAdvancedEyeBlink;
    public bool UseAdvancedEyeBlink { get => useAdvancedEyeBlink; init => useAdvancedEyeBlink = value; }
    public const string UseAdvancedEyeBlinkPropName = nameof(useAdvancedEyeBlink);

    [SerializeField] private bool useAnimation;
    public bool UseAnimation { get => useAnimation; init => useAnimation = value; }
    public const string UseAnimationPropName = nameof(useAnimation);

    [SerializeField] private float intervalSeconds; // randomIntervalMaxSecondsと兼用
    public float IntervalSeconds { get => intervalSeconds; init => intervalSeconds = value; }
    public const string IntervalSecondsPropName = nameof(intervalSeconds);

    [SerializeField] private bool useRandomInterval;
    public bool UseRandomInterval { get => useRandomInterval; init => useRandomInterval = value; }
    public const string UseRandomIntervalPropName = nameof(useRandomInterval);

    [SerializeField] private float randomIntervalMinSeconds;
    public float RandomIntervalMinSeconds { get => randomIntervalMinSeconds; init => randomIntervalMinSeconds = value; }
    public const string RandomIntervalMinSecondsPropName = nameof(randomIntervalMinSeconds);

    [SerializeField] private List<BlendShapeAnimation> closeAnimations;
    public IReadOnlyList<BlendShapeAnimation> CloseAnimations { get => closeAnimations.AsReadOnly(); init => closeAnimations = value.ToList(); }
    public const string CloseAnimationsPropName = nameof(closeAnimations);

    [SerializeField] private List<BlendShapeAnimation> openAnimations;
    public IReadOnlyList<BlendShapeAnimation> OpenAnimations { get => openAnimations.AsReadOnly(); init => openAnimations = value.ToList(); }
    public const string OpenAnimationsPropName = nameof(openAnimations);

    [SerializeField] private bool useCanceler;
    public bool UseCanceler { get => useCanceler; init => useCanceler = value; }
    public const string UseCancelerPropName = nameof(useCanceler);

    [SerializeField] private List<string> cancelerBlendShapeNames;
    public IReadOnlyList<string> CancelerBlendShapeNames { get => cancelerBlendShapeNames.AsReadOnly(); init => cancelerBlendShapeNames = value.ToList(); }
    public const string CancelerBlendShapeNamesPropName = nameof(cancelerBlendShapeNames);


    public AdvancedEyBlinkSettings()
    {
        useAdvancedEyeBlink = true;
        useAnimation = true;
        intervalSeconds = 4.0f;
        useRandomInterval = true;
        randomIntervalMinSeconds = 0.8f;
        closeAnimations = new();
        openAnimations = new();
        useCanceler = false;
        cancelerBlendShapeNames = new();
    }

    public AdvancedEyBlinkSettings(
        bool useAdvancedEyeBlink,
        bool useAnimation,
        float intervalSeconds,
        bool useRandomInterval,
        float randomIntervalMinSeconds,
        List<BlendShapeAnimation> closeAnimations,
        List<BlendShapeAnimation> openAnimations,
        bool useCanceler,
        List<string> cancelerBlendShapeNames
    )
    {
        this.useAdvancedEyeBlink = useAdvancedEyeBlink;
        this.useAnimation = useAnimation;
        this.intervalSeconds = intervalSeconds;
        this.useRandomInterval = useRandomInterval;
        this.randomIntervalMinSeconds = randomIntervalMinSeconds;
        this.closeAnimations = closeAnimations;
        this.openAnimations = openAnimations;
        this.useCanceler = useCanceler;
        this.cancelerBlendShapeNames = cancelerBlendShapeNames;
    }

    internal static AdvancedEyBlinkSettings Disabled() => new(false, false, 0.1f, false, 0.1f, new(), new(), false, new());

    public virtual bool Equals(AdvancedEyBlinkSettings other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return UseAdvancedEyeBlink == other.UseAdvancedEyeBlink
         && UseAnimation == other.UseAnimation
         && IntervalSeconds == other.IntervalSeconds
         && UseRandomInterval == other.UseRandomInterval
         && RandomIntervalMinSeconds == other.RandomIntervalMinSeconds
         && CloseAnimations.SequenceEqual(other.CloseAnimations)
         && OpenAnimations.SequenceEqual(other.OpenAnimations)
         && UseCanceler == other.UseCanceler
         && CancelerBlendShapeNames.SequenceEqual(other.CancelerBlendShapeNames);
    }
    
    public override int GetHashCode()
    {
        var hash = UseAdvancedEyeBlink.GetHashCode();
        hash ^= UseAnimation.GetHashCode();
        hash ^= IntervalSeconds.GetHashCode();
        hash ^= UseRandomInterval.GetHashCode();
        hash ^= RandomIntervalMinSeconds.GetHashCode();
        foreach (var closeAnimation in CloseAnimations)
        {
            hash ^= closeAnimation.GetHashCode();
        }
        foreach (var openAnimation in OpenAnimations)
        {
            hash ^= openAnimation.GetHashCode();
        }
        hash ^= UseCanceler.GetHashCode();
        foreach (var name in CancelerBlendShapeNames)
        {
            hash ^= name.GetHashCode();
        }
        return hash;
    }
}                                                                                                                                                                                                                                                                                                                                                                                            