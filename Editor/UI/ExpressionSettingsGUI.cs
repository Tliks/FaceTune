using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Gui;

internal sealed class SerializedReferenceableSettings
{
    public SerializedProperty Reference { get; }
    public SerializedProperty Mode { get; }
    public SerializedProperty Source { get; }
    public SerializedProperty Direct { get; }

    public SerializedReferenceableSettings(
        SerializedObject serializedObject,
        string referencePropertyName,
        string directPropertyName)
    {
        Reference = serializedObject.FindProperty(referencePropertyName);
        Mode = Reference.FindPropertyRelative(nameof(SettingsReference.Mode));
        Source = Reference.FindPropertyRelative(nameof(SettingsReference.Source));
        Direct = serializedObject.FindProperty(directPropertyName);
    }

    internal SectionActionSet CreateActionSet(Func<object?> createDefaultValue)
        => new(
            Direct.serializedObject,
            new[]
            {
                SectionActionField.From(Reference, () => new SettingsReference()),
                SectionActionField.From(Direct, createDefaultValue)
            });
}

internal static class SettingsReferenceGUI
{
    private const float MissingReferenceWarningHeight = 30f;
    private static readonly string[] ModeKeys = { "settingsReferenceMode.option.direct", "settingsReferenceMode.option.reference" };
    private static readonly string[] ShortModeKeys = { "settingsReferenceMode.short.direct", "settingsReferenceMode.short.reference" };

    public static float GetHeight(SerializedReferenceableSettings settings, float directHeight)
        => settings.Mode.intValue == (int)SettingsReferenceMode.Reference
            ? GUIHelper.LineHeight + (ShowsMissingReference(settings.Source)
                ? GUIHelper.VerticalSpacing + MissingReferenceWarningHeight
                : 0f)
            : directHeight;

    public static void Draw(
        Rect position,
        SerializedReferenceableSettings settings,
        float directHeight,
        Action<Rect> drawDirect)
    {
        if (settings.Mode.intValue == (int)SettingsReferenceMode.Reference)
        {
            position.SetSingleHeight();
            EditorGUI.PropertyField(position, settings.Source, "common.component.label".LG());
            position.NewLine();
            if (ShowsMissingReference(settings.Source))
            {
                position.height = MissingReferenceWarningHeight;
                EditorGUI.HelpBox(position, "settingsReference.component.empty.message".LS(), MessageType.Warning);
            }
            return;
        }

        position.height = directHeight;
        drawDirect(position);
    }

    public static float GetHeaderWidth()
        => GUIHelper.CompactPopupWidth(ShortModeKeys.Select(key => key.LG()));

    public static void DrawHeader(Rect position, SerializedReferenceableSettings settings)
    {
        var selected = settings.Mode.enumValueIndex;
        GUIHelper.CompactPopup(
            position,
            settings.Mode.hasMultipleDifferentValues ? EditorGUIUtility.TrTextContent("—") : ShortModeKeys[selected].LG(),
            ModeKeys.Select(key => key.LG()).ToArray(),
            selected,
            index =>
            {
                settings.Mode.serializedObject.UpdateIfRequiredOrScript();
                settings.Mode.enumValueIndex = index;
                settings.Mode.serializedObject.ApplyModifiedProperties();
            },
            settings.Mode.hasMultipleDifferentValues);
    }

    private static bool ShowsMissingReference(SerializedProperty source)
        => !source.hasMultipleDifferentValues && source.objectReferenceValue == null;
}

internal sealed class ReferenceableSettingsSectionDrawer : ISectionDrawer, ISectionHeaderDrawer
{
    private readonly SerializedReferenceableSettings _settings;

    public ReferenceableSettingsSectionDrawer(
        SerializedReferenceableSettings settings,
        Func<object?> createDefaultValue)
    {
        _settings = settings;
        Actions = settings.CreateActionSet(createDefaultValue);
    }

    public SectionActionSet Actions { get; }

    public float GetHeight()
        => SettingsReferenceGUI.GetHeight(
            _settings,
            EditorGUI.GetPropertyHeight(_settings.Direct, GUIContent.none, true));

