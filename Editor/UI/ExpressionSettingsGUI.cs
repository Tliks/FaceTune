namespace Aoyon.FaceTune.Gui;

internal static class SettingsSourceGUI
{
    private const float MissingReferenceWarningHeight = 30f;
    private static readonly string[] ModeKeys = { "settingsSourceMode.option.direct", "settingsSourceMode.option.reference" };
    private static readonly string[] ShortModeKeys = { "settingsSourceMode.short.direct", "settingsSourceMode.short.reference" };

    public static float GetHeight(SerializedProperty property, float directHeight, bool includeMode = true)
    {
        var mode = property.FindPropertyRelative(nameof(FacialBlendShapeDataSource.SourceMode));
        var source = property.FindPropertyRelative(nameof(FacialBlendShapeDataSource.Source));
        var contentHeight = mode.enumValueIndex == (int)SettingsSourceMode.Reference
            ? GUIHelper.LineHeight + (ShowsMissingReference(source)
                ? GUIHelper.VerticalSpacing + MissingReferenceWarningHeight
                : 0f)
            : directHeight;
        return contentHeight + (includeMode ? GUIHelper.VerticalSpacing + GUIHelper.LineHeight : 0f);
    }

    public static void Draw(
        Rect position,
        SerializedProperty property,
        float directHeight,
        Action<Rect> drawDirect,
        bool includeMode = true)
    {
        var mode = property.FindPropertyRelative(nameof(FacialBlendShapeDataSource.SourceMode));
        if (mode.enumValueIndex == (int)SettingsSourceMode.Reference)
        {
            var source = property.FindPropertyRelative(nameof(FacialBlendShapeDataSource.Source));
            position.SetSingleHeight();
            EditorGUI.PropertyField(position, source, "common.component.label".LG());
            position.NewLine();
            if (ShowsMissingReference(source))
            {
                position.height = MissingReferenceWarningHeight;
                EditorGUI.HelpBox(position, "settingsSource.component.empty.message".LS(), MessageType.Warning);
                position.NewLine();
            }
        }
        else
        {
            position.height = directHeight;
            drawDirect(position);
            position.y += directHeight + GUIHelper.VerticalSpacing;
        }

        if (includeMode)
        {
            position.SetSingleHeight();
            GUIHelper.LocalizedEnumPopup(position, mode, "settingsSource.mode.label", ModeKeys);
        }
    }

    private static bool ShowsMissingReference(SerializedProperty source)
        => !source.hasMultipleDifferentValues && source.objectReferenceValue == null;

    public static float GetHeaderWidth(SerializedProperty property)
        => GUIHelper.CompactPopupWidth(ShortModeKeys.Select(key => key.LG()));

    public static void DrawHeader(Rect position, SerializedProperty property)
    {
        var mode = property.FindPropertyRelative(nameof(FacialBlendShapeDataSource.SourceMode));
        var selected = mode.enumValueIndex;
        GUIHelper.CompactPopup(
            position,
            mode.hasMultipleDifferentValues ? EditorGUIUtility.TrTextContent("—") : ShortModeKeys[selected].LG(),
            ModeKeys.Select(key => key.LG()).ToArray(),
            selected,
            index =>
            {
                mode.serializedObject.UpdateIfRequiredOrScript();
                mode.enumValueIndex = index;
                mode.serializedObject.ApplyModifiedProperties();
            },
            mode.hasMultipleDifferentValues);
    }
}

internal sealed class SettingsSourceSectionDrawer : ISectionDrawer, ISectionHeaderDrawer
{
    private readonly SerializedProperty _property;

    public SettingsSourceSectionDrawer(SerializedProperty property) => _property = property;

    private SerializedProperty Direct => _property.FindPropertyRelative("Direct");

    public float GetHeight()
        => SettingsSourceGUI.GetHeight(_property, EditorGUI.GetPropertyHeight(Direct, GUIContent.none, true), false);

    public void Draw(Rect position)
        => SettingsSourceGUI.Draw(
            position,
            _property,
            EditorGUI.GetPropertyHeight(Direct, GUIContent.none, true),
            rect => EditorGUI.PropertyField(rect, Direct, GUIContent.none, true),
            false);

    public float GetHeaderWidth() => SettingsSourceGUI.GetHeaderWidth(_property);
    public void DrawHeader(Rect position) => SettingsSourceGUI.DrawHeader(position, _property);
}

