namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(MenuComponent))]
internal sealed class MenuComponentEditor : FaceTuneSectionEditor<MenuComponent>
{
    private bool _parameterSettingsExpanded;

    protected override bool DefaultExpanded => false;
    protected override GUIContent SectionLabel => "menu.section.label".LG();

    protected override float GetSectionContentHeight()
    {
        var height = GetPropertyHeight(nameof(MenuComponent.MenuName))
                   + GetPropertyHeight(nameof(MenuComponent.Icon))
                   + GetPropertyHeight(nameof(MenuComponent.InstallSettings))
                   + 10f
                   + GetPropertyHeight(nameof(MenuComponent.Kind))
                   + GUIHelper.LineHeight;
        if (!_parameterSettingsExpanded) return height;

        var kind = serializedObject.FindProperty(nameof(MenuComponent.Kind));
        var group = serializedObject
            .FindProperty(nameof(MenuComponent.ExclusiveToggleGroup))
            .FindPropertyRelative(nameof(ExclusiveToggleGroup.GroupName));
        var isToggle = kind.hasMultipleDifferentValues || IsMode(kind, (int)MenuItemKind.Toggle);
        var isFloat = kind.hasMultipleDifferentValues || IsMode(kind, (int)MenuItemKind.Radial);
        var parameterVisible = !IsMode(kind, (int)MenuItemKind.Toggle)
                            || group.hasMultipleDifferentValues
                            || string.IsNullOrWhiteSpace(group.stringValue);

        height += GUIHelper.VerticalSpacing;
        if (isToggle) height += GetPropertyHeight(nameof(MenuComponent.ExclusiveToggleGroup));
        if (parameterVisible) height += GetPropertyHeight(nameof(MenuComponent.ParameterName));
        if (isFloat) height += GetPropertyHeight(nameof(MenuComponent.FloatDefaultValue));
        if (isToggle) height += GetPropertyHeight(nameof(MenuComponent.DefaultSelected));
        return height - GUIHelper.VerticalSpacing;
    }

    protected override void DrawSectionContent(Rect position)
    {
        var menuName = serializedObject.FindProperty(nameof(MenuComponent.MenuName));
        MenuGUI.DrawMenuName(ref position, menuName, Component, "menu.name.label".LG());
        GUIHelper.DrawProperty(ref position, serializedObject.FindProperty(nameof(MenuComponent.Icon)), "menu.icon.label");
        GUIHelper.DrawProperty(ref position, serializedObject.FindProperty(nameof(MenuComponent.InstallSettings)), "menu.destination.label");

        position.y += 10f;
        GUIHelper.DrawProperty(ref position, serializedObject.FindProperty(nameof(MenuComponent.Kind)), "menu.mode.label");
        position.height = GUIHelper.LineHeight;
        _parameterSettingsExpanded = GUIHelper.DrawFoldout(
            position,
            _parameterSettingsExpanded,
            "menu.options.label".LG());
        if (!_parameterSettingsExpanded) return;

        position.NewLine();
        position.Indent();
        var kind = serializedObject.FindProperty(nameof(MenuComponent.Kind));
        var group = serializedObject.FindProperty(nameof(MenuComponent.ExclusiveToggleGroup));
        var groupName = group.FindPropertyRelative(nameof(ExclusiveToggleGroup.GroupName));
        var isToggle = kind.hasMultipleDifferentValues || IsMode(kind, (int)MenuItemKind.Toggle);
        var isFloat = kind.hasMultipleDifferentValues || IsMode(kind, (int)MenuItemKind.Radial);
        if (isToggle) GUIHelper.DrawProperty(ref position, group, "menu.group.label");
        if (!IsMode(kind, (int)MenuItemKind.Toggle)
            || groupName.hasMultipleDifferentValues
            || string.IsNullOrWhiteSpace(groupName.stringValue))
        {
            var parameter = serializedObject.FindProperty(nameof(MenuComponent.ParameterName));
            GUIHelper.DrawPlaceholderTextField(
                position,
                parameter,
                "menu.parameterName.label".LG(),
                "menu.parameterName.auto.placeholder".LG());
            position.NewLine();
        }
        if (isFloat)
            GUIHelper.DrawProperty(ref position, serializedObject.FindProperty(nameof(MenuComponent.FloatDefaultValue)), "menu.floatDefaultValue.label");
        if (isToggle)
            GUIHelper.DrawProperty(ref position, serializedObject.FindProperty(nameof(MenuComponent.DefaultSelected)), "menu.defaultSelected.label");
    }
}
