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
            isVisible: () => kind.hasMultipleDifferentValues || kind.intValue != (int)MenuComponent.Kind.Folder);
    }
}

internal sealed class MenuModeSectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _kind;

    public MenuModeSectionDrawer(SerializedObject serializedObject)
    {
        _kind = serializedObject.FindProperty(nameof(MenuComponent.MenuKind));
        Actions = new SectionActionSet(
            serializedObject,
            new[] { SectionActionField.From(
                _kind,
                () => MenuComponent.DefaultMenuKind) });
    }

    public SectionActionSet Actions { get; }

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
        Actions = new SectionActionSet(
            serializedObject,
            new[] { SectionActionField.From(_menu, () => new MenuSettings()) });
    }

    public SectionActionSet Actions { get; }

    public float GetHeight() => MenuSettingsGUI.GetHeight(_menu);

    public void Draw(Rect position)
        => MenuSettingsGUI.Draw(position, _menu, _owner, "menu.name.label");
}

internal sealed class MenuParameterSettingsSectionDrawer : ISectionDrawer
{
    private readonly Component _owner;
    private readonly SerializedProperty _kind;
    private readonly SerializedProperty _useExistingParameter;
    private readonly SerializedProperty _generateParameterGroup;
    private readonly SerializedProperty _parameterName;
    private readonly SerializedProperty _groupName;
    private readonly SerializedProperty _synced;
    private readonly SerializedProperty _saved;
    private readonly SerializedProperty _initialValue;
    private readonly SerializedProperty _selectedValue;

    public MenuParameterSettingsSectionDrawer(SerializedObject serializedObject, Component owner)
    {
        _owner = owner;
        _kind = serializedObject.FindProperty(nameof(MenuComponent.MenuKind));
        _useExistingParameter = serializedObject.FindProperty(nameof(MenuComponent.UseExistingParameter));
        _generateParameterGroup = serializedObject.FindProperty(nameof(MenuComponent.GenerateParameterGroup));
        _parameterName = serializedObject.FindProperty(nameof(MenuComponent.ParameterName));
        _groupName = serializedObject.FindProperty(nameof(MenuComponent.GroupName));
        _synced = serializedObject.FindProperty(nameof(MenuComponent.Synced));
        _saved = serializedObject.FindProperty(nameof(MenuComponent.Saved));
        _initialValue = serializedObject.FindProperty(nameof(MenuComponent.DefaultValue));
        _selectedValue = serializedObject.FindProperty(nameof(MenuComponent.SelectedValue));
        Actions = new SectionActionSet(
            serializedObject,
            new[]
            {
                SectionActionField.From(_useExistingParameter, () => MenuComponent.DefaultUseExistingParameter),
                SectionActionField.From(_generateParameterGroup, () => MenuComponent.DefaultGenerateParameterGroup),
                SectionActionField.From(_parameterName, () => MenuComponent.DefaultParameterName),
                SectionActionField.From(_groupName, () => MenuComponent.DefaultGroupName),
                SectionActionField.From(_synced, () => MenuComponent.DefaultSynced),
                SectionActionField.From(_saved, () => MenuComponent.DefaultSaved),
                SectionActionField.From(_initialValue, () => MenuComponent.DefaultParameterValue),
                SectionActionField.From(_selectedValue, () => MenuComponent.DefaultSelectedValue)
            });
    }

    public SectionActionSet Actions { get; }

    public float GetHeight()
    {
        var rows = 1;
        if (!IsExisting && IsToggle) rows++;
        if (!IsGroup) rows++;
        if (!IsExisting) rows += IsGroup ? 1 : 3;
        if (IsToggle && !IsGroup) rows++;
        return GUIHelper.GetLinesHeight(rows);
    }

    public void Draw(Rect position)
    {
        if (!IsToggle && !_generateParameterGroup.hasMultipleDifferentValues)
            _generateParameterGroup.boolValue = false;

        position.SetSingleHeight();
        if (DrawBinding(position)) return;
        position.NewLine();

        var isExisting = IsExisting;
        var isToggle = IsToggle;
        var isGroup = IsGroup;
        if (!isExisting && isToggle)
        {
            if (!isGroup && !_groupName.hasMultipleDifferentValues)
                _groupName.stringValue = string.Empty;
            var usesGroup = MenuGUI.DrawGroupSelector(
                position,
                _groupName,
                _owner,
                "menu.group.label".LG());
            if (usesGroup != isGroup)
            {
                _generateParameterGroup.boolValue = usesGroup;
                return;
            }
            position.NewLine();
        }

        if (!isGroup)
        {
            GUIHelper.DrawPlaceholderTextField(
                position,
                _parameterName,
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
            if (!isGroup)
            {
                EditorGUI.PropertyField(position, _synced, "menu.parameterSynced.label".LG());
                position.NewLine();
                EditorGUI.PropertyField(position, _saved, "menu.parameterSaved.label".LG());
                position.NewLine();
            }
        }

        if (isToggle && !isGroup)
            DrawFloatToggle(position, _selectedValue, "menu.selectedValue.label");
    }

    private bool DrawBinding(Rect position)
    {
        using var _ = new EditorGUI.PropertyScope(position, "menu.binding.label".LG(), _useExistingParameter);
        using var rightClick = new GUIHelper.RightClickPassthroughScope(position);
        var previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = _useExistingParameter.hasMultipleDifferentValues;
        var selected = _useExistingParameter.boolValue ? 1 : 0;
        var next = GUIHelper.LocalizedPopup(
            position,
            selected,
            "menu.binding.label",
            new[] { "menu.binding.generate.label", "menu.binding.existing.label" });
        var changed = next != selected;
        if (changed)
        {
            _useExistingParameter.boolValue = next == 1;
            if (_useExistingParameter.boolValue) _generateParameterGroup.boolValue = false;
        }
        EditorGUI.showMixedValue = previousMixed;
        return changed;
    }

    private static void DrawFloatToggle(Rect position, SerializedProperty property, string labelKey)
    {
        using var scope = new EditorGUI.PropertyScope(position, labelKey.LG(), property);
        using var rightClick = new GUIHelper.RightClickPassthroughScope(position);
        var previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        var value = !Mathf.Approximately(property.floatValue, 0f);
        EditorGUI.BeginChangeCheck();
        var next = EditorGUI.Toggle(position, scope.content, value);
        if (EditorGUI.EndChangeCheck()) property.floatValue = next ? 1f : 0f;
        EditorGUI.showMixedValue = previousMixed;
    }

    private bool IsToggle => _kind.hasMultipleDifferentValues || _kind.intValue == (int)MenuComponent.Kind.Toggle;
    private bool IsGroup => IsToggle
                         && !IsExisting
                         && (_generateParameterGroup.hasMultipleDifferentValues
                             || _generateParameterGroup.boolValue);
    private bool IsExisting => !_useExistingParameter.hasMultipleDifferentValues
                            && _useExistingParameter.boolValue;
}
