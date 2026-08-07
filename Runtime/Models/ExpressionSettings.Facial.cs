namespace Aoyon.FaceTune;

[Serializable]
internal record class FacialSettings
{
    [SerializeField] private TrackingPermission allowEyeBlink;
    [SerializeField] private TrackingPermission allowLipSync;
    [SerializeField] private ExpressionWriteMode writeMode;
    [Obsolete("Only kept for migration. Use WriteMode."), SerializeField] private bool enableBlending;

    public TrackingPermission AllowEyeBlink { get => allowEyeBlink; init => allowEyeBlink = value; }
    public TrackingPermission AllowLipSync { get => allowLipSync; init => allowLipSync = value; }
    public ExpressionWriteMode WriteMode { get => writeMode; init => writeMode = value; }
    [Obsolete("Only kept for migration. Use WriteMode.")]
    public bool EnableBlending { get => enableBlending; init => enableBlending = value; }

    public const string AllowEyeBlinkPropName = nameof(allowEyeBlink);
    public const string AllowLipSyncPropName = nameof(allowLipSync);
    public const string WriteModePropName = nameof(writeMode);
    [Obsolete("Only kept for migration. Use WriteModePropName.")]
    public const string EnableBlendingPropName = nameof(enableBlending);

    internal EyeBlinkSettings EyeBlinkSettings { get; init; } = new();
    internal AdvancedLipSyncSettings AdvancedLipSyncSettings { get; init; } = AdvancedLipSyncSettings.Disabled();

    public FacialSettings() : this(TrackingPermission.Disallow, TrackingPermission.Allow, ExpressionWriteMode.Replace) { }
    public FacialSettings(TrackingPermission eyeBlink, TrackingPermission lipSync, ExpressionWriteMode writeMode)
        => (allowEyeBlink, allowLipSync, this.writeMode) = (eyeBlink, lipSync, writeMode);
    [Obsolete("Only kept for migration. Use the ExpressionWriteMode overload.")]
    public FacialSettings(TrackingPermission eyeBlink, TrackingPermission lipSync, bool blend)
        : this(eyeBlink, lipSync, blend ? ExpressionWriteMode.Blend : ExpressionWriteMode.Replace) { }

    internal static FacialSettings Keep = new(TrackingPermission.Keep, TrackingPermission.Keep, ExpressionWriteMode.Blend);
}

internal enum TrackingPermission { Allow, Disallow, Keep }
internal enum ExpressionWriteMode { Replace, Blend }
