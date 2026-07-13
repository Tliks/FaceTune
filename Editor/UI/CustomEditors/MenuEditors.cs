namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(MenuComponent))]
internal sealed class MenuEditor : FaceTuneEditor<MenuComponent>
{
    protected override float GetInspectorHeight()
    {
        var kind = serializedObject.FindProperty(nameof(MenuComponent.Kind));
        var height = GetPropertyHeight(nameof(MenuComponent.MenuName))
                   + GetPropertyHeight(nameof(MenuComponent.Icon))
                   + GetPropertyHeight(nameof(MenuComponent.InstallSettings))
                   + GetPropertyHeight(nameof(MenuComponent.Kind))
                   + GetPropertyHeight(nameof(MenuComponent.ExclusiveToggleGroup))
                   + GetPropertyHeight(nameof(MenuComponent.ParameterName));
        if (kind.hasMultipleDifferentValues || IsMode(kind, (int)MenuItemKind.Toggle))
            height += GetPropertyHeight(nameof(MenuComponent.DefaultSelected));
        return Mathf.Max(0f, height - GUIHelper.VerticalSpacing);
    }

    protected override void DrawInspector(Rect position)
    {
        var menuName = serializedObject.FindProperty(nameof(MenuComponent.MenuName));
        MenuGUI.DrawMenuName(
            ref position,
            menuName,
            Component,
            new GUIContent(menuName.displayName));
        DrawProperty(ref position, nameof(MenuComponent.Icon));
        DrawProperty(ref position, nameof(MenuComponent.InstallSettings));
        DrawProperty(ref position, nameof(MenuComponent.Kind));
        DrawProperty(ref position, nameof(MenuComponent.ExclusiveToggleGroup));
        DrawProperty(ref position, nameof(MenuComponent.ParameterName));

        var kind = serializedObject.FindProperty(nameof(MenuComponent.Kind));
        if (kind.hasMultipleDifferentValues || IsMode(kind, (int)MenuItemKind.Toggle))
            DrawProperty(ref position, nameof(MenuComponent.DefaultSelected));
    }
}

[CanEditMultipleObjects]
[CustomEditor(typeof(MenuFolderComponent))]
internal sealed class MenuFolderEditor : FaceTuneEditor<MenuFolderComponent>
{
    protected override float GetInspectorHeight()
        => GetPropertyHeight(nameof(MenuFolderComponent.MenuName))
         + GetPropertyHeight(nameof(MenuFolderComponent.Icon))
         + GetPropertyHeight(nameof(MenuFolderComponent.InstallSettings))
         - GUIHelper.VerticalSpacing;

    protected override void DrawInspector(Rect position)
    {
        var menuName = serializedObject.FindProperty(nameof(MenuFolderComponent.MenuName));
        MenuGUI.DrawMenuName(
            ref position,
            menuName,
            Component,
            new GUIContent(menuName.displayName));
        DrawProperty(ref position, nameof(MenuFolderComponent.Icon));
        DrawProperty(ref position, nameof(MenuFolderComponent.InstallSettings));
    }
}
