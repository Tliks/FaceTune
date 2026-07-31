using Aoyon.FaceTune.Settings;

namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(FaceTuneComponent))]
internal sealed class FaceTuneComponentEditor : FaceTuneSectionEditor<FaceTuneComponent>
{
    protected override bool ShowLanguageSwitcher => true;

    private const float ParameterWarningHeight = 30f;

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
    {
        var data = serializedObject.FindProperty(nameof(FaceTuneComponent.Data));
        ExpressionGUI.InitializeExpansions(data);
        return new(
            "expression.section.label".LG(),
            () => ExpressionGUI.GetContentHeight(data),
            content => ExpressionGUI.DrawContent(
                content,
                serializedObject,
                Component,
                targets.Length),
            true);
    }

    private FaceTuneSection CreateBehaviorSection()
        => new(
            "expression.behavior.section.label".LG(),
            () => GUIHelper.GetLinesHeight(3),
            DrawBehaviorContent,
            true);

    private FaceTuneSection CreateAnimationSection()
    {
        var settings = serializedObject.FindProperty(nameof(FaceTuneComponent.ExpressionSettings));
        return new(
            "expression.animationSettings.section.label".LG(),
            () => AnimationContentHeight(settings),
            content => DrawMultiFrameSettings(content, settings),
            false);
    }

    private FaceTuneSection CreateConditionSection()
    {
        var enabled = serializedObject.FindProperty(nameof(FaceTuneComponent.ConditionEnabled));
        var condition = serializedObject.FindProperty(nameof(FaceTuneComponent.Condition));
        return new(
            "expression.condition.section.label".LG(),
            () => EditorGUI.GetPropertyHeight(condition, GUIContent.none, true),
            content =>
            {
                content.height = EditorGUI.GetPropertyHeight(condition, GUIContent.none, true);
                using var disabled = new EditorGUI.DisabledScope(
                    !enabled.boolValue || enabled.hasMultipleDifferentValues);
                EditorGUI.PropertyField(content, condition, GUIContent.none, true);
            },
            enabled.boolValue,
            EnabledProperty: enabled);
    }

    private FaceTuneSection CreateDirectMenuSection()
    {
        var enabled = serializedObject.FindProperty(nameof(FaceTuneComponent.DirectMenuEnabled));
        var settings = serializedObject.FindProperty(nameof(FaceTuneComponent.DirectMenuSettings));
        return new(
            "expression.directMenu.label".LG(),
            () => EditorGUI.GetPropertyHeight(settings, GUIContent.none, true),
            content =>
            {
                content.height = EditorGUI.GetPropertyHeight(settings, GUIContent.none, true);
                using var disabled = new EditorGUI.DisabledScope(
                    !enabled.boolValue || enabled.hasMultipleDifferentValues);
                EditorGUI.PropertyField(content, settings, GUIContent.none, true);
            },
            false,
            EnabledProperty: enabled);
    }

    private FaceTuneSection CreatePreviewSection()
        => new(
            "expression.previewSettings.section.label".LG(),
            () => GUIHelper.GetLinesHeight(3),
            DrawPreviewContent,
            false);

    private void DrawBehaviorContent(Rect position)
    {
        var facial = serializedObject.FindProperty(nameof(FaceTuneComponent.FacialSettings));
        DrawEnum(
            position,
            facial.FindPropertyRelative(FacialSettings.AllowEyeBlinkPropName),
            "facialSettings.allowEyeBlink.label",
            nameof(TrackingPermission));
        position.NewLine();
        DrawEnum(
            position,
            facial.FindPropertyRelative(FacialSettings.AllowLipSyncPropName),
            "facialSettings.allowLipSync.label",
            nameof(TrackingPermission));
        position.NewLine();
        DrawApplication(position, facial.FindPropertyRelative(FacialSettings.WriteModePropName));
    }

    private void DrawPreviewContent(Rect position)
    {
        GUIHelper.DrawToggleLeft(
            position,
            serializedObject.FindProperty(nameof(FaceTuneComponent.EnableRealTimePreview)),
            "expression.realTimePreview.label".LG());
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

    private static void DrawApplication(Rect position, SerializedProperty mode)
    {
        var contents = new[]
        {
            "expression.application.replace.label".LG(),
            "expression.application.blend.label".LG()
        };
        var toolbarRect = EditorGUI.PrefixLabel(position, "expression.application.label".LG());
        var next = GUI.Toolbar(toolbarRect, mode.enumValueIndex, contents);
        if (next != mode.enumValueIndex) mode.enumValueIndex = next;
    }

    private static void DrawMultiFrameSettings(Rect position, SerializedProperty settings)
    {
        var mode = settings.FindPropertyRelative(ExpressionSettings.MultiFrameModePropName);
        var triggerHand = settings.FindPropertyRelative(ExpressionSettings.TriggerHandPropName);
        var parameter = settings.FindPropertyRelative(ExpressionSettings.ParameterNamePropName);
        var labels = new[]
        {
            "expression.multiFrame.default.label".LG(),
            "expression.multiFrame.loop.label".LG(),
            "expression.multiFrame.trigger.label".LG(),
            "expression.multiFrame.parameter.label".LG()
        };
        var next = GUI.Toolbar(position, mode.enumValueIndex, labels);
        if (next != mode.enumValueIndex) mode.enumValueIndex = next;
        if (next != 2 && next != 3) return;

        position.NewLine();
        position.Indent();
        if (next == 2)
            DrawEnum(position, triggerHand, "expression.multiFrame.linkedHand.label", nameof(Hand));
        else
        {
            GUIHelper.LocalizedPropertyField(
                position,
                parameter,
                "expression.multiFrame.parameterName.label");
            if (string.IsNullOrWhiteSpace(parameter.stringValue))
            {
                position.NewLine();
                position.height = ParameterWarningHeight;
                EditorGUI.HelpBox(
                    position,
                    "expression.multiFrame.parameterName.empty.message".LS(),
                    MessageType.Warning);
            }
        }
    }

    private static void DrawEnum(Rect position, SerializedProperty property, string labelKey, string typeName)
        => GUIHelper.DrawLocalizedEnum(position, property, labelKey, typeName);

    private static float AnimationContentHeight(SerializedProperty settings)
    {
        var mode = settings.FindPropertyRelative(ExpressionSettings.MultiFrameModePropName);
        var height = GUIHelper.GetLinesHeight(mode.enumValueIndex is 2 or 3 ? 2 : 1);
        if (mode.enumValueIndex == 3)
        {
            var parameter = settings.FindPropertyRelative(ExpressionSettings.ParameterNamePropName);
            if (string.IsNullOrWhiteSpace(parameter.stringValue))
                height += GUIHelper.VerticalSpacing + ParameterWarningHeight;
        }
        return height;
    }

}
