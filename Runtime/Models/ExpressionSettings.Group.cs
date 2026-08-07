namespace Aoyon.FaceTune;

[Serializable]
internal sealed class TransitionSetting { public float DurationSeconds; }

[Serializable]
internal sealed class StyleSetting
{
    public ExpressionData Data = new();
    public bool ApplyToRenderer;
}

[Serializable]
internal sealed class EyeBlinkSetting : ISettingReference<EyeBlinkSettings>
{
    public SettingSourceMode SourceMode = SettingSourceMode.Direct;
    public Transform? Source;
    public EyeBlinkSettings Settings = new();
    SettingSourceMode ISettingReference<EyeBlinkSettings>.SourceMode => SourceMode;
    Transform? ISettingReference<EyeBlinkSettings>.Source => Source;
    EyeBlinkSettings ISettingReference<EyeBlinkSettings>.DirectValue => Settings;
}

[Serializable]
internal sealed class LipSyncSetting : ISettingReference<AdvancedLipSyncSettings>
{
    public SettingSourceMode SourceMode = SettingSourceMode.Direct;
    public Transform? Source;
    public AdvancedLipSyncSettings Settings = new();
    SettingSourceMode ISettingReference<AdvancedLipSyncSettings>.SourceMode => SourceMode;
    Transform? ISettingReference<AdvancedLipSyncSettings>.Source => Source;
    AdvancedLipSyncSettings ISettingReference<AdvancedLipSyncSettings>.DirectValue => Settings;
}

[Serializable]
internal sealed class SetSetting
{
    public MenuSettings Menu = new();
    public bool DefaultSelected;
}

internal interface IExpressionSettingsSource
{
    IReadOnlyList<TransitionSetting> TransitionSettings { get; }
    IReadOnlyList<StyleSetting> StyleSettings { get; }
    IReadOnlyList<EyeBlinkSetting> EyeBlinkSettings { get; }
    IReadOnlyList<LipSyncSetting> LipSyncSettings { get; }
}

internal interface ISettingReference<TValue>
{
    SettingSourceMode SourceMode { get; }
    Transform? Source { get; }
    TValue DirectValue { get; }
}

internal enum SettingSourceMode { Direct, Reference }
