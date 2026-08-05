namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(MenuComponent))]
internal sealed class MenuComponentEditor : FaceTuneSectionEditorBase<MenuComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateMenuSection() };

    private FaceTuneSection CreateMenuSection()
        => CreateSection(
            "menu.section.label",
            new MenuSectionDrawer(serializedObject),
            defaultExpanded: false);
}

internal sealed class MenuSectionDrawer : ISectionDrawer
{
    private const float ModeSpacing = 10f;

    private readonly PropertiesSectionDrawer _menuSettings;
    private readonly SerializedProperty _kind;
    private readonly SerializedProperty _group;
    private readonly SerializedProperty _parameter;
    private readonly SerializedProperty _floatDefaultValue;
    private readonly SerializedProperty _defaultSelected;
    private bool _parameterSettingsExpanded;

    public MenuSectionDrawer(SerializedObject serializedObject)
    {
        _menuSettings = new PropertiesSectionDrawer(
            new PropertiesSectionDrawer.Entry(
                serializedObject.FindProperty(nameof(MenuComponent.Menu)),
                new MenuSettings()));
        _kind = serializedObject.FindProperty(nameof(MenuComponent.Kind));
        _group = serializedObject.FindProperty(nameof(MenuComponent.ExclusiveToggleGroup));
        _parameter = serializedObject.FindProperty(nameof(MenuComponent.ParameterName));
        _floatDefaultValue = serializedObject.FindProperty(nameof(MenuComponent.FloatDefaultValue));
        _defaultSelected = serializedObject.FindProperty(nameof(MenuComponent.DefaultSelected));
    }

    public float GetHeight()
    {
        var height = _menuSettings.GetHeight() + ModeSpacing + GUIHelper.PropertyHeight(_kind) + GUIHelper.LineHeight;
        if (!_parameterSettingsExpanded) return height;

        var groupName = _group.FindPropertyRelative(nameof(ExclusiveToggleGroup.GroupName));
        var isToggle = IsMode((int)MenuItemKind.Toggle);
        var isFloat = IsMode((int)MenuItemKind.Radial);
        var parameterVisible = !IsMode((int)MenuItemKind.Toggle) || groupName.hasMultipleDifferentValues || string.IsNullOrWhiteSpace(groupName.stringValue);
        var childHeight = 0f;
        var childCount = 0;
        if (isToggle) { childHeight += EditorGUI.GetPropertyHeight(_group, true); childCount++; }
        if (parameterVisible) { childHeight += GUIHelper.LineHeight; childCount++; }
        if (isFloat) { childHeight += EditorGUI.GetPropertyHeight(_floatDefaultValue, true); childCount++; }
        if (isToggle) { childHeight += EditorGUI.GetPropertyHeight(_defaultSelected, true); childCount++; }
        return height + GUIHelper.VerticalSpacing + childHeight + GUIHelper.VerticalSpacing * Mathf.Max(0, childCount - 1);
    }

    public void Draw(Rect position)
    {
        _menuSettings.Draw(position);
        position.y += _menuSettings.GetHeight() + ModeSpacing;
        GUIHelper.DrawProperty(ref position, _kind, "menu.mode.label");
        position.height = GUIHelper.LineHeight;
        _parameterSettingsExpanded = GUIHelper.DrawFoldout(position, _parameterSettingsExpanded, "menu.options.label".LG());
        if (!_parameterSettingsExpanded) return;

        position.NewLine();
        var groupName = _group.FindPropertyRelative(nameof(ExclusiveToggleGroup.GroupName));
        var isToggle = IsMode((int)MenuItemKind.Toggle);
        var isFloat = IsMode((int)MenuItemKind.Radial);
        if (isToggle) GUIHelper.DrawPropertyWithIndentedLabel(ref position, _group, "menu.group.label");
        if (!IsMode((int)MenuItemKind.Toggle) || groupName.hasMultipleDifferentValues || string.IsNullOrWhiteSpace(groupName.stringValue))
        {
            GUIHelper.DrawPlaceholderTextField(
                position,
                _parameter,
                "menu.parameterName.label".LG(),
                "menu.parameterName.auto.placeholder".LG(),
                indentLabel: true);
            position.NewLine();
        }
        if (isFloat) GUIHelper.DrawPropertyWithIndentedLabel(ref position, _floatDefaultValue, "menu.floatDefaultValue.label");
        if (isToggle) GUIHelper.DrawPropertyWithIndentedLabel(ref position, _defaultSelected, "menu.defaultSelected.label");
    }

    public void Reset()
    {
        _menuSettings.Reset();
        _kind.CopyFrom(MenuItemKind.Toggle);
        _group.CopyFrom(new ExclusiveToggleGroup());
        _parameter.CopyFrom(string.Empty);
        _floatDefaultValue.CopyFrom(0f);
        _defaultSelected.CopyFrom(false);
        _parameterSettingsExpanded = false;
    }

    private bool IsMode(int value) => _kind.hasMultipleDifferentValues || _kind.enumValueIndex == value;
}
