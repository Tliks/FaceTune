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
    
    [SerializeField] private BlendingPermission _blendingPermission;
    public const string BlendingPermissionPropName = "_blendingPermission";
    public BlendingPermission BlendingPermission { get => _blendingPermission; init => _blendingPermission = value; }
    
    public FacialSettings()
    {
        _allowEyeBlink = TrackingPermission.Disallow;
        _allowLipSync = TrackingPermission.Allow;
        _blendingPermission = BlendingPermission.Disallow;
    }

    public FacialSettings(TrackingPermission allowEyeBlink, TrackingPermission allowLipSync, BlendingPermission blendingPermission)
    {
        _allowEyeBlink = allowEyeBlink;
        _allowLipSync = allowLipSync;
        _blendingPermission = blendingPermission;
    }

    internal static FacialSettings Keep = new(TrackingPermission.Keep, TrackingPermission.Keep, BlendingPermission.Keep);

    internal FacialSettings Merge(FacialSettings other)
    {
        return new FacialSettings(
            _allowEyeBlink == TrackingPermission.Keep ? other._allowEyeBlink : _allowEyeBlink,
            _allowLipSync == TrackingPermission.Keep ? other._allowLipSync : _allowLipSync,
            _blendingPermission == BlendingPermission.Keep ? other._blendingPermission : _blendingPermission);
    }

    public virtual bool Equals(FacialSettings other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _allowEyeBlink == other._allowEyeBlink && _allowLipSync == other._allowLipSync && _blendingPermission == other._blendingPermission;
    }
    public override int GetHashCode()
    {
        return _allowEyeBlink.GetHashCode() ^ _allowLipSync.GetHashCode() ^ _blendingPermission.GetHashCode();
    }
}