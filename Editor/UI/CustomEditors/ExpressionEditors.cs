using Aoyon.FaceTune.Gui.ShapesEditor;
using Aoyon.FaceTune.Settings;

namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(FaceTuneComponent))]
internal sealed class ExpressionEditor : FaceTuneEditor<FaceTuneComponent>
{
    private const float ParameterWarningHeight = 30f;
    private static GUIStyle? _groupLabelStyle;
    private static GUIStyle GroupLabelStyle => _groupLabelStyle ??= new GUIStyle(EditorStyles.boldLabel)
    {
        normal = { textColor = Color.white }
    };

    private bool _expressionExpanded;
    private bool _otherExpressionExpanded;
    private bool _behaviorExpanded;
    private bool _animationExpanded;
    private bool _previewExpanded;
    private Rect _cursor;

    private void OnEnable()
    {
        serializedObject.UpdateIfRequiredOrScript();
        _expressionExpanded = true;
        _otherExpressionExpanded = targets
            .Cast<FaceTuneComponent>()
            .Any(component => component.DataReference?.Get(component) != null || component.Data.Clip != null);
        _behaviorExpanded = true;
        _animationExpanded = false;
        _previewExpanded = false;
    }

    protected override float GetInspectorHeight()
    {
        var height = 0f;
        Add(ref height, GUIHelper.LineHeight);
        Add(ref height, ExpressionGUI.GetHeight(serializedObject.FindProperty(nameof(FaceTuneComponent.Data)), _expressionExpanded, _otherExpressionExpanded));
        Add(ref height, SectionHeight(_behaviorExpanded, ContentHeight(3)));

        var settings = serializedObject.FindProperty(nameof(FaceTuneComponent.ExpressionSettings));
        Add(ref height, SectionHeight(_animationExpanded, AnimationContentHeight(settings)));

        height += 10f;
        Add(ref height, GUIHelper.LineHeight);
        Add(ref height, ToggleSectionHeight(serializedObject.FindProperty(nameof(FaceTuneComponent.ConditionEnabled)), EditorGUI.GetPropertyHeight(serializedObject.FindProperty(nameof(FaceTuneComponent.Condition)), GUIContent.none, true)));
        Add(ref height, ToggleSectionHeight(serializedObject.FindProperty(nameof(FaceTuneComponent.DirectMenuEnabled)), EditorGUI.GetPropertyHeight(serializedObject.FindProperty(nameof(FaceTuneComponent.DirectMenuSettings)), GUIContent.none, true)));

        height += 10f;
        Add(ref height, GUIHelper.LineHeight);
        Add(ref height, SectionHeight(_previewExpanded, ContentHeight(2)));
        return Mathf.Max(0f, height - GUIHelper.HeaderSpacing);
    }

    protected override void DrawInspector(Rect position)
    {
        _cursor = position;
        DrawGroupLabel("expression.group.expression.label");
        DrawExpressionSection();
        DrawBehaviorSection();
        DrawAnimationSection();

        _cursor.y += 10f;
        DrawGroupLabel("expression.group.conditions.label");
        DrawConditionSection();
        DrawDirectMenuSection();

        _cursor.y += 10f;
        DrawGroupLabel("expression.group.other.label");
        DrawPreviewSection();
    }

    private void DrawGroupLabel(string key)
        => EditorGUI.LabelField(Take(GUIHelper.LineHeight), key.LG(), GroupLabelStyle);

    private void DrawExpressionSection()
    {
        var data = serializedObject.FindProperty(nameof(FaceTuneComponent.Data));
        var height = ExpressionGUI.GetHeight(data, _expressionExpanded, _otherExpressionExpanded);
        var position = Take(height);
        ExpressionGUI.Draw(
            position,
            serializedObject,
            Component,
            targets.Length,
            ref _expressionExpanded,
            ref _otherExpressionExpanded);
    }

