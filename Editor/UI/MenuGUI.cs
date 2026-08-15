using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune.Gui;

internal static class MenuSettingsGUI
{
    public static float GetHeight(SerializedProperty settings)
        => GUIHelper.PropertyHeight(settings.FindPropertyRelative(nameof(MenuSettings.MenuName)))
         + GUIHelper.PropertyHeight(settings.FindPropertyRelative(nameof(MenuSettings.Icon)))
         + EditorGUI.GetPropertyHeight(settings.FindPropertyRelative(nameof(MenuSettings.InstallContainer)), GUIContent.none, true);

    public static void Draw(Rect position, SerializedProperty settings, Component? owner, string menuNameLabelKey)
    {
        position.height = GUIHelper.LineHeight;
        GUIHelper.DrawPlaceholderTextField(
            position,
            settings.FindPropertyRelative(nameof(MenuSettings.MenuName)),
            menuNameLabelKey.LG(),
            new GUIContent(owner?.gameObject.name ?? string.Empty));
        position.NewLine();
        GUIHelper.DrawProperty(ref position, settings.FindPropertyRelative(nameof(MenuSettings.Icon)), "menu.icon.label");
        GUIHelper.DrawProperty(ref position, settings.FindPropertyRelative(nameof(MenuSettings.InstallContainer)), "menu.destination.label");
    }
}

[CustomPropertyDrawer(typeof(MenuSettings))]
internal sealed class MenuSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        => MenuSettingsGUI.Draw(position, property, property.serializedObject.targetObject as Component, GetMenuNameLabelKey(property));

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => MenuSettingsGUI.GetHeight(property);

    private static string GetMenuNameLabelKey(SerializedProperty property)
        => property.propertyPath.StartsWith(nameof(ExpressionComponent.DirectMenuSettings), StringComparison.Ordinal)
            ? "directMenu.menuName.label"
            : "menu.name.label";
}

[CustomPropertyDrawer(typeof(MenuInstallContainerAttribute))]
internal sealed class MenuInstallContainerDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var owner = property.serializedObject.targetObject as Component;
        var destination = FindParentFolder(owner) is { } folder
            ? EffectiveMenuName(folder)
            : "menuInstallSettings.root.placeholder".LS();
        var placeholder = $"{destination} ({"menuInstallSettings.destinationType.placeholder".LS()})";
        GUIHelper.DrawPlaceholderObjectLikeField(
            position,
            property,
            label,
            new GUIContent(placeholder),
            property.objectReferenceValue == null);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => GUIHelper.LineHeight;

    private static MenuComponent? FindParentFolder(Component? owner)
    {
        for (var current = owner?.transform.parent; current != null; current = current.parent)
        {
            var menu = current.GetComponent<MenuComponent>();
            if (menu != null && menu.MenuKind == MenuComponent.Kind.Folder) return menu;
        }
        return null;
    }

    private static string EffectiveMenuName(MenuComponent folder)
        => string.IsNullOrWhiteSpace(folder.Menu.MenuName)
            ? folder.gameObject.name
            : folder.Menu.MenuName;
}

internal static class MenuGUI
{
    public static bool IsCreatingGroup(SerializedProperty groupName)
        => GetGroupState(groupName).IsCreating;

    public static bool DrawGroupSelector(Rect position, SerializedProperty groupName, Component? owner, GUIContent label)
    {
        if (groupName.hasMultipleDifferentValues)
        {
            EditorGUI.PropertyField(position, groupName, label);
            return true;
        }

        var state = GetGroupState(groupName);
        if (state.IsCreating)
        {
            groupName.stringValue = EditorGUI.DelayedTextField(position, label, groupName.stringValue);
            if (!string.IsNullOrWhiteSpace(groupName.stringValue)) state.IsCreating = false;
            return true;
        }

        var groups = GetDefinedGroupNames(owner);
        var currentIndex = groups.IndexOf(groupName.stringValue);
        var options = new GUIContent[groups.Count + 2];
        options[0] = "menu.group.none.label".LG();
        for (var i = 0; i < groups.Count; i++) options[i + 1] = new GUIContent(groups[i]);
        options[^1] = "menu.group.new.label".LG();
        var selectedIndex = currentIndex < 0 ? 0 : currentIndex + 1;
        var nextIndex = EditorGUI.Popup(position, label, selectedIndex, options);
        if (nextIndex == selectedIndex) return !string.IsNullOrWhiteSpace(groupName.stringValue);
        if (nextIndex == options.Length - 1)
        {
            groupName.stringValue = string.Empty;
            state.IsCreating = true;
            GUI.FocusControl(null);
            return true;
        }
        groupName.stringValue = nextIndex == 0 ? string.Empty : groups[nextIndex - 1];
        return nextIndex != 0;
    }

    private static GroupState GetGroupState(SerializedProperty groupName)
        => GUIState.Get(groupName, "MenuGroup", () => new GroupState());

    private sealed class GroupState
    {
        public bool IsCreating;
    }

