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
            new GUIContent(owner.DestroyedAsNull()?.gameObject.name ?? string.Empty));
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
        var owner = (property.serializedObject.targetObject as Component).DestroyedAsNull();
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
        owner = owner.DestroyedAsNull();
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
    private static void BeginGroupCreation(SerializedProperty groupName)
    {
        groupName.stringValue = string.Empty;
        var state = GetGroupState(groupName);
        state.IsCreating = true;
        state.FocusRequested = true;
    }

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
            GUI.SetNextControlName(state.ControlName);
            groupName.stringValue = EditorGUI.DelayedTextField(position, label, groupName.stringValue);
            if (!string.IsNullOrWhiteSpace(groupName.stringValue)) state.IsCreating = false;

            var current = Event.current;
            if (current.type == EventType.KeyDown
                && current.keyCode == KeyCode.Escape
                && GUI.GetNameOfFocusedControl() == state.ControlName)
            {
                groupName.stringValue = string.Empty;
                state.IsCreating = false;
                current.Use();
            }
            else if (state.FocusRequested && current.type == EventType.Repaint)
            {
                EditorGUI.FocusTextInControl(state.ControlName);
                state.FocusRequested = false;
            }
            else if (!state.FocusRequested
                     && current.type == EventType.Repaint
                     && GUI.GetNameOfFocusedControl() != state.ControlName
                     && string.IsNullOrWhiteSpace(groupName.stringValue))
            {
                state.IsCreating = false;
            }
            return state.IsCreating || !string.IsNullOrWhiteSpace(groupName.stringValue);
        }

        var groups = GetDefinedGroupNames(owner);
        if (!string.IsNullOrWhiteSpace(groupName.stringValue)
            && !groups.Contains(groupName.stringValue, StringComparer.Ordinal))
            groups.Add(groupName.stringValue);
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
            BeginGroupCreation(groupName);
            return true;
        }
        groupName.stringValue = nextIndex == 0 ? string.Empty : groups[nextIndex - 1];
        return nextIndex != 0;
    }

    private static GroupState GetGroupState(SerializedProperty groupName)
        => GUIState.Get(groupName, "MenuGroup", () => new GroupState());

    private sealed class GroupState
    {
        public readonly string ControlName = $"FaceTuneGroup_{Guid.NewGuid():N}";
        public bool IsCreating;
        public bool FocusRequested;
    }

    public static void DrawBuiltInGroup(Rect position, GUIContent label, string groupName)
    {
        using var _ = new EditorGUI.DisabledScope(true);
        EditorGUI.Popup(position, label, 0, new[] { "menu.group.directMenuReplace.label".LG() });
    }

    private static List<string> GetDefinedGroupNames(Component? owner)
    {
        owner = owner.DestroyedAsNull();
        var root = owner == null
            ? null
            : RuntimeUtil.FindAvatarInParents(owner.transform).DestroyedAsNull();
        if (root == null) return new();
        return root.GetComponentsInChildren<MenuComponent>(true)
            .Where(menu => !menu.UseExistingParameter
                        && menu.GenerateParameterGroup
                        && !string.IsNullOrWhiteSpace(menu.GroupName))
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
        var advanced = GUIState.Foldout(property, "DirectMenuAdvancedSettings");

        position.height = EditorGUI.GetPropertyHeight(menu, GUIContent.none, true);
        EditorGUI.PropertyField(position, menu, GUIContent.none, true);
        position.y = position.yMax + GUIHelper.VerticalSpacing;
        position.SetSingleHeight();
        advanced.Expanded = GUIHelper.DrawFoldout(
            position,
            advanced.Expanded,
            "menu.advancedSettings.section.label".LG());
        if (!advanced.Expanded) return;

        position.NewLine();
        position.Indent();
        var group = property.FindPropertyRelative(nameof(DirectMenuSettings.GroupName));
        if (IsReplaceMode(property.serializedObject))
            MenuGUI.DrawBuiltInGroup(position, "menu.group.label".LG(), BuiltInMenuGroups.DirectMenuReplace);
        else
            MenuGUI.DrawGroupSelector(
                position,
                group,
                property.serializedObject.targetObject as Component,
                "menu.group.label".LG());
        position.NewLine();
        EditorGUI.PropertyField(
            position,
            property.FindPropertyRelative(nameof(DirectMenuSettings.PriorityOffset)),
            "directMenu.priorityOffset.label".LG());
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var advanced = GUIState.Foldout(property, "DirectMenuAdvancedSettings");
        return EditorGUI.GetPropertyHeight(
                   property.FindPropertyRelative(nameof(DirectMenuSettings.Menu)),
                   GUIContent.none,
                   true)
             + GUIHelper.VerticalSpacing + GUIHelper.LineHeight
             + (advanced.Expanded
                 ? GUIHelper.VerticalSpacing + GUIHelper.GetLinesHeight(2)
                 : 0f);
    }

    private static bool IsReplaceMode(SerializedObject serializedObject)
    {
        var mode = serializedObject.FindProperty(nameof(ExpressionComponent.WriteMode));
        return mode != null && !mode.hasMultipleDifferentValues && mode.enumValueIndex == (int)ExpressionWriteMode.Replace;
    }
}
