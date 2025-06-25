namespace com.aoyon.facetune;

[Serializable]
public record class FacialSettings // Immutable
{
    [SerializeField] private TrackingPermission allowEyeBlink;
    public TrackingPermission AllowEyeBlink { get => allowEyeBlink; init => allowEyeBlink = value; }
    public const string AllowEyeBlinkPropName = nameof(allowEyeBlink);

    [SerializeField] private TrackingPermission allowLipSync;
    public TrackingPermission AllowLipSync { get => allowLipSync; init => allowLipSync = value; }
    public const string AllowLipSyncPropName = nameof(allowLipSync);
    
    [SerializeField] private bool enableBlending;
    public bool EnableBlending { get => enableBlending; init => enableBlending = value; }
    public const string EnableBlendingPropName = nameof(enableBlending);
    
    public FacialSettings()
    {
        allowEyeBlink = TrackingPermission.Disallow;
        allowLipSync = TrackingPermission.Allow;
        enableBlending = false;
    }

    public FacialSettings(TrackingPermission allowEyeBlink, TrackingPermission allowLipSync, bool enableBlending)
    {
        this.allowEyeBlink = allowEyeBlink;
        this.allowLipSync = allowLipSync;
        this.enableBlending = enableBlending;
    }

    internal static FacialSettings Keep = new(TrackingPermission.Keep, TrackingPermission.Keep, true);

    internal FacialSettings Merge(FacialSettings other)
    {
        return new FacialSettings(
            allowEyeBlink == TrackingPermission.Keep ? other.allowEyeBlink : allowEyeBlink,
            allowLipSync == TrackingPermission.Keep ? other.allowLipSync : allowLipSync,
            enableBlending == other.enableBlending);
    }

    public virtual bool Equals(FacialSettings other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return allowEyeBlink == other.allowEyeBlink && allowLipSync == other.allowLipSync && enableBlending == other.enableBlending;
    }
    public override int GetHashCode()
    {
        return allowEyeBlink.GetHashCode() ^ allowLipSync.GetHashCode() ^ enableBlending.GetHashCode();
    }
}