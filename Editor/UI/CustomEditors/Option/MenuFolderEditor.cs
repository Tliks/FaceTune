namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(MenuFolderComponent))]
internal sealed class MenuFolderEditor : FaceTuneSectionEditor<MenuFolderComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateMenuFolderSection() };

    private FaceTuneSection CreateMenuFolderSection()
        => new(
            "menuFolder.section.label".LG(),
            () => GetPropertyHeight(nameof(MenuFolderComponent.MenuName))
                + GUIHelper.VerticalSpacing
                + GetPropertyHeight(nameof(MenuFolderComponent.Icon))
                + GUIHelper.VerticalSpacing
                + GetPropertyHeight(nameof(MenuFolderComponent.InstallSettings)),
            DrawSectionContent,
            false);

    private void DrawSectionContent(Rect position)
    {
        var menuName = serializedObject.FindProperty(nameof(MenuFolderComponent.MenuName));
        MenuGUI.DrawMenuName(ref position, menuName, Component, "menuFolder.name.label".LG());
        GUIHelper.DrawProperty(ref position, serializedObject.FindProperty(nameof(MenuFolderComponent.Icon)), "menu.icon.label");
        GUIHelper.DrawProperty(ref position, serializedObject.FindProperty(nameof(MenuFolderComponent.InstallSettings)), "menu.destination.label");
    }
}
