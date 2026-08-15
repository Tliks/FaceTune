namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(MenuComponent))]
internal sealed class MenuComponentEditor : FaceTuneSectionEditorBase<MenuComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[]
        {
            CreateMenuModeSection(),
            CreateBasicSettingsSection(),
            CreateParameterSettingsSection()
        };

    private FaceTuneSection CreateMenuModeSection()
        => CreateSection("menu.mode.section.label", new MenuModeSectionDrawer(serializedObject), true);

    private FaceTuneSection CreateBasicSettingsSection()
        => CreateSection("menu.basicSettings.section.label", new MenuBasicSettingsSectionDrawer(serializedObject, Component), false);

    private FaceTuneSection CreateParameterSettingsSection()
    {
        var kind = serializedObject.FindProperty(nameof(MenuComponent.MenuKind));
        return CreateSection(
            "menu.advancedSettings.section.label",
            new MenuParameterSettingsSectionDrawer(serializedObject, Component),
            false,
            isVisible: () => kind.hasMultipleDifferentValues || kind.enumValueIndex != (int)MenuComponent.Kind.Folder);
    }
}

internal sealed class MenuModeSectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _kind;

    public MenuModeSectionDrawer(SerializedObject serializedObject)
        => _kind = serializedObject.FindProperty(nameof(MenuComponent.MenuKind));

    public float GetHeight() => GUIHelper.LineHeight;

    public void Draw(Rect position)
        => GUIHelper.LocalizedEnumPopup(
            position,
            _kind,
            "menu.kind.label",
            new[] { "menu.kind.toggle.label", "menu.kind.radial.label", "menu.kind.folder.label" });
}

internal sealed class MenuBasicSettingsSectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _menu;
    private readonly Component _owner;

    public MenuBasicSettingsSectionDrawer(SerializedObject serializedObject, Component owner)
    {
        _menu = serializedObject.FindProperty(nameof(MenuComponent.Menu));
        _owner = owner;
    }

    public float GetHeight() => MenuSettingsGUI.GetHeight(_menu);

    public void Draw(Rect position)
        => MenuSettingsGUI.Draw(position, _menu, _owner, "menu.name.label");
}

internal sealed class MenuParameterSettingsSectionDrawer : ISectionDrawer
{
    private readonly Component _owner;
    private readonly SerializedProperty _kind;
    private readonly SerializedProperty _binding;
    private readonly SerializedProperty _name;
    private readonly SerializedProperty _groupName;
    private readonly SerializedProperty _initialValue;
    private readonly SerializedProperty _selectedValue;

    public MenuParameterSettingsSectionDrawer(SerializedObject serializedObject, Component owner)
    {
        _owner = owner;
        _kind = serializedObject.FindProperty(nameof(MenuComponent.MenuKind));
        _binding = serializedObject.FindProperty(nameof(MenuComponent.Binding));
        _name = serializedObject.FindProperty(nameof(MenuComponent.Name));
        _groupName = serializedObject.FindProperty(nameof(MenuComponent.GroupName));
        _initialValue = serializedObject.FindProperty(nameof(MenuComponent.InitialValue));
        _selectedValue = serializedObject.FindProperty(nameof(MenuComponent.SelectedValue));
    }

    public float GetHeight()
    {
        var rows = 1;
        if (ShowsGroup) rows++;
        if (!IsGroup) rows++;
        if (!IsExisting) rows++;
        if (IsToggle && !IsGroup) rows++;
        return GUIHelper.GetLinesHeight(rows);
    }

    public void Draw(Rect position)
    {
        var isExisting = IsExisting;
        var isToggle = IsToggle;
        var isGroup = IsGroup;
        var showsGroup = ShowsGroup;

        position.SetSingleHeight();
        DrawBinding(position);
        position.NewLine();

        if (showsGroup)
        {
            var usesGroup = MenuGUI.DrawGroupSelector(position, _groupName, _owner, "menu.group.label".LG());
            if (!_binding.hasMultipleDifferentValues
                && _binding.enumValueIndex != (int)MenuComponent.ParameterBinding.Existing
                && !_groupName.hasMultipleDifferentValues)
                _binding.enumValueIndex = string.IsNullOrWhiteSpace(_groupName.stringValue)
                    ? (int)MenuComponent.ParameterBinding.Generate
                    : (int)MenuComponent.ParameterBinding.GenerateGroup;
            isGroup = usesGroup
                   && !_binding.hasMultipleDifferentValues
                   && _binding.enumValueIndex != (int)MenuComponent.ParameterBinding.Existing;
            position.NewLine();
        }

        if (!isGroup)
        {
            GUIHelper.DrawPlaceholderTextField(
                position,
                _name,
                "menu.parameterName.label".LG(),
                isExisting ? GUIContent.none : "menu.parameterName.auto.placeholder".LG());
            position.NewLine();
        }

        if (!isExisting)
        {
            if (isToggle)
                DrawFloatToggle(position, _initialValue, "menu.defaultSelected.label");
            else
                EditorGUI.PropertyField(position, _initialValue, "menu.floatDefaultValue.label".LG());
            position.NewLine();
        }

        if (isToggle && !isGroup)
            DrawFloatToggle(position, _selectedValue, "menu.selectedValue.label");
    }

    private void DrawBinding(Rect position)
    {
        using var scope = new EditorGUI.PropertyScope(position, "menu.binding.label".LG(), _binding);
        var previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = _binding.hasMultipleDifferentValues;
        var displayed = IsExisting ? 1 : 0;
        EditorGUI.BeginChangeCheck();
        var next = GUIHelper.LocalizedPopup(
            position,
            displayed,
            "menu.binding.label",
            new[] { "menu.binding.generate.label", "menu.binding.existing.label" });
        if (EditorGUI.EndChangeCheck())
            _binding.enumValueIndex = next == 1
                ? (int)MenuComponent.ParameterBinding.Existing
                : string.IsNullOrWhiteSpace(_groupName.stringValue)
                    ? (int)MenuComponent.ParameterBinding.Generate
                    : (int)MenuComponent.ParameterBinding.GenerateGroup;
        EditorGUI.showMixedValue = previousMixed;
    }

    private static void DrawFloatToggle(Rect position, SerializedProperty property, string labelKey)
    {
        using var scope = new EditorGUI.PropertyScope(position, labelKey.LG(), property);
        var previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        var value = !Mathf.Approximately(property.floatValue, 0f);
        EditorGUI.BeginChangeCheck();
        var next = EditorGUI.Toggle(position, scope.content, value);
        if (EditorGUI.EndChangeCheck()) property.floatValue = next ? 1f : 0f;
        EditorGUI.showMixedValue = previousMixed;
    }

    private bool IsToggle => _kind.hasMultipleDifferentValues || _kind.enumValueIndex == (int)MenuComponent.Kind.Toggle;
    private bool ShowsGroup => !IsExisting && IsToggle;
    private bool IsGroup => !IsExisting
                         && (!_binding.hasMultipleDifferentValues
                             && _binding.enumValueIndex == (int)MenuComponent.ParameterBinding.GenerateGroup
                             || MenuGUI.IsCreatingGroup(_groupName));
    private bool IsExisting => !_binding.hasMultipleDifferentValues && _binding.enumValueIndex == (int)MenuComponent.ParameterBinding.Existing;
}
