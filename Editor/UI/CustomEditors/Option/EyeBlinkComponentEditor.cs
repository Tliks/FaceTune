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
                serializedObject.FindProperty(nameof(EyeBlinkComponent.ReferenceMode)),
                serializedObject.FindProperty(nameof(EyeBlinkComponent.Reference)),
                serializedObject.FindProperty(nameof(EyeBlinkComponent.AdvancedEyeBlinkSettings))),
            defaultExpanded: false);
}
