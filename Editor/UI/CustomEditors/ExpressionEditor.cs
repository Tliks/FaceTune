using Aoyon.FaceTune.Gui.ShapesEditor;
using Aoyon.FaceTune.Settings;

namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(FaceTuneComponent))]
internal sealed class ExpressionEditor : FaceTuneEditor<FaceTuneComponent>
{
    private static readonly ReorderableListOptions AnimationListOptions = new(
        Foldout: false,
        MaxVisibleHeight: 180f,
        InitializeElement: BlendShapeWeightAnimationDrawer.Initialize);
    private static GUIStyle? _contentStyle;
    private static GUIStyle ContentStyle => _contentStyle ??= new GUIStyle(EditorStyles.helpBox)
    {
        margin = new RectOffset(),
        padding = new RectOffset(4, 4, 4, 4)
    };
    private static GUIStyle? _groupLabelStyle;
    private static GUIStyle GroupLabelStyle => _groupLabelStyle ??= new GUIStyle(EditorStyles.boldLabel)
    {
        normal = { textColor = Color.white }
    };

    private bool _expressionExpanded;
    private bool _otherExpressionExpanded;
    private bool _behaviorExpanded;
    private bool _conditionExpanded;
    private bool _directMenuExpanded;
    private bool _animationExpanded;
    private bool _previewExpanded;

    private void OnEnable()
    {
        serializedObject.UpdateIfRequiredOrScript();
        _expressionExpanded = true;
        _otherExpressionExpanded = targets
            .Cast<FaceTuneComponent>()
            .Any(component => component.DataReference?.Get(component) != null || component.Data.Clip != null);
        _behaviorExpanded = true;
        _conditionExpanded = serializedObject.FindProperty(nameof(FaceTuneComponent.ConditionEnabled)).boolValue;
        _directMenuExpanded = false;
        _animationExpanded = false;
        _previewExpanded = false;
    }

    protected override void DrawInspector()
    {
        DrawGroupLabel("expression.group.expression.label");
        DrawExpressionSection();
        DrawAnimationSection();
        DrawBehaviorSection();

        EditorGUILayout.Space(10f);
        DrawGroupLabel("expression.group.conditions.label");
        DrawConditionSection();
        DrawDirectMenuSection();

        EditorGUILayout.Space(10f);
        DrawGroupLabel("expression.group.other.label");
        DrawPreviewSection();
    }

    private static void DrawGroupLabel(string key)
        => EditorGUILayout.LabelField(key.LG(), GroupLabelStyle);

    private void DrawExpressionSection()
    {
        _expressionExpanded = ShurikenHeaderUI.DrawFoldoutLayout(_expressionExpanded, "expression.section.label".LG());
        if (!_expressionExpanded) return;

        using var region = LayoutRegion.Begin(ContentStyle);
        var data = serializedObject.FindProperty(nameof(FaceTuneComponent.Data));
        var otherExpressionFoldoutRect = EditorGUILayout.GetControlRect();
        _otherExpressionExpanded = FoldoutUI.Draw(
            otherExpressionFoldoutRect,
            _otherExpressionExpanded,
            "expression.otherExpression.label".LG());
        if (_otherExpressionExpanded)
        {
            using var otherExpressionRegion = LayoutRegion.Begin(ContentStyle);
            LocalizedUI.PropertyField(
                serializedObject.FindProperty(nameof(FaceTuneComponent.DataReference)),
                "expression.otherComponent.label");

            var clip = data.FindPropertyRelative("Clip");
            LocalizedUI.PropertyField(clip, "expression.clip.label");
            if (clip.objectReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginHorizontal();
                DrawEnumLayout(data.FindPropertyRelative("ClipOption"), "expression.clip.importOption.label", nameof(ClipImportOption));
                using (new EditorGUI.DisabledScope(targets.Length != 1))
                    if (GUILayout.Button("expression.clip.import.button".LG(), GUILayout.ExpandWidth(false))) ImportClip();
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }
        }

        var animations = data.FindPropertyRelative("BlendShapeAnimations");
        var height = ReorderableListUI.GetHeight(animations, AnimationListOptions);
        var listRect = EditorGUILayout.GetControlRect(false, height);
        ReorderableListUI.Draw(listRect, animations, "expression.blendShapes.label".LG(), AnimationListOptions);

        using (new EditorGUI.DisabledScope(targets.Length != 1))
            if (GUILayout.Button("expression.openEditor.button".LG())) OpenEditor();
    }

    private void DrawBehaviorSection()
    {
        _behaviorExpanded = ShurikenHeaderUI.DrawFoldoutLayout(_behaviorExpanded, "expression.behavior.section.label".LG());
        if (!_behaviorExpanded) return;

        using var region = LayoutRegion.Begin(ContentStyle);
        var facial = serializedObject.FindProperty(nameof(FaceTuneComponent.FacialSettings));
        DrawApplication(facial.FindPropertyRelative(FacialSettings.WriteModePropName));
        DrawEnumLayout(
            facial.FindPropertyRelative(FacialSettings.AllowEyeBlinkPropName),
            "facialSettings.allowEyeBlink.label",
            nameof(TrackingPermission));
        DrawEnumLayout(
            facial.FindPropertyRelative(FacialSettings.AllowLipSyncPropName),
            "facialSettings.allowLipSync.label",
            nameof(TrackingPermission));
    }

