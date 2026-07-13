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

        using (new EditorGUI.IndentLevelScope())
            position = EditorGUI.IndentedRect(position);
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
            var placeholder = "menuIcon.currentExpression.placeholder".LG().text;
            GUIHelper.DrawPlaceholderObjectField(
                position,
                preview,
                "menuIcon.previewExpression.label".LG(),
                new GUIContent($"{placeholder} ({nameof(FaceTuneComponent)})"));
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
        var folderLabel = "menuInstallSettings.folder.placeholder".LS();
        var rootLabel = "menuInstallSettings.root.placeholder".LS();
        var placeholder = folder != null
            ? $"{folderLabel} ({EffectiveMenuName(folder)}) (Transform)"
            : $"{rootLabel} (Transform)";
        GUIHelper.DrawPlaceholderObjectLikeField(
            position,
            reference,
            label,
            new GUIContent(placeholder),
            IsEmptyReference(reference, owner));
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUI.GetPropertyHeight(property.FindPropertyRelative("InstallContainerOverride"), label);

    private static bool IsEmptyReference(SerializedProperty reference, Component? owner)
    {
        var path = reference.FindPropertyRelative("referencePath").stringValue;
        if (!string.IsNullOrEmpty(path)) return false;

        var target = reference.FindPropertyRelative("targetObject").objectReferenceValue as GameObject;
        if (target == null || owner == null) return true;

        var avatarRoot = nadena.dev.modular_avatar.core.RuntimeUtil
            .FindAvatarTransformInParents(owner.transform);
        return avatarRoot == null
            || (target.transform != avatarRoot && !target.transform.IsChildOf(avatarRoot));
    }

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
            GUIHelper.DrawProperty(ref position, property.FindPropertyRelative("BlendExclusiveGroupName"), "directMenu.menuGroup.label");
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
