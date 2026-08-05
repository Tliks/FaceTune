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
            new PropertiesSectionDrawer(
                new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(MMDSupportComponent.Settings)),
                    new MmdSupportSettings())),
            defaultExpanded: false);

    private FaceTuneSection CreateConditionSection()
        => CreateSection(
            "condition.section.label",
            new PropertiesSectionDrawer(
                new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(MMDSupportComponent.DisableWhen)),
                    new Condition(ConditionCase.From(new MenuCondition())))),
            defaultExpanded: false);
}
