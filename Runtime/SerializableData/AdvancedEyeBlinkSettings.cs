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

    [SerializeField] private List<string> blinkBlendShapeNames;
    public IReadOnlyList<string> BlinkBlendShapeNames { get => blinkBlendShapeNames.AsReadOnly(); init => blinkBlendShapeNames = value.ToList(); }
    public const string BlinkBlendShapeNamesPropName = nameof(blinkBlendShapeNames);

    [SerializeField] private float blinkDurationSeconds;
    public float BlinkDurationSeconds { get => blinkDurationSeconds; init => blinkDurationSeconds = value; }
    public const string BlinkDurationSecondsPropName = nameof(blinkDurationSeconds);

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
        blinkBlendShapeNames = new(){ BlinkParam };
        blinkDurationSeconds = 0.05f;
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
        List<string> blinkBlendShapeNames,
        float blinkDurationSeconds,
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
        this.blinkBlendShapeNames = blinkBlendShapeNames;
        this.blinkDurationSeconds = blinkDurationSeconds;
        this.useCanceler = useCanceler;
        this.cancelerBlendShapeNames = cancelerBlendShapeNames;
    }

    internal static AdvancedEyeBlinkSettings Disabled() => new(false);

    internal bool IsEnabled() => useAdvancedEyeBlink; // useAnimatiomが有効でない場合現状意味はない
    internal bool IsAnimationEnabled() => IsEnabled() && useAnimation; 
    internal bool IsCancelerEnabled() => IsAnimationEnabled() && useCanceler && cancelerBlendShapeNames.Count > 0;

    internal AdvancedEyeBlinkSettings GetRenamed(Dictionary<string, string> mapping)
    {
        return this with { BlinkBlendShapeNames = blinkBlendShapeNames.Select(name => mapping.ContainsKey(name) ? mapping[name] : name).ToList() };
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
         && BlinkBlendShapeNames.SequenceEqual(other.BlinkBlendShapeNames)
         && BlinkDurationSeconds == other.BlinkDurationSeconds
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
        hash ^= BlinkBlendShapeNames.GetSequenceHashCode();
        hash ^= BlinkDurationSeconds.GetHashCode();
        hash ^= UseCanceler.GetHashCode();
        hash ^= CancelerBlendShapeNames.GetSequenceHashCode();
        return hash;
    }
}                                                                                                                                                                                                                                                                                                                                                                                            