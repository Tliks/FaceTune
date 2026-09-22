using Aoyon.FaceTune.Platforms;
using Aoyon.FaceTune.Preview;
using Aoyon.FaceTune.Gui.ShapesEditor;

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
        Source = Reference.FindPropertyRelative(nameof(SettingsReference.ComponentSource));
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
            DrawComponentSource(position, settings);
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

    internal static void DrawComponentSource(
        Rect position,
        SerializedReferenceableSettings settings)
    {
        var current = settings.Source.objectReferenceValue as FaceTuneTagComponent;
        settings.Source.objectReferenceValue = ComponentReferenceGUI.Draw(
            position,
            "common.component.label".LG(),
            current,
            source => IsValidSource(settings.Direct, source));
    }

    private static bool IsValidSource(SerializedProperty direct, FaceTuneTagComponent? source)
    {
        if (source == null) return true;
        var name = direct.name;
        if (name.Contains("EyeBlink", StringComparison.Ordinal))
            return source is ISettingProvider<EyeBlinkSettings>;
        if (name.Contains("LipSync", StringComparison.Ordinal))
            return source is ISettingProvider<LipSyncSettings>;
        return source is IExpressionDefinitionProvider;
    }

    private static bool ShowsMissingReference(SerializedProperty source)
        => !source.hasMultipleDifferentValues && source.objectReferenceValue == null;
}

internal static class ComponentReferenceGUI
{
    internal static FaceTuneTagComponent? Draw(
        Rect position,
        GUIContent label,
        FaceTuneTagComponent? current,
        Func<FaceTuneTagComponent, bool> accepts)
    {
        if (position.Contains(Event.current.mousePosition)
            && Event.current.type is EventType.DragUpdated or EventType.DragPerform)
        {
            var candidates = DragAndDrop.objectReferences
                .SelectMany(value => value switch
                {
                    FaceTuneTagComponent component => new[] { component },
                    GameObject gameObject => gameObject.GetComponents<FaceTuneTagComponent>(),
                    _ => Array.Empty<FaceTuneTagComponent>()
                })
                .Where(accepts)
                .Distinct()
                .ToArray();
            DragAndDrop.visualMode = candidates.Length == 1
                ? DragAndDropVisualMode.Link
                : DragAndDropVisualMode.Rejected;
            if (Event.current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                Event.current.Use();
                return candidates.Length == 1 ? candidates[0] : current;
            }
            Event.current.Use();
            return current;
        }

        var selected = EditorGUI.ObjectField(
            position,
            label,
            current,
            typeof(FaceTuneTagComponent),
            true) as FaceTuneTagComponent;
        return selected == null || accepts(selected) ? selected : null;
    }
}

internal sealed class ReferenceableSettingsSectionDrawer : ISectionDrawer, ISectionHeaderDrawer
{
    private readonly SerializedReferenceableSettings _settings;
    private readonly BlendShapeValidationData? _validation;
    private readonly FaceTuneWriteKind _writeKind;

    public ReferenceableSettingsSectionDrawer(
        SerializedReferenceableSettings settings,
        Func<object?> createDefaultValue,
        FaceTuneWriteKind writeKind)
    {
        _settings = settings;
        _validation = BlendShapeValidationData.Create(settings.Direct.serializedObject);
        _writeKind = writeKind;
        Actions = settings.CreateActionSet(createDefaultValue);
    }

    public SectionActionSet Actions { get; }

    public float GetHeight()
        => SettingsReferenceGUI.GetHeight(
            _settings,
            EditorGUI.GetPropertyHeight(_settings.Direct, GUIContent.none, true));

    public void Draw(Rect position)
    {
        using var scope = BlendShapeValidationScope.Push(_validation, _writeKind);
        SettingsReferenceGUI.Draw(
            position,
            _settings,
            EditorGUI.GetPropertyHeight(_settings.Direct, GUIContent.none, true),
            rect => EditorGUI.PropertyField(rect, _settings.Direct, GUIContent.none, true));
    }

    public float GetHeaderWidth() => SettingsReferenceGUI.GetHeaderWidth();
    public void DrawHeader(Rect position) => SettingsReferenceGUI.DrawHeader(position, _settings);
}

