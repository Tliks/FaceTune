namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(MenuComponent))]
internal sealed class MenuEditor : FaceTuneEditor<MenuComponent>
{
    protected override void DrawInspector()
    {
        DrawProperty(nameof(MenuComponent.MenuName));
        DrawProperty(nameof(MenuComponent.Icon));
        DrawProperty(nameof(MenuComponent.InstallSettings));

        var kind = serializedObject.FindProperty(nameof(MenuComponent.Kind));
        EditorGUILayout.PropertyField(kind);

        DrawProperty(nameof(MenuComponent.ExclusiveToggleGroup));
        DrawProperty(nameof(MenuComponent.ParameterName));
        if (kind.hasMultipleDifferentValues || IsMode(kind, (int)MenuItemKind.Toggle))
            DrawProperty(nameof(MenuComponent.DefaultSelected));
    }
}

[CanEditMultipleObjects]
[CustomEditor(typeof(MenuFolderComponent))]
internal sealed class MenuFolderEditor : FaceTuneEditor<MenuFolderComponent>
{
}
