using Aoyon.FaceTune.Gui.ShapesEditor;
using Aoyon.FaceTune.Platforms;
using UnityEditorInternal;

namespace Aoyon.FaceTune.Gui;

internal sealed class FacialDataSectionDrawer : ISectionDrawer, ICollapsedSectionHeaderDrawer
{
    private readonly SerializedProperty _data;

    public FacialDataSectionDrawer(SerializedObject serializedObject, string directPropertyName)
    {
        _data = serializedObject.FindProperty(directPropertyName);
        Actions = new SectionActionSet(
            serializedObject,
            new[] { SectionActionField.From(_data, () => new FacialBlendShapeData()) });
    }

    public SectionActionSet Actions { get; }
    public float GetHeight() => FacialDataGUI.GetContentHeight(_data);
    public void Draw(Rect position) => FacialDataGUI.DrawContent(position, _data);

    public float GetHeaderWidth()
        => GUIHelper.CompactPopupWidth(new[]
        {
            "expression.settingSource.short.standard".LG(),
            "expression.settingSource.short.setting".LG()
        });

    public void DrawHeader(Rect position) => DrawCollapsedHeader(position);

    public void DrawCollapsedHeader(Rect position)
        => GUIHelper.CompactHeaderValue(position, GetSummary(), centered: true);

    private GUIContent GetSummary()
    {
        var references = _data.FindPropertyRelative(nameof(FacialBlendShapeData.ReferenceAnimations));
        var clips = _data.FindPropertyRelative(nameof(FacialBlendShapeData.ClipAnimations));
        var animations = _data.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations));
        if (references.hasMultipleDifferentValues
            || clips.hasMultipleDifferentValues
            || animations.hasMultipleDifferentValues)
            return EditorGUIUtility.TrTextContent("—");
        return (references.arraySize == 0 && clips.arraySize == 0 && animations.arraySize == 0
            ? "expression.settingSource.short.standard"
            : "expression.settingSource.short.setting").LG();
    }
}

