namespace aoyon.facetune;

[Serializable]
public record class AdvancedLipSyncSettings // Immutable
{
    [SerializeField] private bool useAdvancedLipSync;
    public bool UseAdvancedLipSync { get => useAdvancedLipSync; init => useAdvancedLipSync = value; }
    public const string UseAdvancedLipSyncPropName = nameof(useAdvancedLipSync);

    [SerializeField] private bool useAnimation;
    public bool UseAnimation { get => useAnimation; init => useAnimation = value; }
    public const string UseAnimationPropName = nameof(useAnimation);

    [SerializeField] private float weight;
    public float Weight { get => weight; init => weight = value; }
    public const string WeightPropName = nameof(weight);

    [SerializeField] private bool useCanceler;
    public bool UseCanceler { get => useCanceler; init => useCanceler = value; }
    public const string UseCancelerPropName = nameof(useCanceler);

    [SerializeField] private List<string> cancelerBlendShapeNames;
    public IReadOnlyList<string> CancelerBlendShapeNames { get => cancelerBlendShapeNames.AsReadOnly(); init => cancelerBlendShapeNames = value.ToList(); }
    public const string CancelerBlendShapeNamesPropName = nameof(cancelerBlendShapeNames);

    public AdvancedLipSyncSettings() : this(true)
    {
    }

    public AdvancedLipSyncSettings(bool useAdvancedLipSync)
    {
        this.useAdvancedLipSync = useAdvancedLipSync;
        useAnimation = true;
        weight = 1.0f;
        useCanceler = false;
        cancelerBlendShapeNames = new();
    }

    public AdvancedLipSyncSettings(
        bool useAdvancedLipSync,
        bool useAnimation,
        float weight,
        bool useCanceler,
        List<string> cancelerBlendShapeNames
    )
    {
        this.useAdvancedLipSync = useAdvancedLipSync;
        this.useAnimation = useAnimation;
        this.weight = weight;
        this.useCanceler = useCanceler;
        this.cancelerBlendShapeNames = cancelerBlendShapeNames;
    }

    internal static AdvancedLipSyncSettings Disabled() => new(false);

    internal bool IsEnabled() => useAdvancedLipSync && useAnimation;
    internal bool IsCancelerEnabled() => IsEnabled() && useCanceler && cancelerBlendShapeNames.Count > 0;

    public virtual bool Equals(AdvancedLipSyncSettings other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return UseAdvancedLipSync == other.UseAdvancedLipSync
         && UseAnimation == other.UseAnimation
         && Weight == other.Weight
         && UseCanceler == other.UseCanceler
         && CancelerBlendShapeNames.SequenceEqual(other.CancelerBlendShapeNames);
    }

    public override int GetHashCode()
    {
        var hash = UseAdvancedLipSync.GetHashCode();
        hash ^= UseAnimation.GetHashCode();
        hash ^= Weight.GetHashCode();
        hash ^= UseCanceler.GetHashCode();
        foreach (var name in CancelerBlendShapeNames)
        {
            hash ^= name.GetHashCode();
        }
        return hash;
    }
}