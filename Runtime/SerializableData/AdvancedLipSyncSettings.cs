namespace aoyon.facetune;

[Serializable]
public record class AdvancedLipSyncSettings // Immutable
{
    [SerializeField] private bool useAdvancedLipSync;
    public bool UseAdvancedLipSync { get => useAdvancedLipSync; init => useAdvancedLipSync = value; }
    public const string UseAdvancedLipSyncPropName = nameof(useAdvancedLipSync);

    /*
    [SerializeField] private bool useAnimation;
    public bool UseAnimation { get => useAnimation; init => useAnimation = value; }
    public const string UseAnimationPropName = nameof(useAnimation);

    [SerializeField] private float weight;
    public float Weight { get => weight; init => weight = value; }
    public const string WeightPropName = nameof(weight);
    */

    [SerializeField] private bool useCanceler;
    public bool UseCanceler { get => useCanceler; init => useCanceler = value; }
    public const string UseCancelerPropName = nameof(useCanceler);

    [SerializeField] private List<string> cancelerBlendShapeNames;
    public IReadOnlyList<string> CancelerBlendShapeNames { get => cancelerBlendShapeNames.AsReadOnly(); init => cancelerBlendShapeNames = value.ToList(); }
    public const string CancelerBlendShapeNamesPropName = nameof(cancelerBlendShapeNames);

    [SerializeField] private float cancelerDurationSeconds;
    public float CancelerDurationSeconds { get => cancelerDurationSeconds; init => cancelerDurationSeconds = value; }
    public const string CancelerDurationSecondsPropName = nameof(cancelerDurationSeconds);

    public AdvancedLipSyncSettings() : this(true)
    {
    }

    public AdvancedLipSyncSettings(bool useAdvancedLipSync)
    {
        this.useAdvancedLipSync = useAdvancedLipSync;
        useCanceler = false;
        cancelerBlendShapeNames = new();
        cancelerDurationSeconds = 0.1f;
    }

    public AdvancedLipSyncSettings(
        bool useAdvancedLipSync,
        bool useCanceler,
        List<string> cancelerBlendShapeNames,
        float cancelerDurationSeconds
    )
    {
        this.useAdvancedLipSync = useAdvancedLipSync;
        this.useCanceler = useCanceler;
        this.cancelerBlendShapeNames = cancelerBlendShapeNames;
        this.cancelerDurationSeconds = cancelerDurationSeconds;
    }

    internal static AdvancedLipSyncSettings Disabled() => new(false);

    internal bool IsEnabled() => useAdvancedLipSync;
    internal bool IsCancelerEnabled() => IsEnabled() && useCanceler && cancelerBlendShapeNames.Count > 0;

    public virtual bool Equals(AdvancedLipSyncSettings other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return UseAdvancedLipSync == other.UseAdvancedLipSync
         && UseCanceler == other.UseCanceler
         && CancelerBlendShapeNames.SequenceEqual(other.CancelerBlendShapeNames)
         && CancelerDurationSeconds == other.CancelerDurationSeconds;
    }
    public override int GetHashCode()
    {
        var hash = UseAdvancedLipSync.GetHashCode();
        hash ^= UseCanceler.GetHashCode();
        hash ^= CancelerBlendShapeNames.GetSequenceHashCode();
        hash ^= CancelerDurationSeconds.GetHashCode();
        return hash;
    }
}