    public static void DrawBuiltInGroup(Rect position, GUIContent label, string groupName)
    {
        using var _ = new EditorGUI.DisabledScope(true);
        EditorGUI.Popup(position, label, 0, new[] { "menu.group.directMenuReplace.label".LG() });
    }

    private static List<string> GetDefinedGroupNames(Component? owner)
    {
        var root = owner == null ? null : RuntimeUtil.FindAvatarInParents(owner.transform);
        if (root == null) return new();
        return root.GetComponentsInChildren<MenuComponent>(true)
            .Where(menu => menu.Binding == MenuComponent.ParameterBinding.GenerateGroup)
            .Select(menu => menu.GroupName)
            .Concat(root.GetComponentsInChildren<ExpressionComponent>(true)
                .Where(expression => expression.WriteMode == ExpressionWriteMode.Blend && expression.DirectMenuEnabled)
                .Select(expression => expression.DirectMenuSettings.GroupName))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }
}

[CustomPropertyDrawer(typeof(MenuIconSettings))]
internal sealed class MenuIconSettingsDrawer : PropertyDrawer
{
    private const float WarningHeight = 30f;
    private static readonly string[] ModeKeys = { "menuIcon.none.label", "menuIcon.manual.label", "menuIcon.automatic.label" };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var mode = property.FindPropertyRelative(nameof(MenuIconSettings.Mode));
        var manual = property.FindPropertyRelative(nameof(MenuIconSettings.ManualIcon));
        var preview = property.FindPropertyRelative(nameof(MenuIconSettings.PreviewExpression));
        position.SetSingleHeight();
        mode.enumValueIndex = GUIHelper.LocalizedPopup(position, mode.enumValueIndex, "menuIcon.icon.label", ModeKeys);
        if (mode.enumValueIndex == (int)MenuIconSettings.Kind.None) return;
        position.NewLine();

        if (mode.enumValueIndex == (int)MenuIconSettings.Kind.Manual)
        {
            GUIHelper.DrawPropertyWithIndentedLabel(ref position, manual, "menuIcon.manualIcon.label");
            if (manual.objectReferenceValue == null)
            {
                position.height = WarningHeight;
                position.Indent();
                EditorGUI.HelpBox(position, "menuIcon.manualIcon.empty.message".LS(), MessageType.Warning);
            }
            return;
        }

        if (preview.objectReferenceValue == null && property.serializedObject.targetObject is ExpressionComponent)
        {
            GUIHelper.DrawPlaceholderObjectLikeField(
                position,
                preview,
                "menuIcon.previewExpression.label".LG(),
                "menuIcon.currentExpression.placeholder".LG(),
                true,
                indentLabel: true);
            position.NewLine();
        }
        else
        {
            GUIHelper.DrawPropertyWithIndentedLabel(ref position, preview, "menuIcon.previewExpression.label");
        }
        if (preview.objectReferenceValue == null && property.serializedObject.targetObject is not ExpressionComponent)
        {
            position.height = WarningHeight;
            position.Indent();
            EditorGUI.HelpBox(position, "menuIcon.previewExpression.empty.message".LS(), MessageType.Warning);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var mode = property.FindPropertyRelative(nameof(MenuIconSettings.Mode)).enumValueIndex;
        if (mode == (int)MenuIconSettings.Kind.None) return GUIHelper.LineHeight;
        var height = GUIHelper.GetLinesHeight(2);
        var missing = mode == (int)MenuIconSettings.Kind.Manual
            ? property.FindPropertyRelative(nameof(MenuIconSettings.ManualIcon)).objectReferenceValue == null
            : property.serializedObject.targetObject is not ExpressionComponent
              && property.FindPropertyRelative(nameof(MenuIconSettings.PreviewExpression)).objectReferenceValue == null;
        return missing ? height + GUIHelper.VerticalSpacing + WarningHeight : height;
    }
}

[CustomPropertyDrawer(typeof(DirectMenuSettings))]
internal sealed class DirectMenuSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var menu = property.FindPropertyRelative(nameof(DirectMenuSettings.Menu));
        position.height = EditorGUI.GetPropertyHeight(menu, GUIContent.none, true);
        EditorGUI.PropertyField(position, menu, GUIContent.none, true);
        position.NewLine();
        position.height = GUIHelper.LineHeight;
        var group = property.FindPropertyRelative(nameof(DirectMenuSettings.GroupName));
        if (IsReplaceMode(property.serializedObject))
            MenuGUI.DrawBuiltInGroup(position, "menu.group.label".LG(), BuiltInMenuGroups.DirectMenuReplace);
        else
            MenuGUI.DrawGroupSelector(position, group, property.serializedObject.targetObject as Component, "menu.group.label".LG());
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(DirectMenuSettings.Menu)), GUIContent.none, true)
         + GUIHelper.VerticalSpacing
         + GUIHelper.LineHeight;

    private static bool IsReplaceMode(SerializedObject serializedObject)
    {
        var mode = serializedObject.FindProperty(nameof(ExpressionComponent.WriteMode));
        return mode != null && !mode.hasMultipleDifferentValues && mode.enumValueIndex == (int)ExpressionWriteMode.Replace;
    }
}
