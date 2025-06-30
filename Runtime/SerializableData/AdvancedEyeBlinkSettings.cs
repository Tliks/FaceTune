namespace aoyon.facetune;

[Serializable]
public record class AdvancedEyeBlinkSettings // Immutable
{
    [SerializeField] private bool useAdvancedEyeBlink;
    public bool UseAdvancedEyeBlink { get => useAdvancedEyeBlink; init => useAdvancedEyeBlink = value; }
    public const string UseAdvancedEyeBlinkPropName = nameof(useAdvancedEyeBlink);

    [SerializeField] private bool useAnimation;
    public bool UseAnimation { get => useAnimation; init => useAnimation = value; }
    public const string UseAnimationPropName = nameof(useAnimation);

    [SerializeField] private float intervalSeconds;
    public float IntervalSeconds { get => intervalSeconds; init => intervalSeconds = value; }
    public const string IntervalSecondsPropName = nameof(intervalSeconds);

    [SerializeField] private bool useRandomInterval;
    public bool UseRandomInterval { get => useRandomInterval; init => useRandomInterval = value; }
    public const string UseRandomIntervalPropName = nameof(useRandomInterval);

    [SerializeField] private float randomIntervalMinSeconds;
    public float RandomIntervalMinSeconds { get => randomIntervalMinSeconds; init => randomIntervalMinSeconds = value; }
    public const string RandomIntervalMinSecondsPropName = nameof(randomIntervalMinSeconds);

    [SerializeField] private float randomIntervalMaxSeconds;
    public float RandomIntervalMaxSeconds { get => randomIntervalMaxSeconds; init => randomIntervalMaxSeconds = value; }
    public const string RandomIntervalMaxSecondsPropName = nameof(randomIntervalMaxSeconds);

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


    public AdvancedEyeBlinkSettings() : this(true)
    {
    }

    private const string BlinkParam = "vrc.blink"; // Todo: デフォルトとするか、Editor上の追加とするか考える 
    public AdvancedEyeBlinkSettings(bool useAdvancedEyeBlink)
    {
        this.useAdvancedEyeBlink = useAdvancedEyeBlink;
        useAnimation = true;
        intervalSeconds = 10.0f;
        useRandomInterval = true;
        randomIntervalMinSeconds = 4.0f;
        randomIntervalMaxSeconds = 20.0f;

        var closeCurve = new AnimationCurve();
        closeCurve.AddKey(0f, 0f);
        closeCurve.AddKey(0.05f, 100f);
        closeAnimations = new(){new (BlinkParam, closeCurve)};

        var openCurve = new AnimationCurve();
        openCurve.AddKey(0f, 100f);
        openCurve.AddKey(0.05f, 0f);
        openAnimations = new(){new (BlinkParam, openCurve)};

        useCanceler = false;
        cancelerBlendShapeNames = new();
    }

    public AdvancedEyeBlinkSettings(
        bool useAdvancedEyeBlink,
        bool useAnimation,
        float intervalSeconds,
        bool useRandomInterval,
        float randomIntervalMinSeconds,
        float randomIntervalMaxSeconds,
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
        this.randomIntervalMaxSeconds = randomIntervalMaxSeconds;
        this.closeAnimations = closeAnimations;
        this.openAnimations = openAnimations;
        this.useCanceler = useCanceler;
        this.cancelerBlendShapeNames = cancelerBlendShapeNames;
    }

    internal static AdvancedEyeBlinkSettings Disabled() => new(false);

    internal bool IsEnabled() => useAdvancedEyeBlink && useAnimation;
    internal bool IsCancelerEnabled() => IsEnabled() && useCanceler && cancelerBlendShapeNames.Count > 0;

    internal AdvancedEyeBlinkSettings GetRenamed(Dictionary<string, string> mapping)
    {
        var closeAnimations = new List<BlendShapeAnimation>();
        foreach (var animation in CloseAnimations)
        {
            if (mapping.TryGetValue(animation.Name, out var newName))
            {
                closeAnimations.Add(animation with { Name = newName });
            }
            else
            {
                closeAnimations.Add(animation);
            }
        }
        var openAnimations = new List<BlendShapeAnimation>();
        foreach (var animation in OpenAnimations)
        {
            if (mapping.TryGetValue(animation.Name, out var newName))
            {
                openAnimations.Add(animation with { Name = newName });
            }   
            else
            {
                openAnimations.Add(animation);
            }
        }
        return this with { CloseAnimations = closeAnimations, OpenAnimations = openAnimations };
    }

    public virtual bool Equals(AdvancedEyeBlinkSettings other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return UseAdvancedEyeBlink == other.UseAdvancedEyeBlink
         && UseAnimation == other.UseAnimation
         && IntervalSeconds == other.IntervalSeconds
         && UseRandomInterval == other.UseRandomInterval
         && RandomIntervalMinSeconds == other.RandomIntervalMinSeconds
         && RandomIntervalMaxSeconds == other.RandomIntervalMaxSeconds
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
        hash ^= RandomIntervalMaxSeconds.GetHashCode();
        hash ^= CloseAnimations.GetSequenceHashCode();
        hash ^= OpenAnimations.GetSequenceHashCode();
        hash ^= UseCanceler.GetHashCode();
        hash ^= CancelerBlendShapeNames.GetSequenceHashCode();
        return hash;
    }
}                                                                                                                                                                                                                                                                                                                                                                                            