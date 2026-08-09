namespace Aoyon.FaceTune;

internal static class ComponentReferenceExtensions
{
    public static EyeBlinkSettings ResolveSettings(this EyeBlinkComponent component)
    {
        if (component.ReferenceMode != SettingsSourceMode.Reference) return component.Settings;

        var target = component.Reference;
        return target is { ReferenceMode: SettingsSourceMode.Direct } ? target.Settings : new EyeBlinkSettings();
    }

    public static AdvancedLipSyncSettings ResolveSettings(this LipSyncComponent component)
    {
        if (component.ReferenceMode != SettingsSourceMode.Reference) return component.AdvancedLipSyncSettings;

        var target = component.Reference;
        if (target is { ReferenceMode: SettingsSourceMode.Direct }) return target.AdvancedLipSyncSettings;
        return AdvancedLipSyncSettings.Disabled();
    }
}
