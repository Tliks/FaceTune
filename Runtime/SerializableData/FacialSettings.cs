namespace com.aoyon.facetune;

[Serializable]
public record class FacialSettings // Immutable
{
    [SerializeField] private TrackingPermission _allowEyeBlink;
    public const string AllowEyeBlinkPropName = "_allowEyeBlink";
    public TrackingPermission AllowEyeBlink { get => _allowEyeBlink; init => _allowEyeBlink = value; }

    [SerializeField] private TrackingPermission _allowLipSync;
    public const string AllowLipSyncPropName = "_allowLipSync";
    public TrackingPermission AllowLipSync { get => _allowLipSync; init => _allowLipSync = value; }
    
    [SerializeField] private bool _enableBlending;
    public const string EnableBlendingPropName = "_enableBlending";
    public bool EnableBlending { get => _enableBlending; init => _enableBlending = value; }
    
    public FacialSettings()
    {
        _allowEyeBlink = TrackingPermission.Disallow;
        _allowLipSync = TrackingPermission.Allow;
        _enableBlending = false;
    }

    public FacialSettings(TrackingPermission allowEyeBlink, TrackingPermission allowLipSync, bool enableBlending)
    {
        _allowEyeBlink = allowEyeBlink;
        _allowLipSync = allowLipSync;
        _enableBlending = enableBlending;
    }

    internal static FacialSettings Keep = new(TrackingPermission.Keep, TrackingPermission.Keep, true);

    internal FacialSettings Merge(FacialSettings other)
    {
        return new FacialSettings(
            _allowEyeBlink == TrackingPermission.Keep ? other._allowEyeBlink : _allowEyeBlink,
            _allowLipSync == TrackingPermission.Keep ? other._allowLipSync : _allowLipSync,
            _enableBlending == other._enableBlending);
    }

    public virtual bool Equals(FacialSettings other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _allowEyeBlink == other._allowEyeBlink && _allowLipSync == other._allowLipSync && _enableBlending == other._enableBlending;
    }
    public override int GetHashCode()
    {
        return _allowEyeBlink.GetHashCode() ^ _allowLipSync.GetHashCode() ^ _enableBlending.GetHashCode();
    }
}