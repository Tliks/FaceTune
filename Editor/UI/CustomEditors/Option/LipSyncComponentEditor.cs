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
                    serializedObject.FindProperty(nameof(LipSyncComponent.ReferenceMode))),
                new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(LipSyncComponent.Reference))),
                new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(LipSyncComponent.AdvancedLipSyncSettings)))),
            defaultExpanded: false);
}
