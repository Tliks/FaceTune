namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(SettingsComponent))]
internal sealed class SettingsComponentEditor : FaceTuneSectionEditorBase<SettingsComponent>
{
    private SettingEntry[]? _settings;

    protected override IReadOnlyList<FaceTuneSection> CreateSections()
    {
        var settings = Settings;
        var sections = new FaceTuneSection[settings.Length];
        for (var i = 0; i < settings.Length; i++)
        {
            var setting = settings[i];
            sections[i] = CreateSection(
                setting.LabelKey,
                setting.Drawer,
                false,
                populateHeaderMenu: menu => PopulateRemoveMenu(menu, setting.Enabled),
                isVisible: () => IsEnabled(setting.Enabled),
                spacingGroup: setting.SpacingGroup);
        }
        return sections;
    }

    protected override float GetFooterHeight() => GUIHelper.LineHeight;

    protected override void DrawFooter(Rect position)
    {
        position.SetSingleHeight();
        using var availableSettings = ListPool<SettingEntry>.Get(out var available);
        foreach (var setting in Settings)
        {
            if (!IsEnabled(setting.Enabled)) available.Add(setting);
        }

        var button = EditorGUI.PrefixLabel(position, "settings.add.label".LG());
        using var disabled = new EditorGUI.DisabledScope(available.Count == 0);
        if (!EditorGUI.DropdownButton(button, "settings.add.option.select".LG(), FocusType.Keyboard, EditorStyles.popup)) return;

        var menu = new GenericMenu();
        foreach (var setting in available)
        {
            var selectedSetting = setting;
            menu.AddItem(setting.LabelKey.LG(), false, () => EnableSetting(selectedSetting));
        }
        menu.DropDown(button);
    }

    private void EnableSetting(SettingEntry setting)
    {
        serializedObject.UpdateIfRequiredOrScript();
        setting.Value.CopyFrom(setting.CreateDefault());
        setting.Enabled.boolValue = true;
        serializedObject.ApplyModifiedProperties();
    }

    private SettingEntry[] Settings => _settings ??= new[]
    {
        CreateSetting(
            nameof(SettingsComponent.HasFacialBlendShapes),
            nameof(SettingsComponent.FacialBlendShapes),
            () => new FacialBlendShapeDataSource(),
            "settings.configureFacial.section.label",
            new SettingsFacialSectionDrawer(serializedObject, Component, targets.Length),
            0),
        CreateSetting(
            nameof(SettingsComponent.HasEyeBlink),
            nameof(SettingsComponent.EyeBlink),
            () => new EyeBlinkSettingsSource(),
            "settings.eyeBlink.section.label",
            new SettingsSourceSectionDrawer(serializedObject.FindProperty(nameof(SettingsComponent.EyeBlink))),
            1),
        CreateSetting(
            nameof(SettingsComponent.HasLipSync),
            nameof(SettingsComponent.LipSync),
            () => new LipSyncSettingsSource(),
            "settings.lipSync.section.label",
            new SettingsSourceSectionDrawer(serializedObject.FindProperty(nameof(SettingsComponent.LipSync))),
            1),
        CreateSetting(
            nameof(SettingsComponent.ExpressionSetEnabled),
            nameof(SettingsComponent.ExpressionSet),
            () => new ExpressionSetSettings(),
            "settings.expressionSet.section.label",
            Property(nameof(SettingsComponent.ExpressionSet)),
            2),
        CreateSetting(
            nameof(SettingsComponent.HasCondition),
            nameof(SettingsComponent.Condition),
            () => new Condition(new ConditionCase()),
            "settings.addCondition.section.label",
            Property(nameof(SettingsComponent.Condition)),
            2),
        CreateSetting(
            nameof(SettingsComponent.HasTransition),
            nameof(SettingsComponent.Transition),
            () => new TransitionSettings(),
            "settings.transition.section.label",
            Property(nameof(SettingsComponent.Transition)),
            3),
        CreateSetting(
            nameof(SettingsComponent.HasPriority),
            nameof(SettingsComponent.Priority),
            () => new PrioritySettings(),
            "settings.priority.section.label",
            Property(nameof(SettingsComponent.Priority)),
            3)
    };

    private PropertiesSectionDrawer Property(string propertyName)
        => new(new PropertiesSectionDrawer.Entry(serializedObject.FindProperty(propertyName), null));

    private SettingEntry CreateSetting(
        string enabledPropertyName,
        string valuePropertyName,
        Func<object> createDefault,
        string labelKey,
        ISectionDrawer drawer,
        int spacingGroup)
        => new(
            serializedObject.FindProperty(enabledPropertyName),
            serializedObject.FindProperty(valuePropertyName),
            createDefault,
            labelKey,
            drawer,
            spacingGroup);

    private void PopulateRemoveMenu(GenericMenu menu, SerializedProperty enabled)
        => menu.AddItem("settings.remove.label".LG(), false, () =>
        {
            serializedObject.UpdateIfRequiredOrScript();
            enabled.boolValue = false;
            serializedObject.ApplyModifiedProperties();
        });

    private static bool IsEnabled(SerializedProperty property)
        => property.boolValue || property.hasMultipleDifferentValues;

    private sealed record SettingEntry(
        SerializedProperty Enabled,
        SerializedProperty Value,
        Func<object> CreateDefault,
        string LabelKey,
        ISectionDrawer Drawer,
        int SpacingGroup);
}

internal sealed class SettingsFacialSectionDrawer : ISectionDrawer, ISectionHeaderDrawer
{
    private readonly FacialDataSectionDrawer _expression;
    private readonly SerializedProperty _applyToRenderer;
    private readonly FoldoutState _additionalSettings = new(false);

    public SettingsFacialSectionDrawer(SerializedObject serializedObject, Component component, int targetCount)
    {
        _expression = new FacialDataSectionDrawer(serializedObject, component, targetCount, nameof(SettingsComponent.FacialBlendShapes));
        _applyToRenderer = serializedObject.FindProperty(nameof(SettingsComponent.ApplyToRenderer));
    }

    public float GetHeight()
        => _expression.GetHeight()
         + GUIHelper.VerticalSpacing
         + GUIHelper.GetShurikenSectionHeight(_additionalSettings, GUIHelper.LineHeight);

    public float GetHeaderWidth() => _expression.GetHeaderWidth();
    public void DrawHeader(Rect position) => _expression.DrawHeader(position);

    public void Draw(Rect position)
    {
        position.height = _expression.GetHeight();
        _expression.Draw(position);
        position.y += position.height + GUIHelper.VerticalSpacing;
        position.height = GUIHelper.GetShurikenSectionHeight(_additionalSettings, GUIHelper.LineHeight);
        if (GUIHelper.DrawShurikenSection(
                position,
                _additionalSettings,
                "common.options.section.label".LG(),
                GUIHelper.LineHeight,
                out var content))
        {
            content.height = GUIHelper.LineHeight;
            GUIHelper.DrawToggleLeft(content, _applyToRenderer, "settings.applyToRenderer.label".LG());
        }
    }
}