internal static class TrackingSettingWarningGUI
{
    internal static float GetHeight(
        SerializedProperty? permission,
        bool eyeBlink,
        SerializedProperty? hasBehavior = null)
        => GetMessageKey(permission, eyeBlink, hasBehavior) is { } key
            ? GUIHelper.GetHelpBoxHeight(key.LS(), MessageType.Warning)
            : 0f;

    internal static void Draw(
        Rect position,
        SerializedProperty? permission,
        bool eyeBlink,
        SerializedProperty? hasBehavior = null)
    {
        if (GetMessageKey(permission, eyeBlink, hasBehavior) is not { } key) return;
        GUIHelper.HelpBox(position, key.LS(), MessageType.Warning);
    }

    private static string? GetMessageKey(
        SerializedProperty? permission,
        bool eyeBlink,
        SerializedProperty? hasBehavior)
    {
        if (permission == null || permission.hasMultipleDifferentValues) return null;
        if (hasBehavior != null
            && (hasBehavior.hasMultipleDifferentValues || !hasBehavior.boolValue)) return null;
        return ((TrackingPermission)permission.intValue, eyeBlink) switch
        {
            (TrackingPermission.Keep, true) => "eyeBlink.settingUnused.keep.message",
            (TrackingPermission.Disallow, true) => "eyeBlink.settingUnused.disallow.message",
            (TrackingPermission.Keep, false) => "lipSync.settingUnused.keep.message",
            (TrackingPermission.Disallow, false) => "lipSync.settingUnused.disallow.message",
            _ => null
        };
    }
}

internal static class SerializedObjectGUIContext
{
    internal static Component? GetComponent(SerializedObject serializedObject)
        => serializedObject.targetObject switch
        {
            Component component => component,
            ExpressionSettingsPreviewState preview => preview.Component,
            ExpressionDefinitionPreviewState preview => preview.Component,
            _ => null
        };
}

