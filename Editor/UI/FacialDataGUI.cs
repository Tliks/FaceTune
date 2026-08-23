using Aoyon.FaceTune.Gui.ShapesEditor;

namespace Aoyon.FaceTune.Gui;

internal sealed class FacialDataSectionDrawer : ISectionDrawer, ISectionHeaderDrawer
{
    private readonly SerializedReferenceableSettings _source;
    private readonly Component _component;
    private readonly int _targetCount;

    public FacialDataSectionDrawer(
        SerializedObject serializedObject,
        Component component,
        int targetCount,
        string referencePropertyName,
        string directPropertyName)
    {
        _source = new SerializedReferenceableSettings(serializedObject, referencePropertyName, directPropertyName);
        _component = component;
        _targetCount = targetCount;
    }

    public float GetHeight() => FacialDataGUI.GetContentHeight(_source);
    public void Draw(Rect position) => FacialDataGUI.DrawContent(position, _source, _component, _targetCount);
    public float GetHeaderWidth() => SettingsReferenceGUI.GetHeaderWidth();
    public void DrawHeader(Rect position) => SettingsReferenceGUI.DrawHeader(position, _source);
}

internal static class FacialDataGUI
{
    private static readonly ReorderableListOptions AnimationListOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        HeaderContentHeight: GUIHelper.LineHeight,
        DrawHeaderContent: DrawClipRow,
        InitializeElement: property => property.CopyFrom(new BlendShapeWeightAnimation()));

    public static float GetContentHeight(SerializedReferenceableSettings source)
        => SettingsReferenceGUI.GetHeight(source, GetDirectHeight(source));

    public static void DrawContent(Rect position, SerializedReferenceableSettings source, Component component, int targetCount)
        => SettingsReferenceGUI.Draw(
            position,
            source,
            GetDirectHeight(source),
            rect => DrawDirect(rect, source, component, targetCount));

    private static float GetDirectHeight(SerializedReferenceableSettings source)
    {
        var animations = source.Direct.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations));
        return GUIHelper.GetListHeight(animations, AnimationListOptions)
             + GUIHelper.VerticalSpacing + GUIHelper.LineHeight;
    }

    private static void DrawDirect(
        Rect position,
        SerializedReferenceableSettings source,
        Component component,
        int targetCount)
    {
        var direct = source.Direct;
        var animations = direct.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations));
        position.height = GUIHelper.GetListHeight(animations, AnimationListOptions);
        GUIHelper.DrawList(position, animations, "expression.blendShapes.label".LG(), AnimationListOptions);
        position.NewLine();
        position.SetSingleHeight();
        var open = EditorGUI.PrefixLabel(position, "expression.editor.label".LG());
        using (new EditorGUI.DisabledScope(targetCount != 1))
            if (GUI.Button(open, "common.open.button".LG())) OpenEditor(component);
    }

    private static void DrawClipRow(Rect position, SerializedProperty animations)
    {
        var directPath = animations.propertyPath[..^(nameof(FacialBlendShapeData.BlendShapeAnimations).Length + 1)];
        var direct = animations.serializedObject.FindProperty(directPath);
        if (direct == null || animations.serializedObject.targetObject is not Component component) return;
        var clip = direct.FindPropertyRelative(nameof(FacialBlendShapeData.Clip));
        var option = direct.FindPropertyRelative(nameof(FacialBlendShapeData.ClipOption));
        var valueRect = position;
        var importLabel = "expression.clip.import.button".LG();
        var (fields, button) = valueRect.SplitRight(GUI.skin.button.CalcSize(importLabel).x);
        var (clipRect, optionRect) = fields.SplitRight(GUIHelper.PopupWidth(new[] { "clipImportOption.option.all".LG(), "clipImportOption.option.nonZero".LG() }));
        EditorGUI.PropertyField(clipRect, clip, GUIContent.none);
        option.enumValueIndex = EditorGUI.Popup(optionRect, option.enumValueIndex, new[] { "clipImportOption.option.all".LG(), "clipImportOption.option.nonZero".LG() });
        using (new EditorGUI.DisabledScope(animations.serializedObject.targetObjects.Length != 1 || clip.objectReferenceValue == null))
            if (GUI.Button(button, importLabel)) ImportClip(component, direct);
    }

    private static void ImportClip(Component component, SerializedProperty data)
    {
        if (data.FindPropertyRelative(nameof(FacialBlendShapeData.Clip)).objectReferenceValue is not AnimationClip clip
            || clip == null
            || !AvatarContext.TryGet(component.gameObject, out var avatar, out _)) return;
        var animations = new List<BlendShapeWeightAnimation>();
        clip.GetBlendShapeAnimations((ClipImportOption)data.FindPropertyRelative(nameof(FacialBlendShapeData.ClipOption)).enumValueIndex, animations, avatar.BodyPath);
        MergeBlendShapeAnimations(data.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations)), animations, false);
        data.FindPropertyRelative(nameof(FacialBlendShapeData.Clip)).objectReferenceValue = null;
    }

    private static void OpenEditor(Component component)
    {
        if (!AvatarContext.TryGet(component.gameObject, out var avatar, out _)) return;
        IShapesEditorTargeting? targeting = component switch
        {
            ExpressionComponent targetExpression => new FaceTuneDataTargeting { Target = targetExpression },
            ExpressionDataComponent data => new ExpressionDataTargeting { Target = data },
            SettingsComponent settings => new SettingsFacialTargeting { Target = settings },
            _ => null
        };
        if (targeting == null) return;
        var resolver = new FaceTuneResolver(avatar.Root);
        var baseAnimations = new List<BlendShapeWeightAnimation>();
        resolver.FacialData.AddIncoming(component, baseAnimations, avatar.BodyPath);
        var direct = component switch
        {
            ExpressionComponent expression => expression.FacialBlendShapes,
            ExpressionDataComponent data => data.FacialBlendShapes,
            SettingsComponent settings => settings.FacialBlendShapes,
            _ => null
        };
        if (direct != null && direct.Clip != null)
            direct.Clip.GetBlendShapeAnimations(direct.ClipOption, baseAnimations, avatar.BodyPath);
        var defaults = direct == null
            ? new BlendShapeWeightSet()
            : new BlendShapeWeightSet(direct.BlendShapeAnimations.ToFirstFrameBlendShapes());
        FacialShapesEditor.TryOpenEditor(
            avatar.FaceRenderer,
            targeting,
            new BlendShapeWeightSet(),
            new BlendShapeWeightSet(baseAnimations.ToFirstFrameBlendShapes()),
            defaults);
    }

    internal static void SetBlendShapeAnimations(SerializedProperty property, IReadOnlyList<BlendShapeWeightAnimation> animations)
    {
        property.arraySize = animations.Count;
        for (var i = 0; i < animations.Count; i++)
        {
            var element = property.GetArrayElementAtIndex(i);
            element.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue = animations[i].Name;
            element.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName).animationCurveValue = animations[i].Curve;
        }
    }

    private static void MergeBlendShapeAnimations(SerializedProperty property, IReadOnlyCollection<BlendShapeWeightAnimation> animations, bool overwrite)
    {
        var values = new List<BlendShapeWeightAnimation>();
        for (var i = 0; i < property.arraySize; i++) values.Add(new BlendShapeWeightAnimation(property.GetArrayElementAtIndex(i).FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue, property.GetArrayElementAtIndex(i).FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName).animationCurveValue));
        foreach (var animation in animations)
        {
            var index = values.FindIndex(value => value.Name == animation.Name);
            if (index < 0) values.Add(animation);
            else if (overwrite) values[index] = animation;
        }
        SetBlendShapeAnimations(property, values);
    }
}

