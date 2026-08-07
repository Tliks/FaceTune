namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(DisableLipSyncComponent))]
internal sealed class DisableLipSyncComponentEditor : FaceTuneSectionEditorBase<DisableLipSyncComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateSettingsSection(), CreateConditionSection() };

    private FaceTuneSection CreateSettingsSection()
        => CreateSection(
            "disableLipSync.section.label",
            new PropertiesSectionDrawer(),
            defaultExpanded: false);

    private FaceTuneSection CreateConditionSection()
        => CreateSection(
            "condition.section.label",
            new PropertiesSectionDrawer(
                new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(DisableLipSyncComponent.DisableWhen)))),
            defaultExpanded: false);
}
