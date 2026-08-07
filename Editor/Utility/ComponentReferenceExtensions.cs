namespace Aoyon.FaceTune;

internal static class ComponentReferenceExtensions
{
    public static EyeBlinkSettings ResolveSettings(this EyeBlinkComponent component)
    {
        if (component.ReferenceMode != ComponentReferenceMode.Reference) return component.Settings;

        var target = component.Reference;
        return target is { ReferenceMode: ComponentReferenceMode.Direct } ? target.Settings : new EyeBlinkSettings();
    }

    public static AdvancedLipSyncSettings ResolveSettings(this LipSyncComponent component)
    {
        if (component.ReferenceMode != ComponentReferenceMode.Reference) return component.AdvancedLipSyncSettings;

        var target = component.Reference;
        if (target is { ReferenceMode: ComponentReferenceMode.Direct }) return target.AdvancedLipSyncSettings;
        return AdvancedLipSyncSettings.Disabled();
    }
}
