namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(EyeBlinkComponent))]
internal sealed class EyeBlinkComponentEditor : FaceTuneSectionEditorBase<EyeBlinkComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateEyeBlinkSection() };

    private FaceTuneSection CreateEyeBlinkSection()
        => CreateSection(
            "eyeBlink.section.label",
            new PropertiesSectionDrawer(
                new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(EyeBlinkComponent.ReferenceMode)),
                    ComponentReferenceMode.Direct),
                new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(EyeBlinkComponent.Reference)),
                    null),
                new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(EyeBlinkComponent.Settings)),
                    new EyeBlinkSettings())),
            defaultExpanded: false);
}