internal static class FacialDataGUI
{
    private static readonly ReorderableListOptions ReferenceAnimationsOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        NestContent: false,
        InitializeElement: property => property.objectReferenceValue = null,
        ElementHeight: GUIHelper.LineHeight,
        SingleLineWhenEmpty: true);

    private static readonly ReorderableListOptions ClipAnimationsOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        NestContent: false,
        InitializeElement: property => property.CopyFrom(new FacialClipBlendShapeData()),
        DrawElementOverride: DrawClipAnimation,
        ElementHeight: GUIHelper.LineHeight,
        SingleLineWhenEmpty: true);

    private static readonly ReorderableListOptions BlendShapeAnimationsOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        NestContent: false,
        InitializeElement: property => property.CopyFrom(new BlendShapeWeightAnimation()),
        ElementHeight: GUIHelper.LineHeight,
        Reorderable: false,
        SingleLineWhenEmpty: true);

    internal static float GetContentHeight(SerializedProperty data)
    {
        var references = data.FindPropertyRelative(nameof(FacialBlendShapeData.ReferenceAnimations));
        var clips = data.FindPropertyRelative(nameof(FacialBlendShapeData.ClipAnimations));
        var animations = data.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations));
        return GUIHelper.GetListHeight(references, ReferenceAnimationsOptions)
             + GUIHelper.VerticalSpacing
             + GUIHelper.GetListHeight(clips, ClipAnimationsOptions)
             + GUIHelper.VerticalSpacing
             + GUIHelper.GetListHeight(animations, BlendShapeAnimationsOptions)
             + GUIHelper.VerticalSpacing
             + GUIHelper.LineHeight;
    }

    internal static void DrawContent(Rect position, SerializedProperty data)
    {
        var references = data.FindPropertyRelative(nameof(FacialBlendShapeData.ReferenceAnimations));
        var clips = data.FindPropertyRelative(nameof(FacialBlendShapeData.ClipAnimations));
        var animations = data.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations));

        position.height = GUIHelper.GetListHeight(references, ReferenceAnimationsOptions);
        GUIHelper.DrawList(
            position,
            references,
            "expression.facials.references.label".LG(),
            ReferenceAnimationsOptions);
        position.NewLine();
        position.height = GUIHelper.GetListHeight(clips, ClipAnimationsOptions);
        GUIHelper.DrawList(
            position,
            clips,
            "expression.facials.clips.label".LG(),
            ClipAnimationsOptions);
        position.NewLine();
        position.height = GUIHelper.GetListHeight(animations, BlendShapeAnimationsOptions);
        GUIHelper.DrawList(
            position,
            animations,
            "expression.blendShapes.label".LG(),
            BlendShapeAnimationsOptions);
        position.NewLine();
        position.height = GUIHelper.LineHeight;
        DrawEditorRow(position, animations);
    }

    internal static void SetBlendShapeAnimations(
        SerializedProperty property,
        IReadOnlyList<BlendShapeWeightAnimation> animations)
    {
        property.arraySize = animations.Count;
        for (var i = 0; i < animations.Count; i++)
        {
            var element = property.GetArrayElementAtIndex(i);
            element.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue = animations[i].Name;
            element.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName).animationCurveValue = animations[i].Curve;
        }
    }

    private static void DrawEditorRow(Rect position, SerializedProperty animations)
    {
        var button = EditorGUI.PrefixLabel(position, "facialEditor.title".LG());
        using var disabled = new EditorGUI.DisabledScope(animations.serializedObject.targetObjects.Length != 1);
        if (GUI.Button(button, "facialEditor.open.button".LG())
            && animations.serializedObject.targetObject is Component component)
            OpenEditor(component);
    }

    private static void DrawClipAnimation(Rect position, SerializedProperty clipData)
    {
        var clip = clipData.FindPropertyRelative(nameof(FacialClipBlendShapeData.Clip));
        var option = clipData.FindPropertyRelative(nameof(FacialClipBlendShapeData.ClipOption));
        var importLabel = "expression.clip.import.button".LG();
        var (fields, button) = position.SplitRight(GUI.skin.button.CalcSize(importLabel).x);
        var optionWidth = GUIHelper.PopupWidth(new[]
        {
            "clipImportOption.option.all".LG(),
            "clipImportOption.option.nonZero".LG()
        });
        var (clipRect, optionRect) = fields.SplitRight(optionWidth);
        EditorGUI.PropertyField(clipRect, clip, GUIContent.none);
        using (new EditorGUI.PropertyScope(optionRect, GUIContent.none, option))
        using (new GUIHelper.RightClickPassthroughScope(optionRect))
        using (new EditorGUI.DisabledScope(clip.objectReferenceValue == null))
        {
            option.enumValueIndex = EditorGUI.Popup(
                optionRect,
                option.enumValueIndex,
                new[]
                {
                    "clipImportOption.option.all".LG(),
                    "clipImportOption.option.nonZero".LG()
                });
        }

        using var disabled = new EditorGUI.DisabledScope(
            clipData.serializedObject.targetObjects.Length != 1
            || clip.objectReferenceValue == null);
        if (GUI.Button(button, importLabel)
            && clipData.serializedObject.targetObject is Component component)
            ImportClip(component, clipData);
    }

    private static void ImportClip(Component component, SerializedProperty clipData)
    {
        if (clipData.FindPropertyRelative(nameof(FacialClipBlendShapeData.Clip)).objectReferenceValue
                is not AnimationClip clip
            || !AvatarContext.TryGet(component.gameObject, out var avatar, out _))
            return;

        var data = FindOwningData(clipData);
        if (data == null) return;
        var animations = new List<BlendShapeWeightAnimation>();
        var option = (ClipImportOption)clipData
            .FindPropertyRelative(nameof(FacialClipBlendShapeData.ClipOption))
            .intValue;
        clip.GetBlendShapeAnimations(option, animations, avatar.BodyPath);
        var unavailable = AvatarContext.GetUnavailableBlendShapeNames(
            avatar.Root,
            FaceTuneWriteKind.FacialData);
        animations.RemoveAll(animation => unavailable.Contains(animation.Name));
        MergeBlendShapeAnimations(
            data.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations)),
            animations,
            overwrite: false);
        RemoveClipData(data, clipData);
        data.serializedObject.ApplyModifiedProperties();
    }

    private static void RemoveClipData(SerializedProperty data, SerializedProperty clipData)
    {
        var clips = data.FindPropertyRelative(nameof(FacialBlendShapeData.ClipAnimations));
        for (var index = 0; index < clips.arraySize; index++)
        {
            if (clips.GetArrayElementAtIndex(index).propertyPath != clipData.propertyPath) continue;
            clips.DeleteArrayElementAtIndex(index);
            return;
        }
    }

    private static SerializedProperty? FindOwningData(SerializedProperty clipData)
    {
        var marker = "." + nameof(FacialBlendShapeData.ClipAnimations) + ".Array";
        var markerIndex = clipData.propertyPath.IndexOf(marker, StringComparison.Ordinal);
        return markerIndex < 0
            ? null
            : clipData.serializedObject.FindProperty(clipData.propertyPath[..markerIndex]);
    }

    private static void MergeBlendShapeAnimations(
        SerializedProperty property,
        IReadOnlyCollection<BlendShapeWeightAnimation> animations,
        bool overwrite)
        => property.MergeArrayByKey(
            animations,
            element => element.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue,
            animation => animation.Name,
            (element, animation) => element.CopyFrom(animation),
            overwrite);

    private static void OpenEditor(Component component)
    {
        if (component is ExpressionComponent expression
            && expression.ExpressionDataReference.Mode == SettingsReferenceMode.Reference)
            return;
        if (!AvatarContext.TryGet(component.gameObject, out var avatar, out _)) return;

        var direct = component switch
        {
            ExpressionComponent expressionComponent => expressionComponent.FacialBlendShapes,
            ExpressionDataComponent dataComponent => dataComponent.FacialBlendShapes,
            SettingsComponent settingsComponent => settingsComponent.FacialBlendShapes,
            _ => null
        };
        IShapesEditorTargeting? targeting = component switch
        {
            ExpressionComponent expressionComponent => new FaceTuneDataTargeting { Target = expressionComponent },
            ExpressionDataComponent dataComponent => new ExpressionDataTargeting { Target = dataComponent },
            SettingsComponent settingsComponent => new SettingsFacialTargeting { Target = settingsComponent },
            _ => null
        };
        if (direct == null || targeting == null) return;

        var resolver = new FacialAnimationResolver(avatar.Root);
        var incoming = new List<BlendShapeWeightAnimation>();
        // Data自体のDefinitionはcontext-freeだが、編集時は配置先での見え方を確認できるよう
        // consumerと同じancestor Settingsを背景として表示する。
        incoming.AddRange(resolver.ResolveIncoming(component.transform, avatar.BodyPath));
        IReadOnlyList<BlendShapeWeightAnimation> baseAnimations =
            resolver.TryResolveBase(
                component,
                avatar.BodyPath,
                out var resolvedBase)
                ? resolvedBase.ToList()
                : Array.Empty<BlendShapeWeightAnimation>();
        FacialShapesEditor.TryOpenEditor(
            avatar.FaceRenderer,
            targeting,
            incoming,
            baseAnimations,
            direct.BlendShapeAnimations,
            AvatarContext.GetUnavailableBlendShapeNames(
                avatar.Root,
                FaceTuneWriteKind.FacialData));
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
        if (mode.intValue is not ((int)MultiFrameSettings.Kind.Trigger
                              or (int)MultiFrameSettings.Kind.Parameter
                              or (int)MultiFrameSettings.Kind.Menu)) return;
        position.NewLine();
        if (mode.intValue == (int)MultiFrameSettings.Kind.Trigger)
        {
            var hand = property.FindPropertyRelative(nameof(MultiFrameSettings.TriggerHand));
            var (handLabel, handValue) = GUIHelper.SplitIndentedLabel(position);
            EditorGUI.LabelField(handLabel, "expression.multiFrame.linkedHand.label".LG());
            GUIHelper.LocalizedEnumPopup(handValue, hand, string.Empty, new[] { "hand.option.left", "hand.option.right" });
            return;
        }
        if (mode.intValue == (int)MultiFrameSettings.Kind.Parameter)
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
        var mode = property.FindPropertyRelative(nameof(MultiFrameSettings.MultiFrameMode)).intValue;
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
    private const float PreferredNameRatio = .50f;
    private const float MinimumNameWidth = 64f;
    private const float MinimumValueWidth = 64f;
    private const float SliderWithNumberWidth = 90f;
    private const float SliderNumberWidth = 38f;
    private static GUIContent MultiFrameToggleLabel => new(
        "M",
        "blendShapeAnimation.multiFrame.label".LS());

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        using var rightClick = new GUIHelper.RightClickPassthroughScope(position);
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

        BlendShapeNameGUI.Draw(nameRect, name);
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
            if (valueRect.width >= SliderWithNumberWidth)
            {
                var (slider, number) = valueRect.SplitRight(SliderNumberWidth);
                value = GUI.HorizontalSlider(slider, value, 0f, 100f);
                value = Mathf.Clamp(EditorGUI.FloatField(number, value), 0f, 100f);
            }
            else
            {
                value = GUI.HorizontalSlider(valueRect, value, 0f, 100f);
            }
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