    public void Draw(Rect position)
        => SettingsReferenceGUI.Draw(
            position,
            _settings,
            EditorGUI.GetPropertyHeight(_settings.Direct, GUIContent.none, true),
            rect => EditorGUI.PropertyField(rect, _settings.Direct, GUIContent.none, true));

    public float GetHeaderWidth() => SettingsReferenceGUI.GetHeaderWidth();
    public void DrawHeader(Rect position) => SettingsReferenceGUI.DrawHeader(position, _settings);
}

[CustomPropertyDrawer(typeof(EyeBlinkSettings))]
internal sealed class EyeBlinkSettingsDrawer : PropertyDrawer
{
    private static readonly EyeBlinkSettings.Kind[] ModeValues =
    {
        EyeBlinkSettings.Kind.BuiltIn,
        EyeBlinkSettings.Kind.SimpleAnimation,
        EyeBlinkSettings.Kind.CustomAnimation
    };
    private static readonly string[] ModeKeys =
    {
        "eyeBlinkMode.option.builtIn",
        "eyeBlinkMode.option.simpleAnimation",
        "eyeBlinkMode.option.customAnimation"
    };
    private static readonly string[] IntervalLabelKeys =
    {
        "eyeBlink.interval.minimum.shortLabel",
        "eyeBlink.interval.maximum.shortLabel"
    };
    private static readonly string[] DurationLabelKeys =
    {
        "eyeBlink.simple.duration.closing.shortLabel",
        "eyeBlink.simple.duration.hold.shortLabel",
        "eyeBlink.simple.duration.opening.shortLabel"
    };
    private static readonly ReorderableListOptions BlinkBlendShapesOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        InitializeElement: element => element.CopyFrom(EyeBlinkSettings.CreateDefaultBlinkBlendShape()),
        DrawHeaderAction: (position, list) => DrawBlendShapeWeightPicker(
            position,
            list,
            100f,
            FaceTuneWriteKind.EyeBlinkAnimation),
        ElementHeight: GUIHelper.LineHeight);
    private static readonly ReorderableListOptions ConflictBlendShapesOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        InitializeElement: element => element.CopyFrom(new BlendShapeWeight()),
        DrawHeaderAction: (position, list) => DrawBlendShapeWeightPicker(
            position,
            list,
            0f,
            FaceTuneWriteKind.FacialData),
        ElementHeight: GUIHelper.LineHeight);
    private static GUIStyle? _columnLabelStyle;
    private static GUIStyle ColumnLabelStyle => _columnLabelStyle ??= new GUIStyle(EditorStyles.label)
    {
        alignment = TextAnchor.MiddleCenter
    };
    private static readonly ReorderableListOptions AnimationsOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        HeaderContentHeight: GUIHelper.LineHeight,
        DrawHeaderContent: DrawClipImport,
        InitializeElement: InitializeAnimation);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var mode = property.FindPropertyRelative(nameof(EyeBlinkSettings.EyeBlinkMode));
        position.SetSingleHeight();
        DrawMode(position, mode);
        position.NewLine();

        var kind = (EyeBlinkSettings.Kind)mode.intValue;
        if (kind == EyeBlinkSettings.Kind.BuiltIn) return;

        switch (kind)
        {
            case EyeBlinkSettings.Kind.SimpleAnimation:
                DrawSimple(position, property);
                return;
            case EyeBlinkSettings.Kind.CustomAnimation:
                DrawCustom(position, property);
                return;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var kind = (EyeBlinkSettings.Kind)property
            .FindPropertyRelative(nameof(EyeBlinkSettings.EyeBlinkMode)).intValue;
        if (kind == EyeBlinkSettings.Kind.BuiltIn) return GUIHelper.LineHeight;
        var modeContentHeight = kind switch
        {
            EyeBlinkSettings.Kind.SimpleAnimation => GetSimpleHeight(property),
            EyeBlinkSettings.Kind.CustomAnimation => GetCustomHeight(property),
            _ => throw new ArgumentOutOfRangeException()
        };
        return GUIHelper.LineHeight
             + GUIHelper.VerticalSpacing + modeContentHeight;
    }

    private static void DrawMode(Rect position, SerializedProperty mode)
    {
        using var _ = new EditorGUI.PropertyScope(position, "eyeBlink.mode.label".LG(), mode);
        using var rightClick = new GUIHelper.RightClickPassthroughScope(position);
        var selected = Array.IndexOf(ModeValues, (EyeBlinkSettings.Kind)mode.intValue);
        if (selected < 0) selected = 0;
        var previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = mode.hasMultipleDifferentValues;
        var next = GUIHelper.LocalizedPopup(position, selected, "eyeBlink.mode.label", ModeKeys);
        EditorGUI.showMixedValue = previousMixed;
        if (next != selected) mode.intValue = (int)ModeValues[next];
    }

    private static float GetSimpleHeight(SerializedProperty property)
    {
        var blink = property.FindPropertyRelative(nameof(EyeBlinkSettings.SimpleBlinkBlendShapes));
        var conflicts = property.FindPropertyRelative(nameof(EyeBlinkSettings.SimpleConflictPreventionBlendShapes));
        return GUIHelper.GetListHeight(blink, BlinkBlendShapesOptions)
             + GUIHelper.VerticalSpacing + GUIHelper.GetOptionalListHeight(conflicts, ConflictBlendShapesOptions)
             + GUIHelper.VerticalSpacing + GUIHelper.GetLinesHeight(2)
             + GUIHelper.VerticalSpacing + GUIHelper.GetLinesHeight(2);
    }

    private static void DrawSimple(Rect position, SerializedProperty property)
    {
        var blink = property.FindPropertyRelative(nameof(EyeBlinkSettings.SimpleBlinkBlendShapes));
        position.height = GUIHelper.GetListHeight(blink, BlinkBlendShapesOptions);
        GUIHelper.DrawList(position, blink, "eyeBlink.simple.blinkBlendShapes.label".LG(), BlinkBlendShapesOptions);
        position.NewLine();

        var conflicts = property.FindPropertyRelative(nameof(EyeBlinkSettings.SimpleConflictPreventionBlendShapes));
        position.height = GUIHelper.GetOptionalListHeight(conflicts, ConflictBlendShapesOptions);
        GUIHelper.DrawLocalizedOptionalList(
            position,
            conflicts,
            "eyeBlink.simple.conflictBlendShapes.label".LG(),
            "common.option.none",
            "common.option.present",
            ConflictBlendShapesOptions);
        position.NewLine();

        DrawInterval(ref position, property);

        position.height = GUIHelper.GetLinesHeight(2);
        DrawDurations(position, property.FindPropertyRelative(nameof(EyeBlinkSettings.SimpleDurationsSeconds)));
        position.NewLine();
    }

    private static void DrawDurations(Rect position, SerializedProperty property)
    {
        var value = property.vector3Value;
        var values = new[] { value.x, value.y, value.z };
        if (!DrawFloatTable(
                position,
                property,
                "eyeBlink.simple.durations.label".LG(),
                DurationLabelKeys,
                values)) return;
        property.vector3Value = new Vector3(values[0], values[1], values[2]);
    }

    private static float GetCustomHeight(SerializedProperty property)
        => GUIHelper.GetListHeight(
               property.FindPropertyRelative(nameof(EyeBlinkSettings.Animations)),
               AnimationsOptions)
         + GUIHelper.VerticalSpacing + GUIHelper.GetLinesHeight(2);

    private static void DrawCustom(Rect position, SerializedProperty property)
    {
        var animations = property.FindPropertyRelative(nameof(EyeBlinkSettings.Animations));
        position.height = GUIHelper.GetListHeight(animations, AnimationsOptions);
        GUIHelper.DrawList(position, animations, "eyeBlink.animations.label".LG(), AnimationsOptions);
        position.NewLine();
        DrawInterval(ref position, property);
    }

    private static void DrawInterval(ref Rect position, SerializedProperty property)
    {
        position.height = GUIHelper.GetLinesHeight(2);
        var interval = property.FindPropertyRelative(nameof(EyeBlinkSettings.IntervalSeconds));
        var value = interval.vector2Value;
        var values = new[] { value.x, value.y };
        if (DrawFloatTable(
                position,
                interval,
                "eyeBlink.intervalSeconds.label".LG(),
                IntervalLabelKeys,
                values))
            interval.vector2Value = new Vector2(values[0], values[1]);
        position.NewLine();
    }

    private static bool DrawFloatTable(
        Rect position,
        SerializedProperty property,
        GUIContent label,
        IReadOnlyList<string> columnLabelKeys,
        float[] values)
    {
        using var scope = new EditorGUI.PropertyScope(position, label, property);
        using var rightClick = new GUIHelper.RightClickPassthroughScope(position);
        var header = new Rect(position.x, position.y, position.width, GUIHelper.LineHeight);
        var fields = EditorGUI.PrefixLabel(header, scope.content);
        var preferredWidths = Enumerable.Repeat(1f, values.Length).ToArray();
        var labelColumns = fields.FlexHorizontalSpaced(GUIHelper.HorizontalSpacing, preferredWidths);
        var valueRow = new Rect(
            fields.x,
            header.yMax + GUIHelper.VerticalSpacing,
            fields.width,
            GUIHelper.LineHeight);
        var valueColumns = valueRow.FlexHorizontalSpaced(GUIHelper.HorizontalSpacing, preferredWidths);
        var previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        for (var i = 0; i < values.Length; i++)
        {
            GUI.Label(labelColumns[i], columnLabelKeys[i].LG(), ColumnLabelStyle);
            values[i] = Mathf.Max(0f, EditorGUI.FloatField(valueColumns[i], values[i]));
        }
        var changed = EditorGUI.EndChangeCheck();
        EditorGUI.showMixedValue = previousMixed;
        return changed;
    }

    private static void InitializeAnimation(SerializedProperty property)
        => property.CopyFrom(EyeBlinkSettings.CreateDefaultAnimation());

    private static void DrawBlendShapeWeightPicker(
        Rect position,
        SerializedProperty list,
        float weight,
        FaceTuneWriteKind writeKind)
        => BlendShapeNameGUI.DrawListPicker(
            position,
            list,
            element => element.FindPropertyRelative(BlendShapeWeight.NamePropName),
            (element, name) => element.CopyFrom(new BlendShapeWeight(name, weight)),
            writeKind);

    private static void DrawClipImport(Rect position, SerializedProperty animations)
    {
        using var disabled = new EditorGUI.DisabledScope(animations.serializedObject.targetObjects.Length != 1);
        var clip = EditorGUI.ObjectField(position, GUIContent.none, null, typeof(AnimationClip), false) as AnimationClip;
        if (clip == null || animations.serializedObject.targetObject is not Component component) return;
        if (!AvatarContext.TryGet(component.gameObject, out var context, out _)) return;
        var values = new List<BlendShapeWeightAnimation>();
        clip.GetBlendShapeAnimations(ClipImportOption.All, values, context.BodyPath);
        var unavailable = AvatarContext.GetUnavailableBlendShapeNames(
            context.Root,
            FaceTuneWriteKind.EyeBlinkAnimation);
        values.RemoveAll(animation => unavailable.Contains(animation.Name));
        FacialDataGUI.SetBlendShapeAnimations(animations, values);
    }
}