internal abstract class SettingsSourceDrawer : PropertyDrawer
{
    protected abstract string DirectPropertyName { get; }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var direct = property.FindPropertyRelative(DirectPropertyName);
        var directHeight = EditorGUI.GetPropertyHeight(direct, GUIContent.none, true);
        SettingsSourceGUI.Draw(
            position,
            property,
            directHeight,
            rect => EditorGUI.PropertyField(rect, direct, GUIContent.none, true));
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => SettingsSourceGUI.GetHeight(
            property,
            EditorGUI.GetPropertyHeight(property.FindPropertyRelative(DirectPropertyName), GUIContent.none, true));
}

[CustomPropertyDrawer(typeof(EyeBlinkSettingsSource))]
internal sealed class EyeBlinkSettingsSourceDrawer : SettingsSourceDrawer
{
    protected override string DirectPropertyName => nameof(EyeBlinkSettingsSource.Direct);
}

[CustomPropertyDrawer(typeof(LipSyncSettingsSource))]
internal sealed class LipSyncSettingsSourceDrawer : SettingsSourceDrawer
{
    protected override string DirectPropertyName => nameof(LipSyncSettingsSource.Direct);
}

[CustomPropertyDrawer(typeof(EyeBlinkSettings))]
internal sealed class EyeBlinkSettingsDrawer : PropertyDrawer
{
    private static readonly ReorderableListOptions AnimationsOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        HeaderContentHeight: GUIHelper.LineHeight,
        DrawHeaderContent: DrawClipImport,
        InitializeElement: InitializeAnimation);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var mode = property.FindPropertyRelative(nameof(EyeBlinkSettings.EyeBlinkMode));
        position.SetSingleHeight();
        GUIHelper.LocalizedEnumPopup(position, mode, "eyeBlink.mode.label", new[] { "eyeBlinkMode.option.builtIn", "eyeBlinkMode.option.automatic" });
        if (mode.enumValueIndex != (int)EyeBlinkSettings.Kind.Automatic) return;
        position.NewLine();
        var animations = property.FindPropertyRelative(nameof(EyeBlinkSettings.Animations));
        position.height = GUIHelper.GetListHeight(animations, AnimationsOptions);
        GUIHelper.DrawList(position, animations, "eyeBlink.animations.label".LG(), AnimationsOptions);
        position.NewLine();
        GUIHelper.DrawPropertyWithIndentedLabel(ref position, property.FindPropertyRelative(nameof(EyeBlinkSettings.IntervalSeconds)), "eyeBlink.intervalSeconds.label");
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property.FindPropertyRelative(nameof(EyeBlinkSettings.EyeBlinkMode)).enumValueIndex != (int)EyeBlinkSettings.Kind.Automatic)
            return GUIHelper.LineHeight;
        var animations = property.FindPropertyRelative(nameof(EyeBlinkSettings.Animations));
        return GUIHelper.LineHeight + GUIHelper.VerticalSpacing
             + GUIHelper.GetListHeight(animations, AnimationsOptions)
             + GUIHelper.VerticalSpacing + GUIHelper.LineHeight;
    }

    private static void InitializeAnimation(SerializedProperty property)
    {
        var animation = EyeBlinkSettings.CreateDefaultAnimations()[0];
        property.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue = animation.Name;
        property.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName).animationCurveValue = animation.Curve;
    }

    private static void DrawClipImport(Rect position, SerializedProperty animations)
    {
        using var disabled = new EditorGUI.DisabledScope(animations.serializedObject.targetObjects.Length != 1);
        var clip = EditorGUI.ObjectField(position, GUIContent.none, null, typeof(AnimationClip), false) as AnimationClip;
        if (clip == null || animations.serializedObject.targetObject is not Component component) return;
        if (!AvatarContext.TryGet(component.gameObject, out var context, out _)) return;
        var values = new List<BlendShapeWeightAnimation>();
        clip.GetBlendShapeAnimations(ClipImportOption.All, values, context.BodyPath);
        FacialDataGUI.SetBlendShapeAnimations(animations, values);
    }
}

[CustomPropertyDrawer(typeof(LipSyncSettings))]
internal sealed class LipSyncSettingsDrawer : PropertyDrawer
{
    private static readonly ReorderableListOptions BlendShapesOptions = new(Header: ReorderableListOptions.HeaderMode.Label);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var blendShapes = property.FindPropertyRelative(nameof(LipSyncSettings.CancellerBlendShapes));
        var contentHeight = GUIHelper.GetListHeight(blendShapes, BlendShapesOptions);
        var state = GUIState.Foldout(property, "LipSyncConflictPrevention", false);
        if (GUIHelper.DrawShurikenSection(
                position,
                state,
                "lipSync.conflictPrevention.section.label".LG(),
                contentHeight,
                out var content))
        {
            content.height = contentHeight;
            GUIHelper.DrawList(content, blendShapes, "lipSync.cancellerBlendShapes.label".LG(), BlendShapesOptions);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var state = GUIState.Foldout(property, "LipSyncConflictPrevention", false);
        return GUIHelper.GetShurikenSectionHeight(
            state,
            GUIHelper.GetListHeight(
                property.FindPropertyRelative(nameof(LipSyncSettings.CancellerBlendShapes)),
                BlendShapesOptions));
    }

}