    private void DrawBehaviorSection()
    {
        var height = ContentHeight(3);
        if (!DrawSection(
                ref _behaviorExpanded,
                "expression.behavior.section.label",
                height,
                out var content)) return;
        var facial = serializedObject.FindProperty(nameof(FaceTuneComponent.FacialSettings));
        DrawApplication(content, facial.FindPropertyRelative(FacialSettings.WriteModePropName));
        content.NewLine();
        DrawEnum(
            content,
            facial.FindPropertyRelative(FacialSettings.AllowEyeBlinkPropName),
            "facialSettings.allowEyeBlink.label",
            nameof(TrackingPermission));
        content.NewLine();
        DrawEnum(
            content,
            facial.FindPropertyRelative(FacialSettings.AllowLipSyncPropName),
            "facialSettings.allowLipSync.label",
            nameof(TrackingPermission));
    }

    private void DrawConditionSection()
        => DrawToggleSection(
            nameof(FaceTuneComponent.ConditionEnabled),
            nameof(FaceTuneComponent.Condition),
            "expression.condition.section.label");

    private void DrawDirectMenuSection()
        => DrawToggleSection(
            nameof(FaceTuneComponent.DirectMenuEnabled),
            nameof(FaceTuneComponent.DirectMenuSettings),
            "expression.directMenu.label");

    private void DrawToggleSection(string enabledName, string contentName, string key)
    {
        var enabled = serializedObject.FindProperty(enabledName);
        var content = serializedObject.FindProperty(contentName);
        var propertyHeight = EditorGUI.GetPropertyHeight(content, GUIContent.none, true);
        var contentHeight = propertyHeight + GUIHelper.ContentPadding * 2f;
        if (!DrawToggleSection(
                enabled,
                key,
                contentHeight,
                out var contentRect)) return;
        contentRect.height = propertyHeight;
        using var disabled = new EditorGUI.DisabledScope(!enabled.boolValue || enabled.hasMultipleDifferentValues);
        EditorGUI.PropertyField(contentRect, content, GUIContent.none, true);
    }

    private void DrawAnimationSection()
    {
        var settings = serializedObject.FindProperty(nameof(FaceTuneComponent.ExpressionSettings));
        if (!DrawSection(
                ref _animationExpanded,
                "expression.animationSettings.section.label",
                AnimationContentHeight(settings),
                out var content)) return;
        DrawMultiFrameSettings(content, settings);
    }

    private void DrawPreviewSection()
    {
        if (!DrawSection(
                ref _previewExpanded,
                "expression.previewSettings.section.label",
                ContentHeight(2),
                out var content)) return;
        GUIHelper.DrawToggleLeft(
            content,
            serializedObject.FindProperty(nameof(FaceTuneComponent.EnableRealTimePreview)),
            "expression.realTimePreview.label".LG());
        content.NewLine();
        ProjectSettings.EnableSelectedExpressionPreview = GUIHelper.DrawToggleLeft(
            content,
            ProjectSettings.EnableSelectedExpressionPreview,
            "expression.selectedExpressionPreview.label".LG());
    }

    private bool DrawSection(
        ref bool expanded,
        string labelKey,
        float contentHeight,
        out Rect content)
    {
        var totalHeight = GUIHelper.ShurikenHeaderHeight;
        if (expanded)
        {
            totalHeight += GUIHelper.ContentSpacing + GUIHelper.ContentBottomSpacing + contentHeight;
        }

        var position = Take(totalHeight);
        var header = new Rect(
            position.x,
            position.y,
            position.width,
            GUIHelper.ShurikenHeaderHeight);
        expanded = GUIHelper.DrawShuriken(header, expanded, labelKey.LG());
        content = expanded ? DrawSectionContent(position, contentHeight) : Rect.zero;
        return expanded;
    }

    private bool DrawToggleSection(
        SerializedProperty enabled,
        string labelKey,
        float contentHeight,
        out Rect content)
    {
        var expanded = enabled.boolValue || enabled.hasMultipleDifferentValues;
        var totalHeight = GUIHelper.ShurikenHeaderHeight;
        if (expanded)
        {
            totalHeight += GUIHelper.ContentSpacing + GUIHelper.ContentBottomSpacing + contentHeight;
        }

        var position = Take(totalHeight);
        var header = new Rect(
            position.x,
            position.y,
            position.width,
            GUIHelper.ShurikenHeaderHeight);
        expanded = GUIHelper.DrawShurikenToggle(header, enabled, labelKey.LG());
        content = expanded ? DrawSectionContent(position, contentHeight) : Rect.zero;
        return expanded;
    }

