namespace Aoyon.FaceTune;

internal static class ComponentReferenceUtility
{
    public static AdvancedEyeBlinkSettings ResolveSettings(EyeBlinkComponent component)
    {
        if (component.ReferenceMode != ComponentReferenceMode.Reference) return component.AdvancedEyeBlinkSettings;

        var target = component.Reference.Get(component)?.GetComponent<EyeBlinkComponent>();
        if (target is { ReferenceMode: ComponentReferenceMode.Direct }) return target.AdvancedEyeBlinkSettings;
        return AdvancedEyeBlinkSettings.Disabled();
    }

    public static AdvancedLipSyncSettings ResolveSettings(LipSyncComponent component)
    {
        if (component.ReferenceMode != ComponentReferenceMode.Reference) return component.AdvancedLipSyncSettings;

        var target = component.Reference.Get(component)?.GetComponent<LipSyncComponent>();
        if (target is { ReferenceMode: ComponentReferenceMode.Direct }) return target.AdvancedLipSyncSettings;
        return AdvancedLipSyncSettings.Disabled();
    }
}
