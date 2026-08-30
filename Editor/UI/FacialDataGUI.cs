using Aoyon.FaceTune.Gui.ShapesEditor;
using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Gui;

internal sealed class FacialDataSectionDrawer : ISectionDrawer, ISectionHeaderDrawer, ISectionHeaderMenuDrawer
{
    private readonly SerializedReferenceableSettings _source;

    public FacialDataSectionDrawer(
        SerializedObject serializedObject,
        string referencePropertyName,
        string directPropertyName)
    {
        _source = new SerializedReferenceableSettings(serializedObject, referencePropertyName, directPropertyName);
        Actions = _source.CreateActionSet(() => new FacialBlendShapeData());
    }

    public SectionActionSet Actions { get; }

    public float GetHeight() => FacialDataGUI.GetContentHeight(_source);
    public void Draw(Rect position) => FacialDataGUI.DrawContent(position, _source);
    public float GetHeaderWidth() => SettingsReferenceGUI.GetHeaderWidth();
    public void DrawHeader(Rect position) => SettingsReferenceGUI.DrawHeader(position, _source);
    public void PopulateHeaderMenu(GenericMenu menu)
        => FacialDataGUI.PopulateHeaderMenu(menu, _source, Actions);
}

internal static class FacialDataGUI
{
    private static readonly ReorderableListOptions AnimationListOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        NestContent: false,
        HeaderContentHeight: GUIHelper.LineHeight,
        DrawHeaderContent: DrawClipRow,
        InitializeElement: property => property.CopyFrom(new BlendShapeWeightAnimation()),
        DrawHeaderAction: DrawEditorButton,
        ElementHeight: GUIHelper.LineHeight,
        Reorderable: false);

    public static float GetContentHeight(SerializedReferenceableSettings source)
        => SettingsReferenceGUI.GetHeight(source, GetDirectHeight(source));

    public static void DrawContent(Rect position, SerializedReferenceableSettings source)
        => SettingsReferenceGUI.Draw(
            position,
            source,
            GetDirectHeight(source),
            rect => DrawDirect(rect, source));

    internal static void PopulateHeaderMenu(
        GenericMenu menu,
        SerializedReferenceableSettings source,
        SectionActionSet actions)
    {
        var label = "expression.separate.menu".LG();
        if (CanSeparate(source))
            menu.AddItem(label, false, () => Separate(source, actions));
        else
            menu.AddDisabledItem(label);
    }

    private static bool CanSeparate(SerializedReferenceableSettings source)
    {
        var serializedObject = source.Reference.serializedObject;
        return serializedObject.targetObjects.Length == 1
               && !source.Mode.hasMultipleDifferentValues
               && source.Mode.intValue == (int)SettingsReferenceMode.Direct
               && serializedObject.targetObject is Component component
               && !EditorUtility.IsPersistent(component.gameObject);
    }

    private static void Separate(
        SerializedReferenceableSettings source,
        SectionActionSet actions)
    {
        if (!CanSeparate(source)) return;

        var serializedObject = source.Reference.serializedObject;
        serializedObject.UpdateIfRequiredOrScript();
        if (serializedObject.targetObject is not Component owner) return;

        ExpressionDataComponent? separatedData = null;
        SectionOperations.RunUndo("expression.separate.menu".LS(), () =>
        {
            var expressionData = FaceTuneRecipes.AddExpressionData(owner.transform.parent);
            separatedData = expressionData;
            using var expressionDataSerializedObject = new SerializedObject(expressionData);
            expressionDataSerializedObject.UpdateIfRequiredOrScript();
            expressionDataSerializedObject.CopyFromSerializedProperty(source.Direct);
            expressionDataSerializedObject.ApplyModifiedProperties();

            SectionOperations.ResetValues(actions);
            source.Mode.intValue = (int)SettingsReferenceMode.Reference;
            source.Source.objectReferenceValue = expressionData.transform;
            serializedObject.ApplyModifiedProperties();
        });

        if (separatedData != null)
            EditorGUIUtility.PingObject(separatedData);
    }

    private static float GetDirectHeight(SerializedReferenceableSettings source)
    {
        var animations = source.Direct.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations));
        return GUIHelper.GetListHeight(animations, AnimationListOptions);
    }

    private static void DrawDirect(
        Rect position,
        SerializedReferenceableSettings source)
    {
        var direct = source.Direct;
        var animations = direct.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations));
        position.height = GUIHelper.GetListHeight(animations, AnimationListOptions);
        GUIHelper.DrawList(position, animations, "expression.blendShapes.label".LG(), AnimationListOptions);
    }

    private static void DrawEditorButton(Rect position, SerializedProperty animations)
    {
        using var disabled = new EditorGUI.DisabledScope(animations.serializedObject.targetObjects.Length != 1);
        if (GUI.Button(position, "expression.editor.button".LG(), GUIStyles.ListButton)
            && animations.serializedObject.targetObject is Component component)
            OpenEditor(component);
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
        using (new EditorGUI.PropertyScope(optionRect, GUIContent.none, option))
        using (new GUIHelper.RightClickPassthroughScope(optionRect))
        using (new EditorGUI.DisabledScope(clip.objectReferenceValue == null))
        {
            option.enumValueIndex = EditorGUI.Popup(optionRect, option.enumValueIndex, new[] { "clipImportOption.option.all".LG(), "clipImportOption.option.nonZero".LG() });
        }
        using (new EditorGUI.DisabledScope(animations.serializedObject.targetObjects.Length != 1 || clip.objectReferenceValue == null))
            if (GUI.Button(button, importLabel)) ImportClip(component, direct);
    }

    private static void ImportClip(Component component, SerializedProperty data)
    {
        if (data.FindPropertyRelative(nameof(FacialBlendShapeData.Clip)).objectReferenceValue is not AnimationClip clip
            || clip == null
            || !AvatarContext.TryGet(component.gameObject, out var avatar, out _)) return;
        var animations = new List<BlendShapeWeightAnimation>();
        var option = (ClipImportOption)data
            .FindPropertyRelative(nameof(FacialBlendShapeData.ClipOption))
            .intValue;
        clip.GetBlendShapeAnimations(option, animations, avatar.BodyPath);
        var unavailable = AvatarContext.GetUnavailableBlendShapeNames(
            avatar.Root,
            FaceTuneWriteKind.FacialData);
        animations.RemoveAll(animation => unavailable.Contains(animation.Name));
        MergeBlendShapeAnimations(
            data.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations)),
            animations,
            false);
        data.FindPropertyRelative(nameof(FacialBlendShapeData.Clip)).objectReferenceValue = null;
    }

    private static void OpenEditor(Component component)
    {
        if (!AvatarContext.TryGet(component.gameObject, out var avatar, out _)) return;
        var source = FacialEditorSource.Create(component);
        if (source == null) return;
        var resolver = new FaceTuneResolver(avatar.Root);
        var facialAnimations = new List<BlendShapeWeightAnimation>();
        resolver.FacialData.AddIncoming(component.transform, facialAnimations, avatar.BodyPath);
        var baseAnimations = source.ResolveBaseAnimations(resolver, avatar.BodyPath);
        FacialShapesEditor.TryOpenEditor(
            avatar.FaceRenderer,
            source.Targeting,
            facialAnimations,
            baseAnimations,
            source.Direct.BlendShapeAnimations,
            AvatarContext.GetUnavailableBlendShapeNames(
                avatar.Root,
                FaceTuneWriteKind.FacialData));
    }

    private sealed record FacialEditorSource(
        IShapesEditorTargeting Targeting,
        FacialBlendShapeData Direct,
        Func<FaceTuneResolver, string, IReadOnlyList<BlendShapeWeightAnimation>> ResolveBaseAnimations)
    {
        public static FacialEditorSource? Create(Component component)
            => component switch
            {
                ExpressionComponent expression => new(
                    new FaceTuneDataTargeting { Target = expression },
                    expression.FacialBlendShapes,
                    (resolver, bodyPath) => ResolveExpressionBaseAnimations(expression, resolver, bodyPath)),
                ExpressionDataComponent data => new(
                    new ExpressionDataTargeting { Target = data },
                    data.FacialBlendShapes,
                    (resolver, bodyPath) => ResolveExpressionDataBaseAnimations(data, resolver, bodyPath)),
                SettingsComponent settings => new(
                    new SettingsFacialTargeting { Target = settings },
                    settings.FacialBlendShapes,
                    (resolver, bodyPath) => ResolveSettingsBaseAnimations(settings, resolver, bodyPath)),
                _ => null
            };
    }

    private static IReadOnlyList<BlendShapeWeightAnimation> ResolveExpressionBaseAnimations(
        ExpressionComponent expression,
        FaceTuneResolver resolver,
        string bodyPath)
    {
        var result = new List<BlendShapeWeightAnimation>();
        var expressionData = resolver.FacialData.EnumerateLocal(expression)
            .Concat(resolver.FacialData.EnumerateLocalData(expression.transform))
            .FirstOrDefault().Value;
        AddClipAnimations(expressionData, result, bodyPath);
        return result;
    }

    private static IReadOnlyList<BlendShapeWeightAnimation> ResolveExpressionDataBaseAnimations(
        ExpressionDataComponent targetData,
        FaceTuneResolver resolver,
        string bodyPath)
    {
        var result = new List<BlendShapeWeightAnimation>();
        var owner = targetData.GetComponentInParent<ExpressionComponent>(true);
        if (owner == null) return result;

        var sources = resolver.FacialData.EnumerateLocal(owner)
            .Concat(resolver.FacialData.EnumerateLocalData(owner.transform));
        foreach (var (source, data) in sources)
        {
            AddClipAnimations(data, result, bodyPath);
            if (source == targetData) break;
            foreach (var animation in data.BlendShapeAnimations) result.Add(animation);
        }
        return result;
    }

    private static IReadOnlyList<BlendShapeWeightAnimation> ResolveSettingsBaseAnimations(
        SettingsComponent settings,
        FaceTuneResolver resolver,
        string bodyPath)
    {
        var result = new List<BlendShapeWeightAnimation>();
        if (resolver.SettingsReferences.TryResolve<FacialBlendShapeData>(settings, out var settingsData))
            AddClipAnimations(settingsData, result, bodyPath);
        return result;
    }

    private static void AddClipAnimations(
        FacialBlendShapeData? data,
        ICollection<BlendShapeWeightAnimation> result,
        string bodyPath)
    {
        if (data?.Clip != null)
            data.Clip.GetBlendShapeAnimations(data.ClipOption, result, bodyPath);
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