[CustomPropertyDrawer(typeof(TransitionSettings))]
internal sealed class TransitionSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        => EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(TransitionSettings.DurationSeconds)), "transition.duration.label".LG());
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => GUIHelper.LineHeight;
}

[CustomPropertyDrawer(typeof(PrioritySettings))]
internal sealed class PrioritySettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        => EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(PrioritySettings.Priority)), "priority.value.label".LG());
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => GUIHelper.LineHeight;
}

[CustomPropertyDrawer(typeof(ExpressionSetSettings))]
internal sealed class ExpressionSetSettingsDrawer : PropertyDrawer
{

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        position.SetSingleHeight();
        EditorGUI.PropertyField(
            position,
            property.FindPropertyRelative(nameof(ExpressionSetSettings.DefaultSelected)),
            "expressionSet.defaultSelected.label".LG());
        position.NewLine();

        var menu = property.FindPropertyRelative(nameof(ExpressionSetSettings.Menu));
        var menuHeight = EditorGUI.GetPropertyHeight(menu, GUIContent.none, true);
        var state = GUIState.Foldout(property, "ExpressionSetMenu", false);
        var section = new Rect(
            position.x,
            position.y,
            position.width,
            GUIHelper.GetShurikenSectionHeight(state, menuHeight));
        if (GUIHelper.DrawShurikenSection(section, state, "menuSettings.section.label".LG(), menuHeight, out var content))
        {
            content.height = menuHeight;
            EditorGUI.PropertyField(content, menu, GUIContent.none, true);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var menu = property.FindPropertyRelative(nameof(ExpressionSetSettings.Menu));
        var state = GUIState.Foldout(property, "ExpressionSetMenu", false);
        return GUIHelper.LineHeight
             + GUIHelper.VerticalSpacing
             + GUIHelper.GetShurikenSectionHeight(
                 state,
                 EditorGUI.GetPropertyHeight(menu, GUIContent.none, true));
    }

}

[CustomPropertyDrawer(typeof(MMDSupportSettings))]
internal sealed class MMDSupportSettingsDrawer : PropertyDrawer
{
    private static readonly ReorderableListOptions BlendShapeListOptions = new(Header: ReorderableListOptions.HeaderMode.None, Controls: ReorderableListOptions.ControlsPlacement.Header, NestContent: false);
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var supportMode = property.FindPropertyRelative(nameof(MMDSupportSettings.SupportMode));
        position.SetSingleHeight();
        GUIHelper.LocalizedEnumPopup(position, supportMode, "mmdSupport.mode.label", new[]
        {
            "mmdSupport.mode.option.auto",
            "mmdSupport.mode.option.disableFxLayer",
            "mmdSupport.mode.option.disableLayers"
        });
        position.NewLine();
        var names = property.FindPropertyRelative(nameof(MMDSupportSettings.ExplicitBlendShapeNames));
        var selectedMode = names.arraySize == 0 ? 0 : 1;
        var nextMode = GUIHelper.LocalizedPopup(position, selectedMode, "mmdSupport.blendShapes.label", new[] { "mmdSupport.blendShapes.option.auto", "mmdSupport.blendShapes.option.specified" });
        if (nextMode != selectedMode)
        {
            if (nextMode == 0) names.ClearArray();
            else
            {
                names.InsertArrayElementAtIndex(0);
                names.GetArrayElementAtIndex(0).stringValue = string.Empty;
            }
        }
        position.NewLine();
        if (nextMode == 0) return;
        position.Indent();
        position.height = GUIHelper.GetListHeight(names, BlendShapeListOptions);
        GUIHelper.DrawList(position, names, GUIContent.none, BlendShapeListOptions);
    }
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var names = property.FindPropertyRelative(nameof(MMDSupportSettings.ExplicitBlendShapeNames));
        return GUIHelper.GetLinesHeight(2)
             + (names.arraySize == 0 ? 0f : GUIHelper.VerticalSpacing + GUIHelper.GetListHeight(names, BlendShapeListOptions));
    }
}
