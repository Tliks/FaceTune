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
    
    /* // 仕様が固まるまで公開しない
    [SerializeField] private float cancelerEntryDurationSeconds;
    public float CancelerEntryDurationSeconds { get => cancelerEntryDurationSeconds; init => cancelerEntryDurationSeconds = value; }
    public const string CancelerEntryDurationSecondsPropName = nameof(cancelerEntryDurationSeconds);

    [SerializeField] private float cancelerExitDurationSeconds;
    public float CancelerExitDurationSeconds { get => cancelerExitDurationSeconds; init => cancelerExitDurationSeconds = value; }
    public const string CancelerExitDurationSecondsPropName = nameof(cancelerExitDurationSeconds);
    */

    /*
    [SerializeField] private float cancelerThreshold;
    public float CancelerThreshold { get => cancelerThreshold; init => cancelerThreshold = value; }
    public const string CancelerThresholdPropName = nameof(cancelerThreshold);
    */

    public AdvancedLipSyncSettings() : this(true)
    {
    }

    public AdvancedLipSyncSettings(bool useAdvancedLipSync)
    {
        this.useAdvancedLipSync = useAdvancedLipSync;
        useCanceler = false;
        cancelerBlendShapeNames = new();
    }

    public AdvancedLipSyncSettings(
        bool useAdvancedLipSync,
        bool useCanceler,
        List<string> cancelerBlendShapeNames
    )
    {
        this.useAdvancedLipSync = useAdvancedLipSync;
        this.useCanceler = useCanceler;
        this.cancelerBlendShapeNames = cancelerBlendShapeNames;
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
         && CancelerBlendShapeNames.SequenceEqual(other.CancelerBlendShapeNames);
    }
    public override int GetHashCode()
    {
        var hash = UseAdvancedLipSync.GetHashCode();
        hash ^= UseCanceler.GetHashCode();
        hash ^= CancelerBlendShapeNames.GetSequenceHashCode();
        return hash;
    }
}