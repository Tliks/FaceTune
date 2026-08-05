namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(MMDSupportComponent))]
internal sealed class MMDSupportComponentEditor : FaceTuneSectionEditorBase<MMDSupportComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateSettingsSection(), CreateConditionSection() };

    private FaceTuneSection CreateSettingsSection()
        => CreateSection(
            "mmdSupport.section.label",
            new PropertiesSectionDrawer(serializedObject.FindProperty(nameof(MMDSupportComponent.Settings))),
            defaultExpanded: false);

    private FaceTuneSection CreateConditionSection()
        => CreateSection(
            "condition.section.label",
            new PropertiesSectionDrawer(serializedObject.FindProperty(nameof(MMDSupportComponent.DisableWhen))),
            defaultExpanded: false);
}
