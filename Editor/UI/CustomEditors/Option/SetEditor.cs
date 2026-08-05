namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(SetComponent))]
internal sealed class SetEditor : FaceTuneSectionEditorBase<SetComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateSetSection(), CreateMenuSettingsSection() };

    private FaceTuneSection CreateSetSection()
        => CreateSection(
            "set.section.label",
            new PropertiesSectionDrawer(
                serializedObject.FindProperty(nameof(SetComponent.DefaultSelected)),
                "menu.defaultSelected.label"),
            defaultExpanded: true);

    private FaceTuneSection CreateMenuSettingsSection()
        => CreateSection(
            "menuSettings.section.label",
            new PropertiesSectionDrawer(
                serializedObject.FindProperty(nameof(SetComponent.Menu))),
            defaultExpanded: false);
}
