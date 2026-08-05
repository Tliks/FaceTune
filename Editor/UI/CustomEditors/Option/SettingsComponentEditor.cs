namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(SettingsComponent))]
internal sealed class SettingsComponentEditor : FaceTuneSectionEditorBase<SettingsComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateSettingsSection() };

    private FaceTuneSection CreateSettingsSection()
        => CreateSection(
            "settings.section.label",
            new PropertiesSectionDrawer(serializedObject.FindProperty(nameof(SettingsComponent.Settings))),
            defaultExpanded: false);
}
