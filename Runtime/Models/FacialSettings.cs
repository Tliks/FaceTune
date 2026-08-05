namespace Aoyon.FaceTune;

[Serializable]
internal record class FacialSettings // Immutable
{
    [SerializeField] private TrackingPermission allowEyeBlink;
    public TrackingPermission AllowEyeBlink { get => allowEyeBlink; init => allowEyeBlink = value; }
    public const string AllowEyeBlinkPropName = nameof(allowEyeBlink);

    [SerializeField] private TrackingPermission allowLipSync;
    public TrackingPermission AllowLipSync { get => allowLipSync; init => allowLipSync = value; }
    public const string AllowLipSyncPropName = nameof(allowLipSync);
    
    [SerializeField] private ExpressionWriteMode writeMode;
    public ExpressionWriteMode WriteMode { get => writeMode; init => writeMode = value; }
    public const string WriteModePropName = nameof(writeMode);

    [Obsolete("Only kept for migration. Use WriteMode.")]
    [SerializeField] private bool enableBlending = false;
    [Obsolete("Only kept for migration. Use WriteMode.")]
    public bool EnableBlending { get => enableBlending; init => enableBlending = value; }
    [Obsolete("Only kept for migration. Use WriteModePropName.")]
    public const string EnableBlendingPropName = nameof(enableBlending);

    internal EyeBlinkSettings EyeBlinkSettings { get; init; } = new();
    internal AdvancedLipSyncSettings AdvancedLipSyncSettings { get; init; } = AdvancedLipSyncSettings.Disabled();
    
    public FacialSettings() : this(TrackingPermission.Disallow, TrackingPermission.Allow, ExpressionWriteMode.Replace)
    {
    }

    public FacialSettings(TrackingPermission allowEyeBlink, TrackingPermission allowLipSync, ExpressionWriteMode writeMode)
    {
        this.allowEyeBlink = allowEyeBlink;
        this.allowLipSync = allowLipSync;
        this.writeMode = writeMode;
    }

    [Obsolete("Only kept for migration. Use the ExpressionWriteMode overload.")]
    public FacialSettings(TrackingPermission allowEyeBlink, TrackingPermission allowLipSync, bool enableBlending) : this(allowEyeBlink, allowLipSync, enableBlending ? ExpressionWriteMode.Blend : ExpressionWriteMode.Replace)
    {
    }

    internal static FacialSettings Keep = new(TrackingPermission.Keep, TrackingPermission.Keep, ExpressionWriteMode.Blend);

    public virtual bool Equals(FacialSettings other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return allowEyeBlink == other.allowEyeBlink
         && allowLipSync == other.allowLipSync
         && writeMode == other.writeMode
         && EyeBlinkSettings == other.EyeBlinkSettings
         && AdvancedLipSyncSettings == other.AdvancedLipSyncSettings;
    }
    public override int GetHashCode()
    {
        return allowEyeBlink.GetHashCode() 
        ^ allowLipSync.GetHashCode() 
        ^ writeMode.GetHashCode() 
        ^ EyeBlinkSettings.GetHashCode()
        ^ AdvancedLipSyncSettings.GetHashCode();
    }
}

internal enum TrackingPermission
{
    Allow,
    Disallow,
    Keep
}

internal enum ExpressionWriteMode
{
    Replace,
    Blend
}