[CustomPropertyDrawer(typeof(EyeBlinkSettings))]
internal sealed class EyeBlinkSettingsDrawer : PropertyDrawer
{
    private const float ListMaxVisibleHeight = 63f;

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
        MaxVisibleHeight: ListMaxVisibleHeight,
        InitializeElement: element => element.CopyFrom(EyeBlinkSettings.CreateDefaultBlinkBlendShape()),
        DrawHeaderAction: (position, list) => DrawBlendShapeWeightPicker(
            position,
            list,
            100f,
            FaceTuneWriteKind.EyeBlinkAnimation),
        ElementHeight: GUIHelper.LineHeight);
    private static readonly ReorderableListOptions ConflictBlendShapesOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        MaxVisibleHeight: ListMaxVisibleHeight,
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
        MaxVisibleHeight: ListMaxVisibleHeight,
        HeaderContentHeight: GUIHelper.LineHeight,
        DrawHeaderContent: DrawClipImport,
        InitializeElement: InitializeAnimation);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUIHelper.RegisterPropertyRegion(position, property);
        var mode = property.FindPropertyRelative(nameof(EyeBlinkSettings.EyeBlinkMode));
        position.SetSingleHeight();
        DrawMode(position, mode);
        position.NewLine();

        var kind = (EyeBlinkSettings.Kind)mode.intValue;
        if (kind == EyeBlinkSettings.Kind.BuiltIn)
        {
            if (CannotResolveBuiltIn(property))
                GUIHelper.HelpBox(
                    position,
                    "eyeBlink.builtIn.unavailable.message".LS(),
                    MessageType.Warning);
            return;
        }

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
        if (kind == EyeBlinkSettings.Kind.BuiltIn)
        {
            if (!CannotResolveBuiltIn(property)) return GUIHelper.LineHeight;
            return GUIHelper.LineHeight
                 + GUIHelper.VerticalSpacing
                 + GUIHelper.GetHelpBoxHeight(
                     "eyeBlink.builtIn.unavailable.message".LS(),
                     MessageType.Warning);
        }
        var modeContentHeight = kind switch
        {
            EyeBlinkSettings.Kind.SimpleAnimation => GetSimpleHeight(property),
            EyeBlinkSettings.Kind.CustomAnimation => GetCustomHeight(property),
            _ => throw new ArgumentOutOfRangeException()
        };
        return GUIHelper.LineHeight
             + GUIHelper.VerticalSpacing + modeContentHeight;
    }

    private static bool CannotResolveBuiltIn(SerializedProperty property)
    {
        if (property.serializedObject.targetObjects.Length != 1
            || SerializedObjectGUIContext.GetComponent(property.serializedObject) is not { } component
            || !AvatarContext.TryGet(component.gameObject, out var avatar, out _))
            return false;

        return MetaversePlatformSupport.GetForAvatar(avatar.Root.transform)
            .All(support => support.GetBuiltInEyeBlinkAnimations(avatar.FaceRenderer) == null);
    }

    private static void DrawMode(Rect position, SerializedProperty mode)
    {
        GUIHelper.RegisterPropertyRegion(position, mode);
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
             + GUIHelper.VerticalSpacing + GUIHelper.GetListHeight(conflicts, ConflictBlendShapesOptions)
             + GUIHelper.VerticalSpacing + GUIHelper.GetLinesHeight(2)
             + GUIHelper.VerticalSpacing + GUIHelper.GetLinesHeight(2)
             + GUIHelper.VerticalSpacing + GUIHelper.LineHeight;
    }

    private static void DrawSimple(Rect position, SerializedProperty property)
    {
        var blink = property.FindPropertyRelative(nameof(EyeBlinkSettings.SimpleBlinkBlendShapes));
        position.height = GUIHelper.GetListHeight(blink, BlinkBlendShapesOptions);
        using (BlendShapeValidationScope.PushWriteKind(FaceTuneWriteKind.EyeBlinkAnimation))
            GUIHelper.DrawList(position, blink, "eyeBlink.simple.blinkBlendShapes.label".LG(), BlinkBlendShapesOptions);
        position.NewLine();

        var conflicts = property.FindPropertyRelative(nameof(EyeBlinkSettings.SimpleConflictPreventionBlendShapes));
        position.height = GUIHelper.GetListHeight(conflicts, ConflictBlendShapesOptions);
        using (BlendShapeValidationScope.PushWriteKind(FaceTuneWriteKind.FacialData))
            GUIHelper.DrawList(
                position,
                conflicts,
                new GUIContent(
                    "eyeBlink.simple.conflictBlendShapes.label".LS(),
                    "eyeBlink.simple.conflictBlendShapes.tooltip".LS()),
                ConflictBlendShapesOptions);
        position.NewLine();

        DrawInterval(ref position, property);

        position.height = GUIHelper.GetLinesHeight(2);
        DrawDurations(position, property.FindPropertyRelative(nameof(EyeBlinkSettings.SimpleDurationsSeconds)));
        position.NewLine();
        DrawEditorRow(position, property, ShapesEditorMode.EyeBlinkSimple);
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
         + GUIHelper.VerticalSpacing + GUIHelper.GetLinesHeight(2)
         + GUIHelper.VerticalSpacing + GUIHelper.LineHeight;

    private static void DrawCustom(Rect position, SerializedProperty property)
    {
        var animations = property.FindPropertyRelative(nameof(EyeBlinkSettings.Animations));
        position.height = GUIHelper.GetListHeight(animations, AnimationsOptions);
        using (BlendShapeValidationScope.PushWriteKind(FaceTuneWriteKind.EyeBlinkAnimation))
            GUIHelper.DrawList(position, animations, "eyeBlink.animations.label".LG(), AnimationsOptions);
        position.NewLine();
        DrawInterval(ref position, property);
        DrawEditorRow(position, property, ShapesEditorMode.EyeBlinkCustom);
    }

    private static void DrawEditorRow(
        Rect position,
        SerializedProperty property,
        ShapesEditorMode mode)
    {
        var button = EditorGUI.PrefixLabel(position.SetSingleHeight(), "facialEditor.edit.button".LG());
        using var disabled = new EditorGUI.DisabledScope(
            property.serializedObject.targetObjects.Length != 1
            || property.serializedObject.targetObject is not Component);
        if (!GUI.Button(button, "facialEditor.open.button".LG())) return;
        property.serializedObject.ApplyModifiedProperties();
        FacialShapesEditor.TryOpenSettingsEditor(property, mode);
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
        GUIHelper.RegisterPropertyRegion(position, property);
        using var rightClick = new GUIHelper.RightClickPassthroughScope(position);
        var header = new Rect(position.x, position.y, position.width, GUIHelper.LineHeight);
        var fields = EditorGUI.PrefixLabel(header, label);
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
        clip.GetBlendShapeAnimations(ClipImportOption.All, values, string.Empty);
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
    private const float ListMaxVisibleHeight = 63f;

    private static readonly LipSyncSettings.Kind[] ModeValues =
    {
        LipSyncSettings.Kind.BuiltIn,
        LipSyncSettings.Kind.Custom
    };
    private static readonly string[] ModeKeys =
    {
        "lipSync.mode.option.builtIn",
        "lipSync.mode.option.custom"
    };
    private static readonly ReorderableListOptions CancellerOptions = CreateBlendShapeOptions(
        ReorderableListOptions.HeaderMode.Label,
        0f,
        FaceTuneWriteKind.FacialData);
    private static readonly ReorderableListOptions VisemeOptions = CreateBlendShapeOptions(
        ReorderableListOptions.HeaderMode.Label,
        100f,
        FaceTuneWriteKind.LipSyncAnimation) with
    {
        NestContent = false
    };
    private const int VisemeColumns = 5;
    private const int VisemeRows = 3;
    private static readonly float[] VisemeColumnWidths =
        Enumerable.Repeat(1f, VisemeColumns).ToArray();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUIHelper.RegisterPropertyRegion(position, property);
        var mode = property.FindPropertyRelative(nameof(LipSyncSettings.Mode));
        position.SetSingleHeight();
        DrawMode(position, property, mode);
        position.NewLine();

        if ((LipSyncSettings.Kind)mode.intValue == LipSyncSettings.Kind.Custom)
        {
            var visemes = GetSerializedVisemes(property);
            var selection = GetVisemeSelection(property);
            var customPosition = position;
            customPosition.Indent();
            customPosition.height = GUIHelper.GetLinesHeight(VisemeRows);
            using (BlendShapeValidationScope.PushWriteKind(FaceTuneWriteKind.LipSyncAnimation))
                DrawVisemeGrid(customPosition, property, visemes, selection);
            customPosition.NewLine();

            if (selection >= 0)
            {
                var selected = visemes[selection];
                customPosition.height = GUIHelper.GetListHeight(selected.Shapes, VisemeOptions);
                using (BlendShapeValidationScope.PushWriteKind(FaceTuneWriteKind.LipSyncAnimation))
                    GUIHelper.DrawList(
                        customPosition,
                        selected.Shapes,
                        new GUIContent(string.Format(
                            "lipSync.selectedVisemeBlendShapes.label".LS(),
                            selected.Name)),
                        VisemeOptions);
                customPosition.NewLine();
            }
            position.y = customPosition.y;
        }
        else if (EditsCurrentSource(property))
        {
            DirectBlendShapePreview.Instance.Selected.SetVisemeHover(
                VisemeHoverSource.Inspector,
                -1);
        }

        var canceller = property.FindPropertyRelative(nameof(LipSyncSettings.CancellerBlendShapes));
        position.height = GUIHelper.GetListHeight(canceller, CancellerOptions);
        using (BlendShapeValidationScope.PushWriteKind(FaceTuneWriteKind.FacialData))
            GUIHelper.DrawList(
                position,
                canceller,
                new GUIContent(
                    "lipSync.cancellerBlendShapes.label".LS(),
                    "lipSync.cancellerBlendShapes.tooltip".LS()),
                CancellerOptions);

        position.NewLine();
        if ((LipSyncSettings.Kind)mode.intValue == LipSyncSettings.Kind.BuiltIn
            && CannotResolveBuiltIn(property))
        {
            position.height = GUIHelper.GetHelpBoxHeight(
                "lipSync.builtIn.unavailable.message".LS(),
                MessageType.Warning);
            GUIHelper.HelpBox(
                position,
                "lipSync.builtIn.unavailable.message".LS(),
                MessageType.Warning);
            position.NewLine();
        }
        DrawEditorRow(position, property);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var height = GUIHelper.LineHeight + GUIHelper.VerticalSpacing;
        var mode = property.FindPropertyRelative(nameof(LipSyncSettings.Mode));
        if ((LipSyncSettings.Kind)mode.intValue == LipSyncSettings.Kind.Custom)
        {
            var selection = GetVisemeSelection(property);
            height += GUIHelper.GetLinesHeight(VisemeRows) + GUIHelper.VerticalSpacing;
            if (selection >= 0)
            {
                var visemes = GetSerializedVisemes(property);
                var listHeight = GUIHelper.GetListHeight(visemes[selection].Shapes, VisemeOptions);
                height += listHeight + GUIHelper.VerticalSpacing;
            }
        }

        var canceller = property.FindPropertyRelative(nameof(LipSyncSettings.CancellerBlendShapes));
        height += GUIHelper.GetListHeight(canceller, CancellerOptions);
        if ((LipSyncSettings.Kind)mode.intValue == LipSyncSettings.Kind.BuiltIn
            && CannotResolveBuiltIn(property))
            height += GUIHelper.VerticalSpacing
                    + GUIHelper.GetHelpBoxHeight(
                        "lipSync.builtIn.unavailable.message".LS(),
                        MessageType.Warning);
        return height + GUIHelper.VerticalSpacing + GUIHelper.LineHeight;
    }

    private static bool CannotResolveBuiltIn(SerializedProperty property)
    {
        if (property.serializedObject.targetObjects.Length != 1
            || SerializedObjectGUIContext.GetComponent(property.serializedObject) is not { } component
            || !AvatarContext.TryGet(component.gameObject, out var avatar, out _))
            return false;

        return MetaversePlatformSupport.GetForAvatar(avatar.Root.transform)
            .All(support => support.GetBuiltInLipSyncShapes(avatar.FaceRenderer) == null);
    }

    private static void DrawMode(
        Rect position,
        SerializedProperty settings,
        SerializedProperty mode)
    {
        GUIHelper.RegisterPropertyRegion(position, mode);
        using var rightClick = new GUIHelper.RightClickPassthroughScope(position);
        var selected = Array.IndexOf(ModeValues, (LipSyncSettings.Kind)mode.intValue);
        if (selected < 0) selected = 0;
        var previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = mode.hasMultipleDifferentValues;
        var next = GUIHelper.LocalizedPopup(position, selected, "lipSync.mode.label", ModeKeys);
        EditorGUI.showMixedValue = previousMixed;
        if (next == selected) return;

        mode.intValue = (int)ModeValues[next];
        if (ModeValues[selected] == LipSyncSettings.Kind.BuiltIn
            && ModeValues[next] == LipSyncSettings.Kind.Custom)
            InitializeCustomShapes(settings);
    }

    private static void InitializeCustomShapes(SerializedProperty settings)
    {
        if (settings.serializedObject.targetObjects.Length != 1
            || settings.serializedObject.targetObject is not Component component
            || !AvatarContext.TryGet(component.gameObject, out var avatar, out _))
            return;

        var shapes = MetaversePlatformSupport.GetForAvatar(avatar.Root.transform)
            .Select(support => support.GetBuiltInLipSyncShapes(avatar.FaceRenderer))
            .FirstOrDefault(value => value != null);
        if (shapes != null)
            settings.FindPropertyRelative(nameof(LipSyncSettings.Shapes)).CopyFrom(shapes);
    }

    private static void DrawEditorRow(Rect position, SerializedProperty property)
    {
        var button = EditorGUI.PrefixLabel(position.SetSingleHeight(), "facialEditor.edit.button".LG());
        using var disabled = new EditorGUI.DisabledScope(
            property.serializedObject.targetObjects.Length != 1
            || property.serializedObject.targetObject is not Component);
        if (!GUI.Button(button, "facialEditor.open.button".LG())) return;
        property.serializedObject.ApplyModifiedProperties();
        var selection = (LipSyncSettings.Kind)property
            .FindPropertyRelative(nameof(LipSyncSettings.Mode)).intValue == LipSyncSettings.Kind.Custom
            ? GetVisemeSelection(property) + 1
            : 0;
        FacialShapesEditor.TryOpenSettingsEditor(
            property,
            ShapesEditorMode.LipSync,
            Mathf.Max(0, selection));
    }

    private static ReorderableListOptions CreateBlendShapeOptions(
        ReorderableListOptions.HeaderMode header,
        float initialWeight,
        FaceTuneWriteKind writeKind)
        => new(
            Header: header,
            MaxVisibleHeight: ListMaxVisibleHeight,
            InitializeElement: element => element.CopyFrom(new BlendShapeWeight()),
            DrawHeaderAction: (position, list) => BlendShapeNameGUI.DrawListPicker(
                position,
                list,
                element => element.FindPropertyRelative(BlendShapeWeight.NamePropName),
                (element, name) => element.CopyFrom(new BlendShapeWeight(name, initialWeight)),
                writeKind),
            ElementHeight: GUIHelper.LineHeight);

    private static void DrawVisemeGrid(
        Rect position,
        SerializedProperty property,
        IReadOnlyList<(string Name, SerializedProperty Shapes)> visemes,
        int selection)
    {
        var hovered = -1;
        for (var row = 0; row < VisemeRows; row++)
        {
            var rowRect = new Rect(
                position.x,
                position.y + row * (GUIHelper.LineHeight + GUIHelper.VerticalSpacing),
                position.width,
                GUIHelper.LineHeight);
            var cells = rowRect.FlexHorizontalSpaced(
                GUIHelper.HorizontalSpacing,
                VisemeColumnWidths);
            for (var column = 0; column < VisemeColumns; column++)
            {
                var index = row * VisemeColumns + column;
                var (name, shapes) = visemes[index];
                var cell = cells[column];
                if (cell.Contains(Event.current.mousePosition)) hovered = index;
                GUIHelper.RegisterPropertyRegion(cell, shapes);
                using (new GUIHelper.RightClickPassthroughScope(cell))
                {
                    var selected = selection == index;
                    if (GUI.Toggle(cell, selected, name, EditorStyles.miniButton) != selected)
                        SetVisemeSelection(property, selected ? -1 : index);
                }

                if (shapes.arraySize == 0)
                {
                    var marker = new Rect(cell.xMax - 6f, cell.y + 3f, 3f, 3f);
                    EditorGUI.DrawRect(marker, EditorGUIUtility.isProSkin
                        ? new Color(.75f, .75f, .75f)
                        : new Color(.3f, .3f, .3f));
                }
                if (shapes.prefabOverride)
                {
                    var marker = new Rect(cell.x + 1f, cell.yMax - 2f, cell.width - 2f, 2f);
                    EditorGUI.DrawRect(marker, new Color(.25f, .55f, 1f));
                }
            }
        }

        if (EditsCurrentSource(property)
            && (Event.current.type == EventType.Repaint
                || Event.current.type == EventType.MouseMove
                || Event.current.type == EventType.MouseLeaveWindow))
        {
            var nextHover = Event.current.type == EventType.MouseLeaveWindow
                ? -1
                : hovered;
            DirectBlendShapePreview.Instance.Selected.SetVisemeHover(
                VisemeHoverSource.Inspector,
                nextHover);
        }
    }

    private static int GetVisemeSelection(SerializedProperty property)
    {
        var preview = DirectBlendShapePreview.Instance.Selected;
        return EditsCurrentSource(property)
            ? preview.SelectedViseme
            : GUIState.Get(property, "lipSyncViseme", () => new VisemeSelectionState()).Index;
    }

    private static void SetVisemeSelection(SerializedProperty property, int index)
    {
        var preview = DirectBlendShapePreview.Instance.Selected;
        if (EditsCurrentSource(property))
            preview.SetVisemeSelection(index);
        else
            GUIState.Get(property, "lipSyncViseme", () => new VisemeSelectionState()).Index = index;
    }

    private static bool EditsCurrentSource(SerializedProperty property)
    {
        if (property.serializedObject.targetObjects.Length != 1) return false;
        var target = property.serializedObject.targetObject;
        var source = DirectBlendShapePreview.Instance.Selected.CurrentSource;
        if (!ReferenceEquals(target, source)) return false;
        return property.propertyPath == nameof(ExpressionComponent.LipSync);
    }

    private sealed class VisemeSelectionState
    {
        public int Index = -1;
    }

    private static (string Name, SerializedProperty Shapes)[] GetSerializedVisemes(
        SerializedProperty property)
    {
        var shapes = property.FindPropertyRelative(nameof(LipSyncSettings.Shapes));
        var properties = VrcVisemeLipSyncShapes.PropertyNames
            .Select(shapes.FindPropertyRelative);
        return VrcVisemeLipSyncShapes.Names
            .Zip(properties, (name, shapesProperty) => (name, shapesProperty))
            .ToArray();
    }
}

[CustomPropertyDrawer(typeof(TransitionSettings))]
internal sealed class TransitionSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUIHelper.RegisterPropertyRegion(position, property);
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
        GUIHelper.RegisterPropertyRegion(position, property);
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
        GUIHelper.RegisterPropertyRegion(position, property);
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
        GUIHelper.RegisterPropertyRegion(position, property);
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
