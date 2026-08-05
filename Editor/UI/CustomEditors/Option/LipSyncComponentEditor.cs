namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(LipSyncComponent))]
internal sealed class LipSyncComponentEditor : FaceTuneSectionEditorBase<LipSyncComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateLipSyncSection() };

    private FaceTuneSection CreateLipSyncSection()
        => CreateSection(
            "lipSync.section.label",
            new PropertiesSectionDrawer(
                new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(LipSyncComponent.ReferenceMode)),
                    ComponentReferenceMode.Direct),
                new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(LipSyncComponent.Reference)),
                    null),
                new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(LipSyncComponent.AdvancedLipSyncSettings)),
                    new AdvancedLipSyncSettings())),
            defaultExpanded: false);
}
