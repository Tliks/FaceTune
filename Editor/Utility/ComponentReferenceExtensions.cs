namespace Aoyon.FaceTune;

internal static class ComponentReferenceExtensions
{
    public static AdvancedEyeBlinkSettings ResolveSettings(this EyeBlinkComponent component)
    {
        if (component.ReferenceMode != ComponentReferenceMode.Reference) return component.AdvancedEyeBlinkSettings;

        var target = component.Reference.Get(component)?.GetComponent<EyeBlinkComponent>();
        if (target is { ReferenceMode: ComponentReferenceMode.Direct }) return target.AdvancedEyeBlinkSettings;
        return AdvancedEyeBlinkSettings.Disabled();
    }

    public static AdvancedLipSyncSettings ResolveSettings(this LipSyncComponent component)
    {
        if (component.ReferenceMode != ComponentReferenceMode.Reference) return component.AdvancedLipSyncSettings;

        var target = component.Reference.Get(component)?.GetComponent<LipSyncComponent>();
        if (target is { ReferenceMode: ComponentReferenceMode.Direct }) return target.AdvancedLipSyncSettings;
        return AdvancedLipSyncSettings.Disabled();
    }
}
