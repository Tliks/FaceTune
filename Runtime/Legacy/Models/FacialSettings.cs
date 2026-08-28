#pragma warning disable CS0618

namespace Aoyon.FaceTune;

[Serializable]
[Obsolete("Legacy serialized data retained only for migration.")]
internal record class FacialSettings // Immutable
{
    [SerializeField] private LegacyTrackingPermission allowEyeBlink;
    public LegacyTrackingPermission AllowEyeBlink { get => allowEyeBlink; init => allowEyeBlink = value; }
    public const string AllowEyeBlinkPropName = nameof(allowEyeBlink);

    [SerializeField] private LegacyTrackingPermission allowLipSync;
    public LegacyTrackingPermission AllowLipSync { get => allowLipSync; init => allowLipSync = value; }
    public const string AllowLipSyncPropName = nameof(allowLipSync);
    
    [SerializeField] private bool enableBlending;
    public bool EnableBlending { get => enableBlending; init => enableBlending = value; }
    public const string EnableBlendingPropName = nameof(enableBlending);

    internal AdvancedEyeBlinkSettings AdvancedEyBlinkSettings { get; init; }
    internal AdvancedLipSyncSettings AdvancedLipSyncSettings { get; init; }
    
    public FacialSettings() : this(LegacyTrackingPermission.Disallow, LegacyTrackingPermission.Allow, false)
    {
    }

    public FacialSettings(LegacyTrackingPermission allowEyeBlink, LegacyTrackingPermission allowLipSync, bool enableBlending) : this(allowEyeBlink, allowLipSync, enableBlending, AdvancedEyeBlinkSettings.Disabled(), AdvancedLipSyncSettings.Disabled())
    {
    }

    public FacialSettings(LegacyTrackingPermission allowEyeBlink, LegacyTrackingPermission allowLipSync, bool enableBlending, AdvancedEyeBlinkSettings advancedEyBlinkSettings, AdvancedLipSyncSettings advancedLipSyncSettings)
    {
        this.allowEyeBlink = allowEyeBlink;
        this.allowLipSync = allowLipSync;
        this.enableBlending = enableBlending;
        this.AdvancedEyBlinkSettings = advancedEyBlinkSettings;
        this.AdvancedLipSyncSettings = advancedLipSyncSettings;
    }

    internal static FacialSettings Keep = new(LegacyTrackingPermission.Keep, LegacyTrackingPermission.Keep, true);

    internal FacialSettings Merge(FacialSettings other)
    {
        return new FacialSettings(
            allowEyeBlink == LegacyTrackingPermission.Keep ? other.allowEyeBlink : allowEyeBlink,
            allowLipSync == LegacyTrackingPermission.Keep ? other.allowLipSync : allowLipSync,
            enableBlending == other.enableBlending,
            other.AdvancedEyBlinkSettings,
            other.AdvancedLipSyncSettings);
    }

    public virtual bool Equals(FacialSettings other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return allowEyeBlink == other.allowEyeBlink
         && allowLipSync == other.allowLipSync
         && enableBlending == other.enableBlending
         && AdvancedEyBlinkSettings == other.AdvancedEyBlinkSettings
         && AdvancedLipSyncSettings == other.AdvancedLipSyncSettings;
    }
    public override int GetHashCode()
    {
        return allowEyeBlink.GetHashCode() 
        ^ allowLipSync.GetHashCode() 
        ^ enableBlending.GetHashCode() 
        ^ AdvancedEyBlinkSettings.GetHashCode()
        ^ AdvancedLipSyncSettings.GetHashCode();
    }
}

#pragma warning restore CS0618