[CustomPropertyDrawer(typeof(LipSyncSettings))]
internal sealed class LipSyncSettingsDrawer : PropertyDrawer
{
    private static readonly ReorderableListOptions BlendShapesOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        InitializeElement: element => element.CopyFrom(new BlendShapeWeight()),
        DrawHeaderAction: (position, list) => BlendShapeNameGUI.DrawListPicker(
            position,
            list,
            element => element.FindPropertyRelative(BlendShapeWeight.NamePropName),
            (element, name) => element.CopyFrom(new BlendShapeWeight(name, 0f)),
            FaceTuneWriteKind.FacialData),
        ElementHeight: GUIHelper.LineHeight);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var blendShapes = property.FindPropertyRelative(nameof(LipSyncSettings.CancellerBlendShapes));
        position.height = GUIHelper.GetOptionalListHeight(blendShapes, BlendShapesOptions);
        GUIHelper.DrawLocalizedOptionalList(
            position,
            blendShapes,
            "lipSync.cancellerBlendShapes.label".LG(),
            "common.option.none",
            "common.option.present",
            BlendShapesOptions);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => GUIHelper.GetOptionalListHeight(
            property.FindPropertyRelative(nameof(LipSyncSettings.CancellerBlendShapes)),
            BlendShapesOptions);

}

