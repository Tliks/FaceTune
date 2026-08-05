namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(MenuFolderComponent))]
internal sealed class MenuFolderEditor : FaceTuneSectionEditorBase<MenuFolderComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateMenuFolderSection() };

    private FaceTuneSection CreateMenuFolderSection()
        => CreateSection(
            "menuFolder.section.label",
            new PropertiesSectionDrawer(
                serializedObject.FindProperty(nameof(MenuFolderComponent.Menu))),
            defaultExpanded: false);
}
