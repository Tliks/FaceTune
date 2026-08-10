using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune.Gui;

internal static class MenuSettingsGUI
{
    public static float GetHeight(SerializedProperty settings)
        => GUIHelper.PropertyHeight(settings.FindPropertyRelative(nameof(MenuSettings.MenuName)))
         + GUIHelper.PropertyHeight(settings.FindPropertyRelative(nameof(MenuSettings.Icon)))
         + GUIHelper.PropertyHeight(settings.FindPropertyRelative(nameof(MenuSettings.InstallContainer)));

    public static void Draw(Rect position, SerializedProperty settings, Component? owner, string menuNameLabelKey)
    {
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
        => MenuSettingsGUI.Draw(position, property, property.serializedObject.targetObject as Component, "menu.name.label");

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => MenuSettingsGUI.GetHeight(property);
}

internal static class MenuGUI
{
    private static readonly Dictionary<string, bool> NewGroupNameEditingByProperty = new();

    public static void DrawGroupSelector(Rect position, SerializedProperty groupName, Component? owner, GUIContent label)
    {
        if (groupName.hasMultipleDifferentValues)
        {
            EditorGUI.PropertyField(position, groupName, label);
            return;
        }

        var key = $"{groupName.serializedObject.targetObject.GetInstanceID()}:{groupName.propertyPath}";
        if (NewGroupNameEditingByProperty.ContainsKey(key))
        {
            groupName.stringValue = EditorGUI.DelayedTextField(position, label, groupName.stringValue);
            if (!string.IsNullOrWhiteSpace(groupName.stringValue)) NewGroupNameEditingByProperty.Remove(key);
            return;
        }

        var groups = GetDefinedGroupNames(owner);
        var index = groups.IndexOf(groupName.stringValue);
        var options = groups.Select(name => new GUIContent(name)).Append(new GUIContent("Create New...")).ToArray();
        var selected = EditorGUI.Popup(position, label, index < 0 ? 0 : index, options);
        if (selected == options.Length - 1)
        {
            groupName.stringValue = string.Empty;
            NewGroupNameEditingByProperty[key] = true;
        }
        else if (groups.Count != 0)
        {
            groupName.stringValue = groups[selected];
        }
    }

    private static List<string> GetDefinedGroupNames(Component? owner)
    {
        var root = owner == null ? null : RuntimeUtil.FindAvatarInParents(owner.transform);
        if (root == null) return new();
        return root.GetComponentsInChildren<MenuComponent>(true)
            .Where(menu => menu.Binding == MenuComponent.ParameterBinding.GenerateGroup)
            .Select(menu => menu.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }
}

[CustomPropertyDrawer(typeof(MenuIconSettings))]
internal sealed class MenuIconSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var mode = property.FindPropertyRelative(nameof(MenuIconSettings.Mode));
        var manual = property.FindPropertyRelative(nameof(MenuIconSettings.ManualIcon));
        var preview = property.FindPropertyRelative(nameof(MenuIconSettings.PreviewExpression));
        EditorGUI.PropertyField(position, mode, label);
        if (mode.enumValueIndex == (int)MenuIconSettings.Kind.Manual)
        {
            position.NewLine();
            EditorGUI.PropertyField(position, manual);
        }
        else if (mode.enumValueIndex == (int)MenuIconSettings.Kind.ExpressionPreview)
        {
            position.NewLine();
            EditorGUI.PropertyField(position, preview);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => property.FindPropertyRelative(nameof(MenuIconSettings.Mode)).enumValueIndex == (int)MenuIconSettings.Kind.None
            ? GUIHelper.LineHeight
            : GUIHelper.LineHeight * 2f + GUIHelper.VerticalSpacing;
}

[CustomPropertyDrawer(typeof(DirectMenuSettings))]
internal sealed class DirectMenuSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var menu = property.FindPropertyRelative(nameof(DirectMenuSettings.Menu));
        EditorGUI.PropertyField(position, menu, true);
        position.y += EditorGUI.GetPropertyHeight(menu, true);
        EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(DirectMenuSettings.GroupName)));
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var menu = property.FindPropertyRelative(nameof(DirectMenuSettings.Menu));
        return EditorGUI.GetPropertyHeight(menu, true)
             + GUIHelper.VerticalSpacing
             + GUIHelper.LineHeight;
    }
}