[CustomPropertyDrawer(typeof(TransitionSettings))]
internal sealed class TransitionSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        EditorGUI.PropertyField(
            position,
            property.FindPropertyRelative(nameof(TransitionSettings.DurationSeconds)),
            "transition.duration.label".LG());
    }
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => GUIHelper.LineHeight;
}

[CustomPropertyDrawer(typeof(PrioritySettings))]
internal sealed class PrioritySettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        EditorGUI.PropertyField(
            position,
            property.FindPropertyRelative(nameof(PrioritySettings.Priority)),
            "priority.value.label".LG());
    }
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => GUIHelper.LineHeight;
}

[CustomPropertyDrawer(typeof(ExpressionSetSettings))]
internal sealed class ExpressionSetSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        position.SetSingleHeight();
        EditorGUI.PropertyField(
            position,
            property.FindPropertyRelative(nameof(ExpressionSetSettings.DefaultSelected)),
            "expressionSet.defaultSelected.label".LG());
        position.NewLine();

        var menu = property.FindPropertyRelative(nameof(ExpressionSetSettings.Menu));
        var foldout = GUIState.Foldout(property, "ExpressionSetMenu");
        foldout.Expanded = GUIHelper.DrawFoldout(
            position,
            foldout.Expanded,
            "menuSettings.section.label".LG());
        if (!foldout.Expanded) return;

        position.NewLine();
        position.Indent();
        position.height = EditorGUI.GetPropertyHeight(menu, GUIContent.none, true);
        EditorGUI.PropertyField(position, menu, GUIContent.none, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var menu = property.FindPropertyRelative(nameof(ExpressionSetSettings.Menu));
        var foldout = GUIState.Foldout(property, "ExpressionSetMenu");
        return GUIHelper.LineHeight
             + GUIHelper.VerticalSpacing + GUIHelper.LineHeight
             + (foldout.Expanded
                 ? GUIHelper.VerticalSpacing
                   + EditorGUI.GetPropertyHeight(menu, GUIContent.none, true)
                 : 0f);
    }

}

