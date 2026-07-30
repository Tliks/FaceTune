using Aoyon.FaceTune.Gui.ShapesEditor;

namespace Aoyon.FaceTune.Gui;

internal sealed record ExpressionGUIOptions(
    GUIContent? HeaderLabel = null,
    GUIContent? ExternalSourceLabel = null,
    GUIContent? FooterButtonLabel = null,
    Action? FooterButtonAction = null);

internal static class ExpressionGUI
{
    private static readonly ReorderableListOptions AnimationListOptions = new(
        Foldout: false,
        MaxVisibleHeight: 126f,
        InitializeElement: InitializeBlendShapeAnimation);
    private static GUIContent[] ClipImportModes => new[]
    {
        "clipImportOption.option.all".LG(),
        "clipImportOption.option.nonZero".LG()
    };

    internal static void InitializeBlendShapeAnimation(SerializedProperty property)
    {
        property.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue = string.Empty;
        property.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName).animationCurveValue = new AnimationCurve();
    }

    public static float GetHeight(
        SerializedProperty data,
        bool expanded,
        bool otherExpressionExpanded,
        ExpressionGUIOptions? options = null)
    {
        if (!expanded) return GUIHelper.ShurikenHeaderHeight;

        var height = GUIHelper.ShurikenHeaderHeight
                   + GUIHelper.ContentSpacing + GUIHelper.ContentBottomSpacing
                   + GUIHelper.ContentPadding * 2f;
        height += GUIHelper.LineHeight;
        if (otherExpressionExpanded)
        {
            height += GUIHelper.VerticalSpacing + GUIHelper.LineHeight;
            height += GUIHelper.VerticalSpacing + GUIHelper.LineHeight;
        }

        var animations = data.FindPropertyRelative("BlendShapeAnimations");
        height += GUIHelper.VerticalSpacing + GUIHelper.GetListHeight(animations, AnimationListOptions);
        height += GUIHelper.VerticalSpacing + GUIHelper.LineHeight;
        if (options?.FooterButtonLabel != null)
            height += GUIHelper.VerticalSpacing + GUIHelper.LineHeight;
        return height;
    }

    public static void Draw(
        Rect position,
        SerializedObject serializedObject,
        Component component,
        int targetCount,
        ref bool expanded,
        ref bool otherExpressionExpanded,
        ExpressionGUIOptions? options = null)
    {
        var header = new Rect(position.x, position.y, position.width, GUIHelper.ShurikenHeaderHeight);
        expanded = GUIHelper.DrawShuriken(
            header,
            expanded,
            options?.HeaderLabel ?? "expression.section.label".LG());
        if (!expanded) return;

        var content = new Rect(
            position.x,
            header.yMax + GUIHelper.ContentSpacing,
            position.width,
            position.height
            - GUIHelper.ShurikenHeaderHeight
            - GUIHelper.ContentSpacing
            - GUIHelper.ContentBottomSpacing);
        if (Event.current.type == EventType.Repaint) GUIHelper.DrawRegion(content);

        var cursor = new Rect(
            content.x + GUIHelper.ContentPadding,
            content.y + GUIHelper.ContentPadding,
            content.width - GUIHelper.ContentPadding * 2f,
            GUIHelper.LineHeight);
        var data = serializedObject.FindProperty("Data");
        var reference = serializedObject.FindProperty("DataReference");

        otherExpressionExpanded = GUIHelper.DrawFoldout(
            cursor,
            otherExpressionExpanded,
            options?.ExternalSourceLabel ?? "expression.otherExpression.label".LG());

        if (otherExpressionExpanded)
        {
            cursor.NewLine();
            cursor = Indent(cursor);
            DrawClipRow(cursor, data, component, targetCount);
            cursor.Back();

            cursor.NewLine();
            cursor = Indent(cursor);
            EditorGUI.PropertyField(
                cursor,
                reference,
                "expression.component.label".LG());
            cursor.Back();
        }

        cursor.NewLine();
        var animations = data.FindPropertyRelative("BlendShapeAnimations");
        cursor.height = GUIHelper.GetListHeight(animations, AnimationListOptions);
        GUIHelper.DrawList(
            cursor,
            animations,
            "expression.blendShapes.label".LG(),
            AnimationListOptions);

        cursor.NewLine();
        cursor.height = GUIHelper.LineHeight;
        using (new EditorGUI.DisabledScope(targetCount != 1))
        {
            if (GUI.Button(cursor, "expression.openEditor.button".LG())) ExpressionEditorActions.OpenEditor(component);
        }

        if (options?.FooterButtonLabel != null)
        {
            cursor.NewLine();
            if (GUI.Button(cursor, options.FooterButtonLabel)) options.FooterButtonAction?.Invoke();
        }
    }

    public static bool HasExternalSource(Component component, ExpressionData data, AvatarObjectReference reference)
        => reference?.Get(component) != null || data.Clip != null;

    private static void DrawClipRow(Rect position, SerializedProperty data, Component component, int targetCount)
    {
        var clip = data.FindPropertyRelative("Clip");
        var option = data.FindPropertyRelative("ClipOption");
        var fields = EditorGUI.PrefixLabel(position, "expression.clip.label".LG());
        var importLabel = "expression.clip.import.button".LG();
        var buttonWidth = GUI.skin.button.CalcSize(importLabel).x;
        var popupWidth = GUIHelper.PopupWidth(ClipImportModes);
        var (beforeButton, buttonRect) = fields.SplitRight(buttonWidth);
        var (clipRect, optionRect) = beforeButton.SplitRight(popupWidth);

        EditorGUI.PropertyField(clipRect, clip, GUIContent.none);
        var selected = option.enumValueIndex == (int)ClipImportOption.All ? 0 : 1;
        option.enumValueIndex = EditorGUI.Popup(optionRect, selected, ClipImportModes);
        using (new EditorGUI.DisabledScope(targetCount != 1 || clip.objectReferenceValue == null))
        {
            if (GUI.Button(buttonRect, importLabel)) ExpressionEditorActions.ImportClip(component, data);
        }
    }

    private static Rect Indent(Rect position)
    {
        position.x += GUIHelper.IndentWidth;
        position.width = Mathf.Max(0f, position.width - GUIHelper.IndentWidth);
        return position;
    }

}

