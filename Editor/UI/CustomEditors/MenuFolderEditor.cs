namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(MenuFolderComponent))]
internal sealed class MenuFolderEditor : FaceTuneSectionEditor<MenuFolderComponent>
{
    protected override bool DefaultExpanded => false;
    protected override GUIContent SectionLabel => "menuFolder.section.label".LG();

    protected override float GetSectionContentHeight()
        => GetPropertyHeight(nameof(MenuFolderComponent.MenuName))
         + GetPropertyHeight(nameof(MenuFolderComponent.Icon))
         + GetPropertyHeight(nameof(MenuFolderComponent.InstallSettings))
         - GUIHelper.VerticalSpacing;

    protected override void DrawSectionContent(Rect position)
    {
        var menuName = serializedObject.FindProperty(nameof(MenuFolderComponent.MenuName));
        MenuGUI.DrawMenuName(ref position, menuName, Component, "menu.name.label".LG());
        GUIHelper.DrawProperty(ref position, serializedObject.FindProperty(nameof(MenuFolderComponent.Icon)), "menu.icon.label");
        GUIHelper.DrawProperty(ref position, serializedObject.FindProperty(nameof(MenuFolderComponent.InstallSettings)), "menu.destination.label");
    }
}