[CustomPropertyDrawer(typeof(MMDSupportSettings))]
internal sealed class MMDSupportSettingsDrawer : PropertyDrawer
{
    private static readonly string[] SupportModeKeys =
    {
        "mmdSupport.mode.option.auto",
        "mmdSupport.mode.option.disableFxLayer",
        "mmdSupport.mode.option.disableLayers"
    };
    private static readonly ReorderableListOptions BlendShapeListOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        Controls: ReorderableListOptions.ControlsPlacement.Header,
        NestContent: false,
        InitializeElement: element => element.stringValue = string.Empty,
        DrawElementOverride: BlendShapeNameGUI.DrawStringElement,
        DrawHeaderAction: (position, list) => BlendShapeNameGUI.DrawListPicker(
            position,
            list,
            element => element,
            (element, name) => element.stringValue = name),
        ElementHeight: GUIHelper.LineHeight);
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var supportMode = property.FindPropertyRelative(nameof(MMDSupportSettings.SupportMode));
        position.SetSingleHeight();
        GUIHelper.LocalizedEnumPopup(position, supportMode, "mmdSupport.mode.label", SupportModeKeys);
        position.NewLine();
        var names = property.FindPropertyRelative(nameof(MMDSupportSettings.ExplicitBlendShapeNames));
        var usesSpecifiedNames = GUIHelper.LocalizedOptionalListPopup(
            position,
            names,
            "mmdSupport.blendShapes.label".LG(),
            "mmdSupport.blendShapes.option.auto",
            "mmdSupport.blendShapes.option.specified",
            element => element.stringValue = string.Empty);
        position.NewLine();
        if (!usesSpecifiedNames) return;
        position.Indent();
        position.height = GUIHelper.GetListHeight(names, BlendShapeListOptions);
        GUIHelper.DrawList(position, names, "mmdSupport.blendShapeName.label".LG(), BlendShapeListOptions);
    }
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var names = property.FindPropertyRelative(nameof(MMDSupportSettings.ExplicitBlendShapeNames));
        return GUIHelper.GetLinesHeight(2)
             + (GUIHelper.OptionalListEnabled(names)
                 ? GUIHelper.VerticalSpacing + GUIHelper.GetListHeight(names, BlendShapeListOptions)
                 : 0f);
    }
}
