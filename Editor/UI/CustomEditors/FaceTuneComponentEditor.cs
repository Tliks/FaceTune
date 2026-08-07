using Aoyon.FaceTune.Settings;

namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(ExpressionComponent))]
internal sealed class FaceTuneComponentEditor : FaceTuneSectionEditorBase<ExpressionComponent>
{
    protected override bool ShowLanguageSwitcher => true;

    protected override float GetAdditionalSectionSpacingBefore(int sectionIndex)
        => sectionIndex is 3 or 5 ? 10f : 0f;

    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[]
        {
            CreateExpressionSection(),
            CreateBehaviorSection(),
            CreateAnimationSection(),
            CreateConditionSection(),
            CreateDirectMenuSection(),
            CreatePreviewSection()
        };

    private FaceTuneSection CreateExpressionSection()
        => CreateSection(
            "expression.section.label",
            new ExpressionSectionDrawer(
                serializedObject,
                Component,
                targets.Length),
            defaultExpanded: true);

    private FaceTuneSection CreateBehaviorSection()
        => CreateSection(
            "expression.behavior.section.label",
            new PropertiesSectionDrawer(
                new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(ExpressionComponent.FacialSettings)))),
            defaultExpanded: true);

    private FaceTuneSection CreateAnimationSection()
        => CreateSection(
            "expression.animationSettings.section.label",
            new PropertiesSectionDrawer(
                new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(ExpressionComponent.ExpressionSettings)))),
            defaultExpanded: false);

    private FaceTuneSection CreateConditionSection()
    {
        var enabled = serializedObject.FindProperty(nameof(ExpressionComponent.ConditionEnabled));
        return CreateSection(
            "expression.condition.section.label",
            new ConditionSectionDrawer(
                enabled,
                serializedObject.FindProperty(nameof(ExpressionComponent.Condition))),
            defaultExpanded: enabled.boolValue,
            enabledProperty: enabled);
    }

    private FaceTuneSection CreateDirectMenuSection()
    {
        var enabled = serializedObject.FindProperty(nameof(ExpressionComponent.DirectMenuEnabled));
        return CreateSection(
            "expression.directMenu.label",
            new DirectMenuSectionDrawer(
                enabled,
                serializedObject.FindProperty(nameof(ExpressionComponent.DirectMenuSettings))),
            defaultExpanded: false,
            enabledProperty: enabled);
    }

    private FaceTuneSection CreatePreviewSection()
        => CreateSection(
            "expression.previewSettings.section.label",
            new PreviewSettingsSectionDrawer(serializedObject),
            defaultExpanded: false);
}

internal sealed class ConditionSectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _enabled;
    private readonly SerializedProperty _condition;

    public ConditionSectionDrawer(SerializedProperty enabled, SerializedProperty condition)
    {
        _enabled = enabled;
        _condition = condition;
    }

    public float GetHeight() => EditorGUI.GetPropertyHeight(_condition, GUIContent.none, true);

    public void Draw(Rect position)
    {
        position.height = GetHeight();
        using var disabled = new EditorGUI.DisabledScope(!_enabled.boolValue || _enabled.hasMultipleDifferentValues);
        EditorGUI.PropertyField(position, _condition, GUIContent.none, true);
    }

}

internal sealed class DirectMenuSectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _enabled;
    private readonly SerializedProperty _settings;

    public DirectMenuSectionDrawer(SerializedProperty enabled, SerializedProperty settings)
    {
        _enabled = enabled;
        _settings = settings;
    }

    public float GetHeight() => EditorGUI.GetPropertyHeight(_settings, GUIContent.none, true);

    public void Draw(Rect position)
    {
        position.height = GetHeight();
        using var disabled = new EditorGUI.DisabledScope(!_enabled.boolValue || _enabled.hasMultipleDifferentValues);
        EditorGUI.PropertyField(position, _settings, GUIContent.none, true);
    }

}

internal sealed class PreviewSettingsSectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _enableRealTimePreview;

    public PreviewSettingsSectionDrawer(SerializedObject serializedObject)
        => _enableRealTimePreview = serializedObject.FindProperty(nameof(ExpressionComponent.EnableRealTimePreview));

    public float GetHeight() => GUIHelper.GetLinesHeight(3);

    public void Draw(Rect position)
    {
        GUIHelper.LocalizedPropertyField(position, _enableRealTimePreview, "expression.realTimePreview.label");
        position.NewLine();
        ProjectSettings.EnableHierarchySelectedExpressionPreview = GUIHelper.DrawToggleLeft(
            position,
            ProjectSettings.EnableHierarchySelectedExpressionPreview,
            "expression.selectedExpressionPreview.label".LG());
        position.NewLine();
        ProjectSettings.EnableProjectSelectedExpressionPreview = GUIHelper.DrawToggleLeft(
            position,
            ProjectSettings.EnableProjectSelectedExpressionPreview,
            "expression.selectedProjectExpressionPreview.label".LG());
    }

}