    private static Rect DrawSectionContent(Rect section, float contentHeight)
    {
        var region = new Rect(
            section.x,
            section.y + GUIHelper.ShurikenHeaderHeight + GUIHelper.ContentSpacing,
            section.width,
            contentHeight);
        if (Event.current.type == EventType.Repaint) GUIHelper.DrawRegion(region);
        return new Rect(
            region.x + GUIHelper.ContentPadding,
            region.y + GUIHelper.ContentPadding,
            region.width - GUIHelper.ContentPadding * 2f,
            GUIHelper.LineHeight);
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
        var height = ContentHeight(mode.enumValueIndex is 2 or 3 ? 2 : 1);
        if (mode.enumValueIndex == 3)
        {
            var parameter = settings.FindPropertyRelative(ExpressionSettings.ParameterNamePropName);
            if (string.IsNullOrWhiteSpace(parameter.stringValue))
                height += GUIHelper.VerticalSpacing + ParameterWarningHeight;
        }
        return height;
    }

    private static float ContentHeight(int rows)
        => GUIHelper.ContentPadding * 2f
         + GUIHelper.LineHeight * rows
         + GUIHelper.VerticalSpacing * (rows - 1);

    private static float SectionHeight(bool expanded, float contentHeight)
        => GUIHelper.ShurikenHeaderHeight
         + (expanded ? GUIHelper.ContentSpacing + GUIHelper.ContentBottomSpacing + contentHeight : 0f);

    private static float ToggleSectionHeight(SerializedProperty enabled, float propertyHeight)
        => SectionHeight(
            enabled.boolValue || enabled.hasMultipleDifferentValues,
            propertyHeight + GUIHelper.ContentPadding * 2f);

    private static void Add(ref float total, float height)
        => total += height + GUIHelper.HeaderSpacing;

    private Rect Take(float height)
    {
        var result = new Rect(_cursor.x, _cursor.y, _cursor.width, height);
        _cursor.y = result.yMax + GUIHelper.HeaderSpacing;
        return result;
    }
}


[CanEditMultipleObjects]
[CustomEditor(typeof(DataComponent))]
internal sealed class ExpressionDataEditor : FaceTuneEditor<DataComponent>
{
    private bool _expressionExpanded = true;
    private bool _otherExpressionExpanded;

    private void OnEnable()
    {
        _otherExpressionExpanded = targets
            .Cast<DataComponent>()
            .Any(component => ExpressionGUI.HasExternalSource(component, component.Data, component.DataReference));
    }

    protected override float GetInspectorHeight()
        => ExpressionGUI.GetHeight(
            serializedObject.FindProperty(nameof(DataComponent.Data)),
            _expressionExpanded,
            _otherExpressionExpanded);

    protected override void DrawInspector(Rect position)
    {
        ExpressionGUI.Draw(
            position,
            serializedObject,
            Component,
            targets.Length,
            ref _expressionExpanded,
            ref _otherExpressionExpanded);
    }
}

[CanEditMultipleObjects]
[CustomEditor(typeof(StyleComponent))]
internal sealed class FacialStyleEditor : FaceTuneEditor<StyleComponent>
{
    private bool _expressionExpanded = true;
    private bool _otherExpressionExpanded;
    private bool _otherExpanded = true;

    private ExpressionGUIOptions ExpressionOptions => new(
        "style.expression.section.label".LG(),
        "style.getFromRenderer.button".LG(),
        GetFromRenderer);

    private void OnEnable()
    {
        _otherExpressionExpanded = targets
            .Cast<StyleComponent>()
            .Any(component => ExpressionGUI.HasExternalSource(component, component.Data, component.DataReference));
    }

    protected override float GetInspectorHeight()
        => ExpressionGUI.GetHeight(
               serializedObject.FindProperty(nameof(StyleComponent.Data)),
               _expressionExpanded,
               _otherExpressionExpanded,
               ExpressionOptions)
         + GUIHelper.HeaderSpacing
         + SectionHeight(_otherExpanded, ContentHeight(1));

