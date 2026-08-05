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
                serializedObject.FindProperty(nameof(LipSyncComponent.ReferenceMode)),
                serializedObject.FindProperty(nameof(LipSyncComponent.Reference)),
                serializedObject.FindProperty(nameof(LipSyncComponent.AdvancedLipSyncSettings))),
            defaultExpanded: false);
}
