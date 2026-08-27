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
        using var _ = new EditorGUI.PropertyScope(position, menuNameLabelKey.LG(), settings);
        position.height = GUIHelper.LineHeight;
        var menuName = settings.FindPropertyRelative(nameof(MenuSettings.MenuName));
        var fallbackName = owner.DestroyedAsNull()?.gameObject.name ?? string.Empty;
        GUIHelper.DrawPlaceholderTextField(
            position,
            menuName,
            menuNameLabelKey.LG(),
            new GUIContent(FaceTuneMenuResolver.GetDisplayName(menuName.stringValue, fallbackName)));
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
        var root = owner == null
            ? null
            : RuntimeUtil.FindAvatarInParents(owner.transform);
        var resolver = root == null ? null : new FaceTuneMenuResolver(root.gameObject);
        var target = resolver?.GetInstallTarget(owner!, property.objectReferenceValue as Transform);
        var folder = target?.GetComponent<MenuComponent>();
        var destination = folder == null
            ? "menuInstallSettings.root.placeholder".LS()
            : FaceTuneMenuResolver.GetDisplayName(folder.Menu.MenuName, folder.name);
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
        var root = owner == null
            ? null
            : RuntimeUtil.FindAvatarInParents(owner.transform);
        if (root == null) return new();
        return new FaceTuneMenuResolver(root.gameObject).GetDefinedGroupNames();
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
        if (mode.intValue == (int)MenuIconSettings.Kind.None) return;
        position.NewLine();

        if (mode.intValue == (int)MenuIconSettings.Kind.Manual)
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

        var previewTarget = FaceTuneMenuResolver.ResolvePreviewTarget(
            preview.objectReferenceValue as Transform,
            property.serializedObject.targetObject as Component);
        var isCurrentExpressionFallback = preview.objectReferenceValue == null
            && property.serializedObject.targetObject is ExpressionComponent
            && previewTarget != null;
        if (isCurrentExpressionFallback)
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
        if (previewTarget == null && property.serializedObject.targetObject is not ExpressionComponent)
        {
            position.height = WarningHeight;
            position.Indent();
            EditorGUI.HelpBox(position, "menuIcon.previewExpression.empty.message".LS(), MessageType.Warning);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var mode = property.FindPropertyRelative(nameof(MenuIconSettings.Mode)).intValue;
        if (mode == (int)MenuIconSettings.Kind.None) return GUIHelper.LineHeight;
        var height = GUIHelper.GetLinesHeight(2);
        var preview = property.FindPropertyRelative(nameof(MenuIconSettings.PreviewExpression));
        var previewEmpty = FaceTuneMenuResolver.ResolvePreviewTarget(
            preview.objectReferenceValue as Transform,
            property.serializedObject.targetObject as Component) == null;
        var missing = mode == (int)MenuIconSettings.Kind.Manual
            ? property.FindPropertyRelative(nameof(MenuIconSettings.ManualIcon)).objectReferenceValue == null
            : property.serializedObject.targetObject is not ExpressionComponent && previewEmpty;
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
        return mode != null && !mode.hasMultipleDifferentValues && mode.intValue == (int)ExpressionWriteMode.Replace;
    }
}
