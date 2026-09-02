namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(ExpressionDataComponent))]
internal sealed class ExpressionDataComponentEditor : FaceTuneSectionEditorBase<ExpressionDataComponent>
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
                setting.DefaultExpanded,
                populateHeaderMenu: menu => PopulateHeaderMenu(menu, setting),
                isVisible: () => IsEnabled(setting.Enabled));
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
        if (!EditorGUI.DropdownButton(
                button,
                "settings.add.option.select".LG(),
                FocusType.Keyboard,
                EditorStyles.popup)) return;

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
        SectionOperations.ResetValues(setting.Drawer.Actions);
        setting.Enabled.boolValue = true;
        serializedObject.ApplyModifiedProperties();
    }

    private void PopulateHeaderMenu(GenericMenu menu, SettingEntry setting)
        => menu.AddItem("settings.remove.label".LG(), false, () =>
        {
            serializedObject.UpdateIfRequiredOrScript();
            setting.Enabled.boolValue = false;
            serializedObject.ApplyModifiedProperties();
        });

    private static bool IsEnabled(SerializedProperty property)
        => property.boolValue || property.hasMultipleDifferentValues;

    private SettingEntry[] Settings => _settings ??= new[]
    {
        new SettingEntry(
            serializedObject.FindProperty(nameof(ExpressionDataComponent.HasFacialBlendShapes)),
            "expression.section.label",
            new ExpressionDataFacialSectionDrawer(serializedObject),
            true),
        new SettingEntry(
            serializedObject.FindProperty(nameof(ExpressionDataComponent.HasFacialBehavior)),
            "expression.behavior.section.label",
            new ExpressionDataBehaviorSectionDrawer(serializedObject),
            true),
        new SettingEntry(
            serializedObject.FindProperty(nameof(ExpressionDataComponent.HasMultiFrame)),
            "expression.multiFrame.section.label",
            new OptionalPropertySectionDrawer(
                serializedObject.FindProperty(nameof(ExpressionDataComponent.HasMultiFrame)),
                serializedObject.FindProperty(nameof(ExpressionDataComponent.MultiFrame)),
                () => new MultiFrameSettings()),
            false),
        new SettingEntry(
            serializedObject.FindProperty(nameof(ExpressionDataComponent.HasEyeBlink)),
            "eyeBlink.section.label",
            new OptionalReferenceableSettingsSectionDrawer(
                serializedObject,
                nameof(ExpressionDataComponent.HasEyeBlink),
                nameof(ExpressionDataComponent.EyeBlinkReference),
                nameof(ExpressionDataComponent.EyeBlink),
                () => new EyeBlinkSettings()),
            false),
        new SettingEntry(
            serializedObject.FindProperty(nameof(ExpressionDataComponent.HasLipSync)),
            "lipSync.section.label",
            new OptionalReferenceableSettingsSectionDrawer(
                serializedObject,
                nameof(ExpressionDataComponent.HasLipSync),
                nameof(ExpressionDataComponent.LipSyncReference),
                nameof(ExpressionDataComponent.LipSync),
                () => new LipSyncSettings()),
            false),
        new SettingEntry(
            serializedObject.FindProperty(nameof(ExpressionDataComponent.HasNonFacialAnimations)),
            "expression.additionalAnimations.section.label",
            new ExpressionDataNonFacialSectionDrawer(serializedObject),
            false)
    };

    private sealed record SettingEntry(
        SerializedProperty Enabled,
        string LabelKey,
        ISectionDrawer Drawer,
        bool DefaultExpanded);
}

internal sealed class ExpressionDataFacialSectionDrawer : ISectionDrawer
{
    private readonly FacialDataSectionDrawer _drawer;

    public ExpressionDataFacialSectionDrawer(SerializedObject serializedObject)
    {
        _drawer = new FacialDataSectionDrawer(
            serializedObject,
            nameof(ExpressionDataComponent.FacialBlendShapes));
        Actions = new SectionActionSet(
            serializedObject,
            new[]
            {
                SectionActionField.From(
                    serializedObject.FindProperty(
                        nameof(ExpressionDataComponent.HasFacialBlendShapes)),
                    () => true)
            }.Concat(_drawer.Actions.Fields));
    }

    public SectionActionSet Actions { get; }
    public float GetHeight() => _drawer.GetHeight();
    public void Draw(Rect position) => _drawer.Draw(position);
}

internal sealed class ExpressionDataNonFacialSectionDrawer : ISectionDrawer
{
    private readonly NonFacialAnimationDataSectionDrawer _drawer;

