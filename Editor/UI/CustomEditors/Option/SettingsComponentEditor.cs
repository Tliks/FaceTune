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
                setting.DefaultExpanded,
                populateHeaderMenu: menu => PopulateHeaderMenu(menu, setting),
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
        SectionOperations.ResetValues(setting.Drawer.Actions);
        setting.Enabled.boolValue = true;
        serializedObject.ApplyModifiedProperties();
    }

    private SettingEntry[] Settings => _settings ??= new[]
    {
        CreateReferenceableSetting(
            nameof(SettingsComponent.HasFacialBlendShapes),
            "settings.configureFacial.section.label",
            new SettingsFacialSectionDrawer(serializedObject),
            0,
            defaultExpanded: true),
        CreateReferenceableSetting(
            nameof(SettingsComponent.HasEyeBlink),
            "settings.eyeBlink.section.label",
            new ReferenceableSettingsSectionDrawer(
                new SerializedReferenceableSettings(
                    serializedObject,
                    nameof(SettingsComponent.EyeBlinkReference),
                    nameof(SettingsComponent.EyeBlink)),
                () => new EyeBlinkSettings()),
            1),
        CreateReferenceableSetting(
            nameof(SettingsComponent.HasLipSync),
            "settings.lipSync.section.label",
            new ReferenceableSettingsSectionDrawer(
                new SerializedReferenceableSettings(
                    serializedObject,
                    nameof(SettingsComponent.LipSyncReference),
                    nameof(SettingsComponent.LipSync)),
                () => new LipSyncSettings()),
            1),
        CreateSetting(
            nameof(SettingsComponent.ExpressionSetEnabled),
            "settings.expressionSet.section.label",
            Property(nameof(SettingsComponent.ExpressionSet), () => new ExpressionSetSettings()),
            2),
        CreateSetting(
            nameof(SettingsComponent.HasCondition),
            "settings.addCondition.section.label",
            Property(nameof(SettingsComponent.Condition), SettingsComponent.CreateDefaultCondition),
            2),
        CreateSetting(
            nameof(SettingsComponent.HasTransition),
            "settings.transition.section.label",
            Property(nameof(SettingsComponent.Transition), () => new TransitionSettings()),
            3),
        CreateSetting(
            nameof(SettingsComponent.HasPriority),
            "settings.priority.section.label",
            Property(nameof(SettingsComponent.Priority), () => new PrioritySettings()),
            3)
    };

    private PropertiesSectionDrawer Property(
        string propertyName,
        Func<object?> createDefault)
        => new(new PropertiesSectionDrawer.Entry(
            serializedObject.FindProperty(propertyName),
            null,
            createDefault));

    private SettingEntry CreateSetting(
        string enabledPropertyName,
        string labelKey,
        ISectionDrawer drawer,
        int spacingGroup)
        => new(
            serializedObject.FindProperty(enabledPropertyName),
            labelKey,
            drawer,
            spacingGroup);

    private SettingEntry CreateReferenceableSetting(
        string enabledPropertyName,
        string labelKey,
        ISectionDrawer drawer,
        int spacingGroup,
        bool defaultExpanded = false)
        => new(
            serializedObject.FindProperty(enabledPropertyName),
            labelKey,
            drawer,
            spacingGroup,
            defaultExpanded);

    private void PopulateHeaderMenu(GenericMenu menu, SettingEntry setting)
    {
        PopulateRemoveMenu(menu, setting.Enabled);
        if (setting.Drawer is not ISectionHeaderMenuDrawer menuDrawer) return;

        menu.AddSeparator(string.Empty);
        menuDrawer.PopulateHeaderMenu(menu);
    }

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
        string LabelKey,
        ISectionDrawer Drawer,
        int SpacingGroup,
        bool DefaultExpanded = false);
}

internal sealed class SettingsFacialSectionDrawer : ISectionDrawer, ISectionHeaderDrawer, ISectionHeaderMenuDrawer
{
    private readonly FacialDataSectionDrawer _expression;
    private readonly SerializedProperty _applyToRenderer;

