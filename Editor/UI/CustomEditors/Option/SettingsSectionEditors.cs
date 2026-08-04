namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(DisableEyeBlinkComponent))]
internal sealed class DisableEyeBlinkComponentEditor : FaceTuneSectionEditor<DisableEyeBlinkComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreatePropertySection("disableEyeBlink.section.label".LG(), nameof(DisableEyeBlinkComponent.DisableWhen)) };
}

[CanEditMultipleObjects]
[CustomEditor(typeof(DisableLipSyncComponent))]
internal sealed class DisableLipSyncComponentEditor : FaceTuneSectionEditor<DisableLipSyncComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreatePropertySection("disableLipSync.section.label".LG(), nameof(DisableLipSyncComponent.DisableWhen)) };
}

[CanEditMultipleObjects]
[CustomEditor(typeof(EyeBlinkComponent))]
internal sealed class EyeBlinkComponentEditor : FaceTuneSectionEditor<EyeBlinkComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[]
        {
            CreatePropertySection(
                "eyeBlink.section.label".LG(),
                nameof(EyeBlinkComponent.ReferenceMode),
                nameof(EyeBlinkComponent.Reference),
                nameof(EyeBlinkComponent.AdvancedEyeBlinkSettings))
        };
}

[CanEditMultipleObjects]
[CustomEditor(typeof(LipSyncComponent))]
internal sealed class LipSyncComponentEditor : FaceTuneSectionEditor<LipSyncComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[]
        {
            CreatePropertySection(
                "lipSync.section.label".LG(),
                nameof(LipSyncComponent.ReferenceMode),
                nameof(LipSyncComponent.Reference),
                nameof(LipSyncComponent.AdvancedLipSyncSettings))
        };
}

[CanEditMultipleObjects]
[CustomEditor(typeof(LockFacialComponent))]
internal sealed class LockFacialComponentEditor : FaceTuneSectionEditor<LockFacialComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreatePropertySection("lockFacial.section.label".LG(), nameof(LockFacialComponent.LockWhen)) };
}

[CanEditMultipleObjects]
[CustomEditor(typeof(MMDSupportComponent))]
internal sealed class MMDSupportComponentEditor : FaceTuneSectionEditor<MMDSupportComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreatePropertySection("mmdSupport.section.label".LG(), nameof(MMDSupportComponent.Settings)) };
}

[CanEditMultipleObjects]
[CustomEditor(typeof(SettingsComponent))]
internal sealed class SettingsComponentEditor : FaceTuneSectionEditor<SettingsComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreatePropertySection("settings.section.label".LG(), nameof(SettingsComponent.Settings)) };
}

[CanEditMultipleObjects]
[CustomEditor(typeof(TransitionComponent))]
internal sealed class TransitionComponentEditor : FaceTuneSectionEditor<TransitionComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreatePropertySection("transition.section.label".LG(), nameof(TransitionComponent.DurationSeconds)) };
}