[CustomPropertyDrawer(typeof(MultiFrameSettings))]
internal sealed class MultiFrameSettingsDrawer : PropertyDrawer
{
    private const float ParameterWarningHeight = 30f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var mode = property.FindPropertyRelative(nameof(MultiFrameSettings.MultiFrameMode));
        position.SetSingleHeight();
        GUIHelper.LocalizedEnumPopup(position, mode, "expression.multiFrame.mode.label", new[]
        {
            "expression.multiFrame.default.label",
            "expression.multiFrame.loop.label",
            "expression.multiFrame.trigger.label",
            "expression.multiFrame.parameter.label",
            "expression.multiFrame.menu.label"
        });
        if (mode.enumValueIndex is not ((int)MultiFrameSettings.Kind.Trigger
                                     or (int)MultiFrameSettings.Kind.Parameter
                                     or (int)MultiFrameSettings.Kind.Menu)) return;
        position.NewLine();
        if (mode.enumValueIndex == (int)MultiFrameSettings.Kind.Trigger)
        {
            var hand = property.FindPropertyRelative(nameof(MultiFrameSettings.TriggerHand));
            var (handLabel, handValue) = GUIHelper.SplitIndentedLabel(position);
            EditorGUI.LabelField(handLabel, "expression.multiFrame.linkedHand.label".LG());
            GUIHelper.LocalizedEnumPopup(handValue, hand, string.Empty, new[] { "hand.option.left", "hand.option.right" });
            return;
        }
        if (mode.enumValueIndex == (int)MultiFrameSettings.Kind.Parameter)
        {
            var parameter = property.FindPropertyRelative(nameof(MultiFrameSettings.ParameterName));
            var (parameterLabel, parameterValue) = GUIHelper.SplitIndentedLabel(position);
            EditorGUI.LabelField(parameterLabel, "expression.multiFrame.parameterName.label".LG());
            EditorGUI.PropertyField(parameterValue, parameter, GUIContent.none);
            if (!string.IsNullOrWhiteSpace(parameter.stringValue)) return;
            DrawWarning(ref position, "expression.multiFrame.parameterName.empty.message");
            return;
        }

