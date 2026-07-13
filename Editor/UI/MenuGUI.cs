namespace Aoyon.FaceTune.Gui;

internal static class MenuGUI
{
    public static void DrawMenuName(
        ref Rect position,
        SerializedProperty property,
        Component? owner,
        GUIContent label)
    {
        position.height = GUIHelper.LineHeight;
        GUIHelper.DrawPlaceholderTextField(
            position,
            property,
            label,
            new GUIContent(owner != null ? owner.gameObject.name : string.Empty));
        position.NewLine();
    }
}

[CustomPropertyDrawer(typeof(MenuIconSettings))]
internal sealed class MenuIconSettingsDrawer : PropertyDrawer
{
    private const float MissingIconWarningHeight = 30f;
    private const float MissingExpressionWarningHeight = 30f;
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

        position.Indent();
        if (next == 1)
        {
            EditorGUI.PropertyField(position, manual, "menuIcon.manualIcon.label".LG());
            if (manual.objectReferenceValue == null)
            {
                position.NewLine();
                position.height = MissingIconWarningHeight;
                EditorGUI.HelpBox(
                    position,
                    "menuIcon.manualIcon.empty.message".LS(),
                    MessageType.Warning);
            }
        }
        else if (next == 2)
        {
            var ownerIsExpression = property.serializedObject.targetObject is FaceTuneComponent;
            if (ownerIsExpression)
            {
                var placeholder = "menuIcon.currentExpression.placeholder".LG().text;
                GUIHelper.DrawPlaceholderObjectField(
                    position,
                    preview,
                    "menuIcon.previewExpression.label".LG(),
                    new GUIContent($"{placeholder} ({nameof(FaceTuneComponent)})"));
            }
            else
            {
                EditorGUI.PropertyField(position, preview, "menuIcon.previewExpression.label".LG());
                if (preview.objectReferenceValue == null)
                {
                    position.NewLine();
                    position.height = MissingExpressionWarningHeight;
                    EditorGUI.HelpBox(
                        position,
                        "menuIcon.previewExpression.empty.message".LS(),
                        MessageType.Warning);
                }
            }
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var mode = property.FindPropertyRelative("Mode").enumValueIndex;
        if (mode == (int)MenuIconMode.None) return GUIHelper.LineHeight;

        var height = GUIHelper.LineHeight * 2f + GUIHelper.VerticalSpacing;
        if (mode == (int)MenuIconMode.Manual
            && property.FindPropertyRelative("ManualIcon").objectReferenceValue == null)
            height += GUIHelper.VerticalSpacing + MissingIconWarningHeight;
        if (mode == (int)MenuIconMode.ExpressionPreview
            && property.serializedObject.targetObject is not FaceTuneComponent
            && property.FindPropertyRelative("PreviewExpression").objectReferenceValue == null)
            height += GUIHelper.VerticalSpacing + MissingExpressionWarningHeight;
        return height;
    }
}

[CustomPropertyDrawer(typeof(MenuInstallSettings))]
internal sealed class MenuInstallSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var reference = property.FindPropertyRelative("InstallContainerOverride");
        var owner = property.serializedObject.targetObject as Component;
        var folder = owner != null
            ? owner.transform.parent?.GetComponentInParent<MenuFolderComponent>()
            : null;
        var rootLabel = "menuInstallSettings.root.placeholder".LS();
        var placeholder = folder != null
            ? $"{EffectiveMenuName(folder)} (Menu Folder)"
            : rootLabel;
        GUIHelper.DrawPlaceholderObjectLikeField(
            position,
            reference,
            label,
            new GUIContent(placeholder),
            AvatarObjectReference.IsEmpty(reference));
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUI.GetPropertyHeight(property.FindPropertyRelative("InstallContainerOverride"), label);

    private static string EffectiveMenuName(MenuFolderComponent folder)
        => string.IsNullOrWhiteSpace(folder.MenuName) ? folder.gameObject.name : folder.MenuName;
}

[CustomPropertyDrawer(typeof(DirectMenuSettings))]
internal sealed class DirectMenuSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        MenuGUI.DrawMenuName(
            ref position,
            property.FindPropertyRelative("MenuName"),
            property.serializedObject.targetObject as Component,
            "directMenu.menuName.label".LG());
        GUIHelper.DrawProperty(ref position, property.FindPropertyRelative("Icon"), "directMenu.icon.label");
        GUIHelper.DrawProperty(ref position, property.FindPropertyRelative("InstallSettings"), "directMenu.destination.label");
        if (!IsReplaceMode(property.serializedObject))
            GUIHelper.DrawProperty(ref position, property.FindPropertyRelative("BlendExclusiveGroupName"), "menu.group.label");
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var height = GUIHelper.PropertyHeight(property.FindPropertyRelative("MenuName"))
                   + GUIHelper.PropertyHeight(property.FindPropertyRelative("Icon"))
                   + GUIHelper.PropertyHeight(property.FindPropertyRelative("InstallSettings"));
        if (!IsReplaceMode(property.serializedObject))
            height += GUIHelper.PropertyHeight(property.FindPropertyRelative("BlendExclusiveGroupName"));
        return height;
    }

    private static bool IsReplaceMode(SerializedObject serializedObject)
    {
        var settings = serializedObject.FindProperty(nameof(FaceTuneComponent.FacialSettings));
        var mode = settings?.FindPropertyRelative(FacialSettings.WriteModePropName);
        return mode != null
            && !mode.hasMultipleDifferentValues
            && mode.enumValueIndex == (int)ExpressionWriteMode.Replace;
    }
}

[CustomPropertyDrawer(typeof(ExclusiveToggleGroup))]
internal sealed class ExclusiveToggleGroupDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        => EditorGUI.PropertyField(position, property.FindPropertyRelative("GroupName"), label);

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUI.GetPropertyHeight(property.FindPropertyRelative("GroupName"), label);
}