    public ExpressionDataNonFacialSectionDrawer(SerializedObject serializedObject)
    {
        _drawer = new NonFacialAnimationDataSectionDrawer(
            serializedObject,
            nameof(ExpressionDataComponent.NonFacialAnimations));
        Actions = new SectionActionSet(
            serializedObject,
            new[]
            {
                SectionActionField.From(
                    serializedObject.FindProperty(
                        nameof(ExpressionDataComponent.HasNonFacialAnimations)),
                    () => false)
            }.Concat(_drawer.Actions.Fields));
    }

    public SectionActionSet Actions { get; }
    public float GetHeight() => _drawer.GetHeight();
    public void Draw(Rect position) => _drawer.Draw(position);
}

internal sealed class ExpressionDataBehaviorSectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _enabled;
    private readonly SerializedProperty _writeMode;
    private readonly SerializedProperty _eyeBlink;
    private readonly SerializedProperty _lipSync;

    public ExpressionDataBehaviorSectionDrawer(SerializedObject serializedObject)
    {
        _enabled = serializedObject.FindProperty(nameof(ExpressionDataComponent.HasFacialBehavior));
        _writeMode = serializedObject.FindProperty(nameof(ExpressionDataComponent.WriteMode));
        _eyeBlink = serializedObject.FindProperty(nameof(ExpressionDataComponent.AllowEyeBlink));
        _lipSync = serializedObject.FindProperty(nameof(ExpressionDataComponent.AllowLipSync));
        Actions = new SectionActionSet(
            serializedObject,
            new[]
            {
                SectionActionField.From(_enabled, () => true),
                SectionActionField.From(_writeMode, () => ExpressionWriteMode.Replace),
                SectionActionField.From(_eyeBlink, () => TrackingPermission.Allow),
                SectionActionField.From(_lipSync, () => TrackingPermission.Allow)
            });
    }

    public SectionActionSet Actions { get; }
    public float GetHeight() => GUIHelper.GetLinesHeight(3);

    public void Draw(Rect position)
        => ExpressionBehaviorGUI.Draw(position, _writeMode, _eyeBlink, _lipSync);
}

internal sealed class OptionalPropertySectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _enabled;
    private readonly SerializedProperty _value;

    public OptionalPropertySectionDrawer(
        SerializedProperty enabled,
        SerializedProperty value,
        Func<object?> createDefault)
    {
        _enabled = enabled;
        _value = value;
        Actions = new SectionActionSet(
            enabled.serializedObject,
            new[]
            {
                SectionActionField.From(enabled, () => true),
                SectionActionField.From(value, createDefault)
            });
    }

    public SectionActionSet Actions { get; }
    public float GetHeight() => EditorGUI.GetPropertyHeight(_value, GUIContent.none, true);

    public void Draw(Rect position)
    {
        position.height = GetHeight();
        EditorGUI.PropertyField(position, _value, GUIContent.none, true);
    }
}

internal sealed class OptionalReferenceableSettingsSectionDrawer
    : ISectionDrawer, ICollapsedSectionHeaderDrawer
{
    private readonly SerializedProperty _enabled;
    private readonly SerializedReferenceableSettings _settings;

    public OptionalReferenceableSettingsSectionDrawer(
        SerializedObject serializedObject,
        string enabledPropertyName,
        string referencePropertyName,
        string valuePropertyName,
        Func<object?> createDefault)
    {
        _enabled = serializedObject.FindProperty(enabledPropertyName);
        _settings = new SerializedReferenceableSettings(
            serializedObject,
            referencePropertyName,
            valuePropertyName);
        Actions = new SectionActionSet(
            serializedObject,
            new[]
            {
                SectionActionField.From(_enabled, () => false),
                SectionActionField.From(_settings.Reference, () => new SettingsReference()),
                SectionActionField.From(_settings.Direct, createDefault)
            });
    }

    public SectionActionSet Actions { get; }

    public float GetHeight()
        => SettingsReferenceGUI.GetHeight(
            _settings,
            EditorGUI.GetPropertyHeight(_settings.Direct, GUIContent.none, true));

    public void Draw(Rect position)
        => SettingsReferenceGUI.Draw(
            position,
            _settings,
            EditorGUI.GetPropertyHeight(_settings.Direct, GUIContent.none, true),
            rect => EditorGUI.PropertyField(rect, _settings.Direct, GUIContent.none, true));

    public float GetHeaderWidth() => SettingsReferenceGUI.GetHeaderWidth();
    public void DrawHeader(Rect position) => SettingsReferenceGUI.DrawHeader(position, _settings);
    public void DrawCollapsedHeader(Rect position) => DrawHeader(position);
}
