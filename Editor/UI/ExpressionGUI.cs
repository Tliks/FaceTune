using Aoyon.FaceTune.Gui.ShapesEditor;

namespace Aoyon.FaceTune.Gui;

internal sealed record ExpressionGUIOptions(
    GUIContent? ExternalSourceLabel = null,
    GUIContent? FooterButtonLabel = null,
    Action? FooterButtonAction = null);

internal static class ExpressionGUI
{
    private static readonly ReorderableListOptions AnimationListOptions = new(
        Header: ReorderableListOptions.HeaderMode.Foldout,
        MaxVisibleHeight: 126f,
        InitializeElement: InitializeBlendShapeAnimation,
        Controls: ReorderableListOptions.ControlsPlacement.Header);
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

    public static float GetContentHeight(
        SerializedProperty data,
        ExpressionGUIOptions? options = null)
    {
        var height = GUIHelper.LineHeight;
        var reference = data.FindPropertyRelative(nameof(ExpressionData.DataReference));
        if (reference.isExpanded)
        {
            height += GUIHelper.VerticalSpacing + GUIHelper.LineHeight;
            height += GUIHelper.VerticalSpacing + GUIHelper.LineHeight;
        }

        var animations = data.FindPropertyRelative(nameof(ExpressionData.BlendShapeAnimations));
        height += GUIHelper.VerticalSpacing + GUIHelper.GetListHeight(animations, AnimationListOptions);
        height += GUIHelper.VerticalSpacing + GUIHelper.LineHeight;
        if (options?.FooterButtonLabel != null)
            height += GUIHelper.VerticalSpacing + GUIHelper.LineHeight;
        return height;
    }

    public static void DrawContent(
        Rect position,
        SerializedObject serializedObject,
        Component component,
        int targetCount,
        ExpressionGUIOptions? options = null)
    {
        var cursor = new Rect(position.x, position.y, position.width, GUIHelper.LineHeight);
        var data = serializedObject.FindProperty(nameof(IHasExpressionData.Data));
        var reference = data.FindPropertyRelative(nameof(ExpressionData.DataReference));

        reference.isExpanded = GUIHelper.DrawFoldout(
            cursor,
            reference.isExpanded,
            options?.ExternalSourceLabel ?? "expression.otherExpression.label".LG());

        if (reference.isExpanded)
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
        var animations = data.FindPropertyRelative(nameof(ExpressionData.BlendShapeAnimations));
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

    internal static void InitializeExpansions(SerializedProperty data)
    {
        data.FindPropertyRelative(nameof(ExpressionData.DataReference)).isExpanded = HasExternalSource(data);
        var blendShapeAnimations = data.FindPropertyRelative(nameof(ExpressionData.BlendShapeAnimations));
        blendShapeAnimations.isExpanded = blendShapeAnimations.arraySize > 0;
    }

    private static bool HasExternalSource(SerializedProperty data)
    {
        foreach (var target in data.serializedObject.targetObjects)
        {
            if (target is not Component component || component is not IHasExpressionData source) continue;
            if (source.Data.DataReference?.Get(component) != null || source.Data.Clip != null) return true;
        }
        return false;
    }

    private static void DrawClipRow(Rect position, SerializedProperty data, Component component, int targetCount)
    {
        var clip = data.FindPropertyRelative(nameof(ExpressionData.Clip));
        var option = data.FindPropertyRelative(nameof(ExpressionData.ClipOption));
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

    internal static void SetBlendShapeAnimations(
        SerializedProperty blendShapeAnimations,
        IReadOnlyList<BlendShapeWeightAnimation> animations)
    {
        var animationByName = new Dictionary<string, BlendShapeWeightAnimation>();
        foreach (var animation in animations)
            animationByName[animation.Name] = animation;

        var matchedNames = new HashSet<string>();
        for (var index = 0; index < blendShapeAnimations.arraySize; index++)
        {
            var element = blendShapeAnimations.GetArrayElementAtIndex(index);
            var name = element.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue;
            if (!animationByName.TryGetValue(name, out var animation) || !matchedNames.Add(name)) continue;

            var curve = element.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName);
            if (!animation.Curve.Equals(curve.animationCurveValue))
                curve.animationCurveValue = animation.Curve;
        }

        foreach (var animation in animations)
        {
            if (!matchedNames.Add(animation.Name)) continue;

            var index = blendShapeAnimations.arraySize;
            blendShapeAnimations.arraySize++;
            var element = blendShapeAnimations.GetArrayElementAtIndex(index);
            element.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue = animation.Name;
            element.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName).animationCurveValue = animationByName[animation.Name].Curve;
        }

        for (var index = blendShapeAnimations.arraySize - 1; index >= 0; index--)
        {
            var name = blendShapeAnimations
                .GetArrayElementAtIndex(index)
                .FindPropertyRelative(BlendShapeWeightAnimation.NamePropName)
                .stringValue;
            if (!animationByName.ContainsKey(name))
                blendShapeAnimations.DeleteArrayElementAtIndex(index);
        }
    }

