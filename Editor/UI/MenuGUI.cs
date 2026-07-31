using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune.Gui;

internal static class MenuGUI
{
    private static readonly Dictionary<string, bool> NewGroupNameEditingByProperty = new();

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

    public static void DrawGroupSelector(
        Rect position,
        SerializedProperty groupName,
        Component? owner,
        GUIContent label)
    {
        if (groupName.hasMultipleDifferentValues)
        {
            EditorGUI.PropertyField(position, groupName, label);
            return;
        }

        var stateKey = $"{groupName.serializedObject.targetObject.GetInstanceID()}:{groupName.propertyPath}";
        if (NewGroupNameEditingByProperty.ContainsKey(stateKey))
        {
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.DelayedTextField(position, label, groupName.stringValue);
            if (EditorGUI.EndChangeCheck()) groupName.stringValue = value;
            if (!string.IsNullOrWhiteSpace(groupName.stringValue))
                NewGroupNameEditingByProperty.Remove(stateKey);
            return;
        }

        var groups = GetDefinedGroupNames(owner);
        var currentIndex = groups.IndexOf(groupName.stringValue);
        var options = new GUIContent[groups.Count + 2];
        options[0] = "menu.group.none.label".LG();
        for (var i = 0; i < groups.Count; i++) options[i + 1] = new GUIContent(groups[i]);
        options[^1] = "menu.group.new.label".LG();

        var selectedIndex = currentIndex < 0 ? 0 : currentIndex + 1;
        var nextIndex = EditorGUI.Popup(position, label, selectedIndex, options);
        if (nextIndex == selectedIndex) return;
        if (nextIndex == options.Length - 1)
        {
            groupName.stringValue = string.Empty;
            NewGroupNameEditingByProperty[stateKey] = true;
            GUI.FocusControl(null);
            return;
        }

        groupName.stringValue = nextIndex == 0 ? string.Empty : groups[nextIndex - 1];
    }

    public static void DrawBuiltInGroup(Rect position, GUIContent label, string groupName)
    {
        using var _ = new EditorGUI.DisabledScope(true);
        EditorGUI.TextField(position, label, groupName);
    }

    private static List<string> GetDefinedGroupNames(Component? owner)
    {
        if (owner == null) return new();
        var root = RuntimeUtil.FindAvatarInParents(owner.transform);
        if (root == null) return new();

        var groups = new HashSet<string>();
        foreach (var menu in root.GetComponentsInChildren<MenuComponent>(true))
        {
            var groupName = menu.ExclusiveToggleGroup.GroupName;
            if (!string.IsNullOrWhiteSpace(groupName) && groupName != BuiltInMenuGroups.DirectMenuReplace)
                groups.Add(groupName);
        }
        foreach (var expression in root.GetComponentsInChildren<FaceTuneComponent>(true))
        {
            var groupName = expression.DirectMenuSettings.BlendExclusiveGroupName;
            if (!string.IsNullOrWhiteSpace(groupName)) groups.Add(groupName);
        }
        return groups.OrderBy(name => name, StringComparer.Ordinal).ToList();
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
                GUIHelper.DrawPlaceholderObjectLikeField(
                    position,
                    preview,
                    "menuIcon.previewExpression.label".LG(),
                    new GUIContent($"{placeholder} ({FaceTuneComponent.ComponentName})"),
                    AvatarObjectReference.IsEmpty(preview));
            }
            else
            {
                EditorGUI.PropertyField(position, preview, "menuIcon.previewExpression.label".LG());
                if (AvatarObjectReference.IsEmpty(preview))
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
            && AvatarObjectReference.IsEmpty(property.FindPropertyRelative("PreviewExpression")))
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
        var folder = owner is MenuFolderComponent
            ? owner.transform.parent?.GetComponentInParent<MenuFolderComponent>()
            : owner?.GetComponentInParent<MenuFolderComponent>();
        var rootLabel = "menuInstallSettings.root.placeholder".LS();
        var destination = folder != null ? EffectiveMenuName(folder) : rootLabel;
        var placeholder = $"{destination} ({"menuInstallSettings.destinationType.placeholder".LS()})";
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
        var group = property.FindPropertyRelative("BlendExclusiveGroupName");
        var replaceMode = IsReplaceMode(property.serializedObject);
        position.height = GUIHelper.LineHeight;
        if (replaceMode)
            MenuGUI.DrawBuiltInGroup(position, "menu.group.label".LG(), BuiltInMenuGroups.DirectMenuReplace);
        else
            MenuGUI.DrawGroupSelector(position, group, property.serializedObject.targetObject as Component, "menu.group.label".LG());
        position.NewLine();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return GUIHelper.PropertyHeight(property.FindPropertyRelative("MenuName"))
             + GUIHelper.PropertyHeight(property.FindPropertyRelative("Icon"))
             + GUIHelper.PropertyHeight(property.FindPropertyRelative("InstallSettings"))
             + GUIHelper.PropertyHeight(property.FindPropertyRelative("BlendExclusiveGroupName"));
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
        => MenuGUI.DrawGroupSelector(
            position,
            property.FindPropertyRelative("GroupName"),
            property.serializedObject.targetObject as Component,
            label);

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUI.GetPropertyHeight(property.FindPropertyRelative("GroupName"), label);
}