internal static class ExpressionEditorActions
{
    public static void ImportClip(Component component, SerializedProperty data)
    {
        if (component is not IExpressionDataSource source) return;
        if (!CustomEditorUtility.TryGetContext(component.gameObject, out var context) || source.Data.Clip == null) return;

        var animations = new List<BlendShapeWeightAnimation>();
        source.Data.Clip.GetBlendShapeAnimations(
            source.Data.ClipOption,
            animations,
            context.BodyPath);
        CustomEditorUtility.AddBlendShapeAnimations(
            data.FindPropertyRelative("BlendShapeAnimations"),
            animations);
        data.FindPropertyRelative("Clip").objectReferenceValue = null;
    }

    public static void OpenEditor(Component component)
    {
        if (component is not IExpressionDataSource source) return;

        var defaults = new BlendShapeWeightSet(
            source.Data.BlendShapeAnimations.Select(animation => animation.ToFirstFrameBlendShape()));
        switch (component)
        {
            case FaceTuneComponent faceTune:
                CustomEditorUtility.OpenEditor(
                    component.gameObject,
                    new FaceTuneDataTargeting { Target = faceTune },
                    defaults);
                break;
            case DataComponent data:
                CustomEditorUtility.OpenEditor(
                    component.gameObject,
                    new ExpressionDataTargeting { Target = data },
                    defaults);
                break;
            case StyleComponent style:
                CustomEditorUtility.OpenEditor(
                    component.gameObject,
                    new FacialStyleTargeting { Target = style },
                    defaults);
                break;
        }
    }
}