    private void DrawConditionSection()
        => DrawToggleSection(
            nameof(FaceTuneComponent.ConditionEnabled),
            nameof(FaceTuneComponent.Condition),
            "expression.condition.section.label",
            ref _conditionExpanded);

    private void DrawDirectMenuSection()
        => DrawToggleSection(
            nameof(FaceTuneComponent.DirectMenuEnabled),
            nameof(FaceTuneComponent.DirectMenuSettings),
            "expression.directMenu.label",
            ref _directMenuExpanded);

    private void DrawToggleSection(string enabledName, string contentName, string key, ref bool expanded)
    {
        var enabled = serializedObject.FindProperty(enabledName);
        var content = serializedObject.FindProperty(contentName);
        expanded = ShurikenHeaderUI.DrawToggleFoldoutLayout(enabled, expanded, key.LG());
        if (!expanded) return;

        using var region = LayoutRegion.Begin(ContentStyle);
        using var disabled = new EditorGUI.DisabledScope(!enabled.boolValue || enabled.hasMultipleDifferentValues);
        EditorGUILayout.PropertyField(content, GUIContent.none, true);
    }

    private void DrawAnimationSection()
    {
        _animationExpanded = ShurikenHeaderUI.DrawFoldoutLayout(_animationExpanded, "expression.animationSettings.section.label".LG());
        if (!_animationExpanded) return;

        using var region = LayoutRegion.Begin(ContentStyle);
        var settings = serializedObject.FindProperty(nameof(FaceTuneComponent.ExpressionSettings));
        DrawMultiFrameSettings(settings);
    }

    private void DrawPreviewSection()
    {
        _previewExpanded = ShurikenHeaderUI.DrawFoldoutLayout(_previewExpanded, "expression.previewSettings.section.label".LG());
        if (!_previewExpanded) return;

        using var region = LayoutRegion.Begin(ContentStyle);
        LocalizedUI.PropertyField(
            serializedObject.FindProperty(nameof(FaceTuneComponent.EnableRealTimePreview)),
            "expression.realTimePreview.label");
        var selectedPreview = ProjectSettings.EnableSelectedExpressionPreview;
        var nextPreview = EditorGUILayout.Toggle("expression.selectedExpressionPreview.label".LG(), selectedPreview);
        if (nextPreview != selectedPreview) ProjectSettings.EnableSelectedExpressionPreview = nextPreview;
    }

    private static void DrawApplication(SerializedProperty mode)
    {
        var contents = new[]
        {
            "expression.application.replace.label".LG(),
            "expression.application.blend.label".LG()
        };
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("expression.application.label".LG());
        var next = GUILayout.Toolbar(mode.enumValueIndex, contents);
        EditorGUILayout.EndHorizontal();
        if (next != mode.enumValueIndex) mode.enumValueIndex = next;
    }

    private static void DrawMultiFrameSettings(SerializedProperty settings)
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
        var next = GUILayout.Toolbar(mode.enumValueIndex, labels);
        if (next != mode.enumValueIndex) mode.enumValueIndex = next;
        if (next != 2 && next != 3) return;

        EditorGUI.indentLevel++;
        if (next == 2)
            DrawEnumLayout(triggerHand, "expression.multiFrame.linkedHand.label", nameof(Hand));
        else
            LocalizedUI.PropertyField(parameter, "expression.multiFrame.parameterName.label");
        EditorGUI.indentLevel--;
    }

    private static void DrawEnumLayout(SerializedProperty property, string labelKey, string typeName)
    {
        var position = EditorGUILayout.GetControlRect();
        FaceTuneDrawerUtility.Enum(position, property, labelKey, typeName);
    }

    private void ImportClip()
    {
        if (targets.Length != 1 || !CustomEditorUtility.TryGetContext(Component.gameObject, out var context)) return;
        if (Component.Data.Clip == null) return;
        var animations = new List<BlendShapeWeightAnimation>();
        Component.Data.Clip.GetBlendShapeAnimations(Component.Data.ClipOption, animations, context.BodyPath);
        var property = serializedObject.FindProperty(nameof(FaceTuneComponent.Data)).FindPropertyRelative("BlendShapeAnimations");
        CustomEditorUtility.AddBlendShapeAnimations(property, animations);
    }

    private void OpenEditor()
    {
        if (targets.Length != 1 || !CustomEditorUtility.TryGetContext(Component.gameObject, out _)) return;
        var defaults = new BlendShapeWeightSet(Component.Data.BlendShapeAnimations.Select(x => x.ToFirstFrameBlendShape()));
        CustomEditorUtility.OpenEditor(Component.gameObject, new FaceTuneDataTargeting { Target = Component }, defaults);
    }
}
