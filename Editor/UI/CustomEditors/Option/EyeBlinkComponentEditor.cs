namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(EyeBlinkComponent))]
internal sealed class EyeBlinkComponentEditor : FaceTuneSectionEditorBase<EyeBlinkComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateEyeBlinkSection() };

    private FaceTuneSection CreateEyeBlinkSection()
        => CreateSection(
            "eyeBlink.section.label",
            new EyeBlinkSectionDrawer(serializedObject, targets.Length),
            defaultExpanded: false);
}

internal sealed class EyeBlinkSectionDrawer : ISectionDrawer
{
    private readonly ReorderableListOptions _animationListOptions;

    private readonly SerializedObject _serializedObject;
    private readonly SerializedProperty _referenceMode;
    private readonly SerializedProperty _reference;
    private readonly SerializedProperty _settings;
    private readonly SerializedProperty _mode;
    private readonly SerializedProperty _animations;
    private readonly SerializedProperty _intervalSeconds;
    private readonly int _targetCount;

    public EyeBlinkSectionDrawer(SerializedObject serializedObject, int targetCount)
    {
        _serializedObject = serializedObject;
        _referenceMode = serializedObject.FindProperty(nameof(EyeBlinkComponent.ReferenceMode));
        _reference = serializedObject.FindProperty(nameof(EyeBlinkComponent.Reference));
        _settings = serializedObject.FindProperty(nameof(EyeBlinkComponent.Settings));
        _mode = _settings.FindPropertyRelative("mode");
        var automatic = _settings.FindPropertyRelative("automatic");
        _animations = automatic.FindPropertyRelative("animations");
        _intervalSeconds = automatic.FindPropertyRelative("intervalSeconds");
        _targetCount = targetCount;
        _animationListOptions = new(
            Header: ReorderableListOptions.HeaderMode.Label,
            HeaderContentHeight: GUIHelper.LineHeight,
            DrawHeaderContent: DrawClipImport,
            InitializeElement: InitializeAnimation);
    }

    public float GetHeight()
    {
        var height = GUIHelper.LineHeight;
        if (_referenceMode.hasMultipleDifferentValues || _referenceMode.enumValueIndex == (int)SettingsSourceMode.Reference)
            return height + GUIHelper.VerticalSpacing + GUIHelper.LineHeight;

        height += GUIHelper.VerticalSpacing + GUIHelper.LineHeight;
        if (_mode.hasMultipleDifferentValues || _mode.enumValueIndex == (int)EyeBlinkMode.Automatic)
        {
            height += GUIHelper.VerticalSpacing + GUIHelper.GetListHeight(_animations, _animationListOptions);
            height += GUIHelper.VerticalSpacing + GUIHelper.LineHeight;
        }
        return height;
    }

    public void Draw(Rect position)
    {
        GUIHelper.DrawLocalizedEnum(ref position, _referenceMode, "eyeBlink.configuration.label", nameof(SettingsSourceMode));
        if (_referenceMode.hasMultipleDifferentValues || _referenceMode.enumValueIndex == (int)SettingsSourceMode.Reference)
        {
            GUIHelper.DrawProperty(ref position, _reference, "eyeBlink.component.label");
            return;
        }

        GUIHelper.DrawLocalizedEnum(ref position, _mode, "eyeBlink.mode.label", nameof(EyeBlinkMode));
        if (!_mode.hasMultipleDifferentValues && _mode.enumValueIndex != (int)EyeBlinkMode.Automatic) return;

        position.height = GUIHelper.GetListHeight(_animations, _animationListOptions);
        GUIHelper.DrawList(position, _animations, "eyeBlink.animations.label".LG(), _animationListOptions);
        position.y += position.height + GUIHelper.VerticalSpacing;
        position.height = GUIHelper.LineHeight;
        GUIHelper.DrawProperty(ref position, _intervalSeconds, "eyeBlink.intervalSeconds.label");
    }

    public void Reset()
    {
        _referenceMode.CopyFrom(EyeBlinkComponent.DefaultReferenceMode);
        _reference.CopyFrom(null);
        _settings.CopyFrom(EyeBlinkComponent.CreateDefaultSettings());
    }

    private static void InitializeAnimation(SerializedProperty property)
        => property.CopyFrom(AutomaticBlinkSettings.CreateDefaultAnimations()[0]);

    private void DrawClipImport(Rect position, SerializedProperty property)
    {
        using var disabled = new EditorGUI.DisabledScope(_targetCount != 1);
        var clip = EditorGUI.ObjectField(
            position,
            GUIContent.none,
            null,
            typeof(AnimationClip),
            false) as AnimationClip;
        if (clip == null || _serializedObject.targetObject is not Component component) return;
        if (!AvatarContext.TryGet(component.gameObject, out var context, out _)) return;

        var animations = new List<BlendShapeWeightAnimation>();
        clip.GetBlendShapeAnimations(ClipImportOption.All, animations, context.BodyPath);
        ExpressionGUI.SetBlendShapeAnimations(_animations, animations);
    }

}