        var menu = property.FindPropertyRelative(nameof(MultiFrameSettings.MenuSource));
        var (menuLabel, menuValue) = GUIHelper.SplitIndentedLabel(position);
        EditorGUI.LabelField(menuLabel, "expression.multiFrame.menuSource.label".LG());
        EditorGUI.PropertyField(menuValue, menu, GUIContent.none);
        if (menu.objectReferenceValue != null) return;
        DrawWarning(ref position, "expression.multiFrame.menuSource.empty.message");
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var mode = property.FindPropertyRelative(nameof(MultiFrameSettings.MultiFrameMode)).enumValueIndex;
        if (mode is not ((int)MultiFrameSettings.Kind.Trigger
                      or (int)MultiFrameSettings.Kind.Parameter
                      or (int)MultiFrameSettings.Kind.Menu)) return GUIHelper.LineHeight;
        var height = GUIHelper.GetLinesHeight(2);
        var isEmpty = mode switch
        {
            (int)MultiFrameSettings.Kind.Parameter => string.IsNullOrWhiteSpace(property
                .FindPropertyRelative(nameof(MultiFrameSettings.ParameterName)).stringValue),
            (int)MultiFrameSettings.Kind.Menu => property
                .FindPropertyRelative(nameof(MultiFrameSettings.MenuSource)).objectReferenceValue == null,
            _ => false
        };
        return isEmpty
            ? height + GUIHelper.VerticalSpacing + ParameterWarningHeight
            : height;
    }

    private static void DrawWarning(ref Rect position, string messageKey)
    {
        position.NewLine();
        position.height = ParameterWarningHeight;
        position.Indent();
        EditorGUI.HelpBox(position, messageKey.LS(), MessageType.Warning);
    }
}

[CustomPropertyDrawer(typeof(BlendShapeWeightAnimation))]
internal sealed class BlendShapeWeightAnimationDrawer : PropertyDrawer
{
    private const float MultiFrameDuration = 1f;
    private const float ModeToggleWidth = 24f;
    private const float PreferredNameRatio = .4f;
    private const float MinimumNameWidth = 64f;
    private const float MinimumValueWidth = 64f;
    private const float SliderWithNumberWidth = 110f;
    private static GUIContent MultiFrameToggleLabel => new(
        "M",
        "blendShapeAnimation.multiFrame.label".LS());

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        position.SetSingleHeight();
        var contentWidth = Mathf.Max(0f, position.width - ModeToggleWidth);
        var nameWidth = contentWidth * PreferredNameRatio;
        if (contentWidth >= MinimumNameWidth + MinimumValueWidth)
            nameWidth = Mathf.Clamp(nameWidth, MinimumNameWidth, contentWidth - MinimumValueWidth);
        var nameRect = new Rect(position.x, position.y, nameWidth, position.height);
        var valueRect = new Rect(nameRect.xMax, position.y, Mathf.Max(0f, position.xMax - nameRect.xMax - ModeToggleWidth), position.height);
        var modeRect = new Rect(valueRect.xMax, position.y, Mathf.Min(ModeToggleWidth, position.xMax - valueRect.xMax), position.height);
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
            value = valueRect.width >= SliderWithNumberWidth
                ? EditorGUI.Slider(valueRect, value, 0f, 100f)
                : GUI.HorizontalSlider(valueRect, value, 0f, 100f);
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
