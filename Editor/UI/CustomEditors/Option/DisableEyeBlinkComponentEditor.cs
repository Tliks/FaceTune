namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(DisableEyeBlinkComponent))]
internal sealed class DisableEyeBlinkComponentEditor : FaceTuneSectionEditorBase<DisableEyeBlinkComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateSettingsSection(), CreateConditionSection() };

    private FaceTuneSection CreateSettingsSection()
        => CreateSection(
            "disableEyeBlink.section.label",
            new PropertiesSectionDrawer(),
            defaultExpanded: false);

    private FaceTuneSection CreateConditionSection()
        => CreateSection(
            "condition.section.label",
            new PropertiesSectionDrawer(
                new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(DisableEyeBlinkComponent.DisableWhen)),
                    DisableEyeBlinkComponent.CreateDefaultDisableWhen())),
            defaultExpanded: false);
}