    protected override void DrawInspector(Rect position)
    {
        var expressionHeight = ExpressionGUI.GetHeight(
            serializedObject.FindProperty(nameof(StyleComponent.Data)),
            _expressionExpanded,
            _otherExpressionExpanded,
            ExpressionOptions);
        var expressionRect = new Rect(position.x, position.y, position.width, expressionHeight);
        ExpressionGUI.Draw(
            expressionRect,
            serializedObject,
            Component,
            targets.Length,
            ref _expressionExpanded,
            ref _otherExpressionExpanded,
            ExpressionOptions);

        position.y = expressionRect.yMax + GUIHelper.HeaderSpacing;
        position.height = SectionHeight(_otherExpanded, ContentHeight(1));
        var header = new Rect(position.x, position.y, position.width, GUIHelper.ShurikenHeaderHeight);
        _otherExpanded = GUIHelper.DrawShuriken(
            header,
            _otherExpanded,
            "expression.group.other.label".LG());
        if (_otherExpanded)
        {
            var region = new Rect(
                position.x,
                header.yMax + GUIHelper.ContentSpacing,
                position.width,
                ContentHeight(1));
            if (Event.current.type == EventType.Repaint) GUIHelper.DrawRegion(region);
            var content = new Rect(
                region.x + GUIHelper.ContentPadding,
                region.y + GUIHelper.ContentPadding,
                region.width - GUIHelper.ContentPadding * 2f,
                GUIHelper.LineHeight);
            GUIHelper.DrawToggleLeft(
                content,
                serializedObject.FindProperty(nameof(StyleComponent.ApplyToRenderer)),
                "style.applyToRenderer.label".LG());
        }
    }

    private static float ContentHeight(int rows)
        => GUIHelper.ContentPadding * 2f
         + GUIHelper.LineHeight * rows
         + GUIHelper.VerticalSpacing * (rows - 1);

    private static float SectionHeight(bool expanded, float contentHeight)
        => GUIHelper.ShurikenHeaderHeight
         + (expanded
             ? GUIHelper.ContentSpacing + GUIHelper.ContentBottomSpacing + contentHeight
             : 0f);

    private void GetFromRenderer()
    {
        if (targets.Length != 1 || !CustomEditorUtility.TryGetContext(Component.gameObject, out var context)) return;
        var animations = context.FaceRenderer
            .GetBlendShapeWeights(context.FaceMesh)
            .ToBlendShapeAnimations()
            .ToArray();
        var property = serializedObject
            .FindProperty(nameof(StyleComponent.Data))
            .FindPropertyRelative("BlendShapeAnimations");
        CustomEditorUtility.ClearAllElements(property);
        CustomEditorUtility.AddBlendShapeAnimations(property, animations);
    }

    [MenuItem($"CONTEXT/{nameof(StyleComponent)}/Apply to SkinnedMeshRenderer")]
    private static void ApplyToSkinnedMeshRenderer(MenuCommand command)
    {
        if (command.context is not StyleComponent component) return;
        if (!CustomEditorUtility.TryGetContext(component.gameObject, out var context)) return;

        var set = new BlendShapeWeightSet();
        ExpressionDataEditorUtility.AddClipFirstFrame(component.Data, set, string.Empty);
        set.AddRange(component.Data.BlendShapeAnimations.Select(animation => animation.ToFirstFrameBlendShape()));
        Undo.RecordObject(context.FaceRenderer, "Apply Blend Shape");
        context.FaceRenderer.ApplyBlendShapes(context.FaceMesh, set, 0f);
        Selection.activeGameObject = context.FaceRenderer.gameObject;
        EditorGUIUtility.PingObject(context.FaceRenderer);
    }
}

internal static class ExpressionDataEditorUtility
{
    public static void AddClipFirstFrame(
        ExpressionData data,
        ICollection<BlendShapeWeight> resultToAdd,
        string? bodyPath,
        IReadOnlyList<BlendShapeWeightAnimation>? facialAnimations = null)
    {
        if (data.Clip == null) return;
        data.Clip.GetFirstFrameBlendShapes(data.ClipOption, resultToAdd, bodyPath, facialAnimations);
    }
}