    public SettingsFacialSectionDrawer(SerializedObject serializedObject)
    {
        _expression = new FacialDataSectionDrawer(
            serializedObject,
            nameof(SettingsComponent.FacialBlendShapesReference),
            nameof(SettingsComponent.FacialBlendShapes));
        _applyToRenderer = serializedObject.FindProperty(nameof(SettingsComponent.ApplyToRenderer));
        Actions = new SectionActionSet(
            serializedObject,
            _expression.Actions.Fields.Concat(new[]
            {
                SectionActionField.From(
                    _applyToRenderer,
                    () => SettingsComponent.DefaultApplyToRenderer)
            }));
    }

    public SectionActionSet Actions { get; }

    public float GetHeight()
        => _expression.GetHeight()
         + GUIHelper.VerticalSpacing
         + GUIHelper.LineHeight;

    public float GetHeaderWidth() => _expression.GetHeaderWidth();
    public void DrawHeader(Rect position) => _expression.DrawHeader(position);

    public void PopulateHeaderMenu(GenericMenu menu)
    {
        if (_applyToRenderer.serializedObject.targetObjects.Length != 1)
        {
            menu.AddDisabledItem("settings.applyToRenderer.menu".LG());
            menu.AddDisabledItem("settings.getFromRenderer.menu".LG());
        }
        else
        {
            menu.AddItem("settings.applyToRenderer.menu".LG(), false, ApplyToRenderer);
            menu.AddItem("settings.getFromRenderer.menu".LG(), false, GetFromRenderer);
        }

        menu.AddSeparator(string.Empty);
        _expression.PopulateHeaderMenu(menu);
    }

    public void Draw(Rect position)
    {
        position.height = _expression.GetHeight();
        _expression.Draw(position);
        position.y += position.height + GUIHelper.VerticalSpacing;
        position.SetSingleHeight();
        EditorGUI.PropertyField(position, _applyToRenderer, "settings.applyToRenderer.label".LG());
    }

    private void ApplyToRenderer()
    {
        if (_applyToRenderer.serializedObject.targetObject is not SettingsComponent settings
            || !AvatarContext.TryGet(settings.gameObject, out var context, out _)) return;

        var resolver = new FaceTuneResolver(context.Root);
        if (!resolver.SettingsReferences.TryResolve<FacialBlendShapeData>(settings, out var data)) return;
        var animations = new List<BlendShapeWeightAnimation>();
        if (data.Clip != null)
            data.Clip.GetBlendShapeAnimations(data.ClipOption, animations, context.BodyPath);
        foreach (var animation in data.BlendShapeAnimations) animations.Add(animation);

        var values = new BlendShapeWeightSet(animations.ToFirstFrameBlendShapes());
        var ignoredNames = AvatarContext.GetExplicitlyExcludedBlendShapeNames(context.Root);
        Undo.RecordObject(context.FaceRenderer, "Apply Blend Shapes");
        new BlendShapeApply(context.FaceRenderer, values, 0f, ignoredNames)
            .ApplyBlendShapes(context.FaceMesh);
        Selection.activeGameObject = context.FaceRenderer.gameObject;
        EditorGUIUtility.PingObject(context.FaceRenderer);
    }

    private void GetFromRenderer()
    {
        var serializedObject = _applyToRenderer.serializedObject;
        if (serializedObject.targetObject is not SettingsComponent settings
            || !AvatarContext.TryGet(settings.gameObject, out var context, out _)) return;

        var values = context.FaceRenderer
            .GetNonZeroBlendShapeAnimations(context.FaceMesh)
            .ToArray();
        serializedObject.UpdateIfRequiredOrScript();
        var reference = serializedObject.FindProperty(nameof(SettingsComponent.FacialBlendShapesReference));
        reference.FindPropertyRelative(nameof(SettingsReference.Mode)).intValue = (int)SettingsReferenceMode.Direct;
        var direct = serializedObject.FindProperty(nameof(SettingsComponent.FacialBlendShapes));
        direct.FindPropertyRelative(nameof(FacialBlendShapeData.Clip)).objectReferenceValue = null;
        direct.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations)).SynchronizeArrayByKey(
            values,
            element => element.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue,
            animation => animation.Name,
            (element, animation) => element.CopyFrom(animation),
            overwrite: true);
        serializedObject.ApplyModifiedProperties();
    }
}