    internal static void MergeBlendShapeAnimations(
        SerializedProperty blendShapeAnimations,
        IReadOnlyCollection<BlendShapeWeightAnimation> animations)
    {
        var existingAnimations = new Dictionary<string, (SerializedProperty Element, AnimationCurve Curve)>();
        for (var i = 0; i < blendShapeAnimations.arraySize; i++)
        {
            var element = blendShapeAnimations.GetArrayElementAtIndex(i);
            var name = element.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue;
            if (string.IsNullOrEmpty(name)) continue;
            existingAnimations[name] = (
                element,
                element.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName).animationCurveValue);
        }

        foreach (var animation in animations)
        {
            if (existingAnimations.TryGetValue(animation.Name, out var existing))
            {
                if (!animation.Curve.Equals(existing.Curve))
                    existing.Element.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName).animationCurveValue = animation.Curve;
                continue;
            }

            var index = blendShapeAnimations.arraySize;
            blendShapeAnimations.arraySize++;
            var element = blendShapeAnimations.GetArrayElementAtIndex(index);
            element.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue = animation.Name;
            element.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName).animationCurveValue = animation.Curve;
            existingAnimations[animation.Name] = (element, animation.Curve);
        }
    }
}

internal static class ExpressionEditorActions
{
    public static void ImportClip(Component component, SerializedProperty data)
    {
        if (component is not IHasExpressionData source || source.Data.Clip == null) return;
        if (!AvatarContext.TryGet(component.gameObject, out var context, out _)) return;

        var animations = new List<BlendShapeWeightAnimation>();
        source.Data.Clip.GetBlendShapeAnimations(
            source.Data.ClipOption,
            animations,
            context.BodyPath);
        var blendShapeAnimations = data.FindPropertyRelative(nameof(ExpressionData.BlendShapeAnimations));
        ExpressionGUI.MergeBlendShapeAnimations(blendShapeAnimations, animations);
        blendShapeAnimations.isExpanded = true;
        data.FindPropertyRelative(nameof(ExpressionData.Clip)).objectReferenceValue = null;
    }

    public static void OpenEditor(Component component)
    {
        if (component is not IHasExpressionData source) return;
        if (!AvatarContext.TryGet(component.gameObject, out var context, out _)) return;

        IShapesEditorTargeting? targeting = component switch
        {
            FaceTuneComponent faceTune => new FaceTuneDataTargeting { Target = faceTune },
            DataComponent data => new ExpressionDataTargeting { Target = data },
            StyleComponent style => new FacialStyleTargeting { Target = style },
            _ => null
        };
        if (targeting == null) return;

        var defaults = new BlendShapeWeightSet(source.Data.BlendShapeAnimations.ToFirstFrameBlendShapes());
        FacialShapesEditor.TryOpenEditor(context.FaceRenderer, targeting, defaults);
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
        return GUIHelper.GetLinesHeight(rows);
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