[CustomPropertyDrawer(typeof(ExpressionSettings))]
internal sealed class ExpressionSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var mode = property.FindPropertyRelative(ExpressionSettings.MultiFrameModePropName);
        GUIHelper.DrawLocalizedEnum(ref position, mode, "expression.multiFrame.mode.label", nameof(MultiFrameMode));
        if (mode.enumValueIndex == (int)MultiFrameMode.Trigger)
            GUIHelper.DrawLocalizedEnum(ref position, property.FindPropertyRelative(ExpressionSettings.TriggerHandPropName), "expression.multiFrame.linkedHand.label", nameof(Hand));
        else if (mode.enumValueIndex == (int)MultiFrameMode.Parameter)
            GUIHelper.DrawProperty(ref position, property.FindPropertyRelative(ExpressionSettings.ParameterNamePropName), "expression.multiFrame.parameterName.label");
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var mode = property.FindPropertyRelative(ExpressionSettings.MultiFrameModePropName);
        var rows = mode.enumValueIndex == (int)MultiFrameMode.Trigger
            || mode.enumValueIndex == (int)MultiFrameMode.Parameter ? 2 : 1;
        return GUIHelper.LineHeight * rows + GUIHelper.VerticalSpacing * (rows - 1);
    }
}

[CustomPropertyDrawer(typeof(FacialSettings))]
internal sealed class FacialSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        GUIHelper.DrawLocalizedEnum(ref position, property.FindPropertyRelative(FacialSettings.AllowEyeBlinkPropName), "facialSettings.allowEyeBlink.label", nameof(TrackingPermission));
        GUIHelper.DrawLocalizedEnum(ref position, property.FindPropertyRelative(FacialSettings.AllowLipSyncPropName), "facialSettings.allowLipSync.label", nameof(TrackingPermission));
        GUIHelper.DrawLocalizedEnum(ref position, property.FindPropertyRelative(FacialSettings.WriteModePropName), "facialSettings.writeMode.label", nameof(ExpressionWriteMode));
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => (GUIHelper.LineHeight + GUIHelper.VerticalSpacing) * 3f;
}

[CustomPropertyDrawer(typeof(BlendShapeWeightAnimation))]
internal sealed class BlendShapeWeightAnimationDrawer : PropertyDrawer
{
    private const float MultiFrameDuration = 1f;
    private const float ModeToggleWidth = 24f;
    private static GUIContent MultiFrameToggleLabel => new(
        "M",
        "blendShapeAnimation.multiFrame.label".LS());

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        position.SetSingleHeight();
        var (nameRect, valueArea) = position.SplitRatio(.4f);
        var (modeRect, valueRect) = valueArea.SplitLeft(ModeToggleWidth);
        var name = property.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName);
        var curve = property.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName);
        var animationCurve = curve.animationCurveValue;
        var mode = animationCurve.length >= 2 ? 1 : 0;

        EditorGUI.PropertyField(nameRect, name, GUIContent.none);
        var multiFrame = GUIHelper.DrawSimpleToggle(modeRect, mode == 1, MultiFrameToggleLabel);
        var nextMode = multiFrame ? 1 : 0;
        if (nextMode != mode)
        {
            var value = animationCurve.length == 0 ? 0f : animationCurve.Evaluate(0f);
            animationCurve = nextMode == 0
                ? CreateSingleFrameCurve(value)
                : CreateMultiFrameCurve(value);
            curve.animationCurveValue = animationCurve;
            mode = nextMode;
        }

        if (mode == 0)
        {
            var value = animationCurve.length == 0 ? 0f : animationCurve.Evaluate(0f);
            EditorGUI.BeginChangeCheck();
            value = EditorGUI.Slider(valueRect, value, 0f, 100f);
            if (EditorGUI.EndChangeCheck()) curve.animationCurveValue = CreateSingleFrameCurve(value);
        }
        else
        {
            EditorGUI.PropertyField(valueRect, curve, GUIContent.none);
        }
    }

    private static AnimationCurve CreateSingleFrameCurve(float value)
        => new(new Keyframe(0f, value));

    private static AnimationCurve CreateMultiFrameCurve(float value)
        => new(
            new Keyframe(0f, value),
            new Keyframe(MultiFrameDuration, value));

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => GUIHelper.LineHeight;
}
