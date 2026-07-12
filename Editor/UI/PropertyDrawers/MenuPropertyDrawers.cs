namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(MenuIconSettings))]
internal sealed class MenuIconSettingsDrawer : PropertyDrawer
{
    private static GUIContent[] Modes => new[]
    {
        "menuIcon.none.label".LG(),
        "menuIcon.manual.label".LG(),
        "menuIcon.automatic.label".LG()
    };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var mode = property.FindPropertyRelative("Mode");
        var manual = property.FindPropertyRelative("ManualIcon");
        var preview = property.FindPropertyRelative("PreviewExpression");
        var selected = mode.enumValueIndex switch
        {
            (int)MenuIconMode.None => 0,
            (int)MenuIconMode.ExpressionPreview => 2,
            _ => 1
        };

        position.SetSingleHeight();
        var toolbarRect = EditorGUI.PrefixLabel(position, "menuIcon.icon.label".LG());
        var next = GUI.Toolbar(toolbarRect, selected, Modes);
        if (next != selected)
        {
            mode.enumValueIndex = next switch
            {
                0 => (int)MenuIconMode.None,
                2 => (int)MenuIconMode.ExpressionPreview,
                _ => (int)MenuIconMode.Manual
            };
            if (next == 0) manual.objectReferenceValue = null;
        }
        position.NewLine();

        position = EditorGUI.IndentedRect(position);
        if (next == 1) EditorGUI.PropertyField(position, manual, "menuIcon.manualIcon.label".LG());
        else if (next == 2) EditorGUI.PropertyField(position, preview, "menuIcon.previewExpression.label".LG());
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => property.FindPropertyRelative("Mode").enumValueIndex == (int)MenuIconMode.None
            ? FaceTuneDrawerUtility.Line
            : FaceTuneDrawerUtility.Line * 2f + FaceTuneDrawerUtility.Space;
}

[CustomPropertyDrawer(typeof(MenuInstallSettings))]
internal sealed class MenuInstallSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        => EditorGUI.PropertyField(position, property.FindPropertyRelative("InstallContainerOverride"), label);

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUI.GetPropertyHeight(property.FindPropertyRelative("InstallContainerOverride"), label);
}

[CustomPropertyDrawer(typeof(DirectMenuSettings))]
internal sealed class DirectMenuSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        FaceTuneDrawerUtility.Draw(ref position, property.FindPropertyRelative("MenuName"), "directMenu.menuName.label");
        FaceTuneDrawerUtility.Draw(ref position, property.FindPropertyRelative("Icon"), "directMenu.icon.label");
        FaceTuneDrawerUtility.Draw(ref position, property.FindPropertyRelative("InstallSettings"), "directMenu.destination.label");
        FaceTuneDrawerUtility.Draw(ref position, property.FindPropertyRelative("BlendExclusiveGroupName"), "directMenu.menuGroup.label");
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => FaceTuneDrawerUtility.Height(property.FindPropertyRelative("MenuName"))
         + FaceTuneDrawerUtility.Height(property.FindPropertyRelative("Icon"))
         + FaceTuneDrawerUtility.Height(property.FindPropertyRelative("InstallSettings"))
         + FaceTuneDrawerUtility.Height(property.FindPropertyRelative("BlendExclusiveGroupName"));
}

[CustomPropertyDrawer(typeof(ExclusiveToggleGroup))]
internal sealed class ExclusiveToggleGroupDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        => EditorGUI.PropertyField(position, property.FindPropertyRelative("GroupName"), label);

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUI.GetPropertyHeight(property.FindPropertyRelative("GroupName"), label);
}
