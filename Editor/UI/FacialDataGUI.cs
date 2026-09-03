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
            "expression.facials.mode.short.simple".LG(),
            "expression.facials.mode.short.composite".LG()
        });

    public void DrawHeader(Rect position)
    {
        var mode = _data.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeMode));
        var selected = mode.intValue == (int)FacialBlendShapeData.Mode.Composite ? 1 : 0;
        GUIHelper.CompactPopup(
            position,
            mode.hasMultipleDifferentValues
                ? EditorGUIUtility.TrTextContent("—")
                : (selected == 0
                    ? "expression.facials.mode.short.simple"
                    : "expression.facials.mode.short.composite").LG(),
            new[]
            {
                "expression.facials.mode.simple".LG(),
                "expression.facials.mode.composite".LG()
            },
            selected,
            next => FacialDataGUI.ChangeMode(_data, next == 0
                ? FacialBlendShapeData.Mode.Simple
                : FacialBlendShapeData.Mode.Composite),
            mode.hasMultipleDifferentValues);
    }

    public void DrawCollapsedHeader(Rect position) => DrawHeader(position);
}

internal static class FacialDataGUI
{
    private static readonly ReorderableListOptions BlendShapeAnimationsOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        NestContent: false,
        InitializeElement: property => property.CopyFrom(new BlendShapeWeightAnimation()),
        ElementHeight: GUIHelper.LineHeight,
        Reorderable: false,
        SingleLineWhenEmpty: true);

    private static readonly ReorderableListOptions CompositeEntriesOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        MaxVisibleHeight: null,
        InitializeElement: property => property.CopyFrom(new FacialBlendShapeData.CompositeEntry()));

    internal static readonly ReorderableListOptions DirectEntryAnimationsOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        NestContent: false,
        InitializeElement: property => property.CopyFrom(new BlendShapeWeightAnimation()),
        DrawHeaderAction: DrawDirectEditorButton,
        DrawHeaderLabelOverride: DrawDirectKindPopup,
        HeaderActionWidth: 70f,
        ElementHeight: GUIHelper.LineHeight,
        Reorderable: false,
        SingleLineWhenEmpty: false);

    internal static float GetContentHeight(SerializedProperty data)
    {
        var mode = (FacialBlendShapeData.Mode)data
            .FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeMode)).intValue;
        if (mode == FacialBlendShapeData.Mode.Composite)
            return GUIHelper.GetListHeight(
                data.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntries)),
                CompositeEntriesOptions);

        var animations = data.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations));
        return GUIHelper.LineHeight
             + GUIHelper.VerticalSpacing + GUIHelper.GetListHeight(animations, BlendShapeAnimationsOptions)
             + GUIHelper.VerticalSpacing + GUIHelper.LineHeight;
    }

    internal static void DrawContent(Rect position, SerializedProperty data)
    {
        var mode = (FacialBlendShapeData.Mode)data
            .FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeMode)).intValue;
        if (mode == FacialBlendShapeData.Mode.Composite)
        {
            var entries = data.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntries));
            position.height = GUIHelper.GetListHeight(entries, CompositeEntriesOptions);
            GUIHelper.DrawList(
                position,
                entries,
                "expression.facials.compositeEntries.label".LG(),
                CompositeEntriesOptions);
            return;
        }

        position.SetSingleHeight();
        DrawSimpleSource(position, data);
        position.NewLine();
        var animations = data.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations));
        position.height = GUIHelper.GetListHeight(animations, BlendShapeAnimationsOptions);
        GUIHelper.DrawList(position, animations, "expression.blendShapes.label".LG(), BlendShapeAnimationsOptions);
        position.NewLine();
        position.height = GUIHelper.LineHeight;
        DrawEditorRow(position, data, animations);
    }

    internal static void ChangeMode(SerializedProperty data, FacialBlendShapeData.Mode next)
    {
        data.serializedObject.UpdateIfRequiredOrScript();
        var mode = data.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeMode));
        if ((FacialBlendShapeData.Mode)mode.intValue == next) return;
        if (next == FacialBlendShapeData.Mode.Composite)
            ConvertSimpleToComposite(data);
        else
            ConvertCompositeToSimple(data);
        mode.intValue = (int)next;
        data.serializedObject.ApplyModifiedProperties();
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

    private static void DrawSimpleSource(Rect position, SerializedProperty data)
    {
        var source = data.FindPropertyRelative(nameof(FacialBlendShapeData.BaseSource));
        var sourceKeys = new[]
        {
            "expression.facials.baseSource.clip",
            "expression.facials.baseSource.component"
        };
        var importLabel = "expression.clip.import.button".LG();
        var importWidth = GUI.skin.button.CalcSize(importLabel).x;
        var (popupArea, value) = GUIHelper.SplitLabel(position);
        var selected = source.intValue == (int)FacialBlendShapeData.SimpleBaseSource.Reference ? 1 : 0;
        var popup = popupArea;
        popup.width = GUIHelper.LocalizedPopupWidth(
            sourceKeys[selected],
            PopupPresentation.Compact);
        var (field, importButton) = value.SplitRight(importWidth);
        var previous = source.intValue;
        GUIHelper.LocalizedEnumPopup(
            popup,
            source,
            string.Empty,
            sourceKeys,
            PopupPresentation.Compact);
        if (source.intValue != previous)
        {
            data.FindPropertyRelative(nameof(FacialBlendShapeData.Clip)).objectReferenceValue = null;
            data.FindPropertyRelative(nameof(FacialBlendShapeData.ClipOption)).intValue = (int)ClipImportOption.NonZero;
            data.FindPropertyRelative(nameof(FacialBlendShapeData.ReferenceSource)).objectReferenceValue = null;
        }

        var hasSource = false;
        if (source.intValue == (int)FacialBlendShapeData.SimpleBaseSource.Reference)
        {
            var reference = data.FindPropertyRelative(nameof(FacialBlendShapeData.ReferenceSource));
            DrawFacialComponentField(field, reference);
            hasSource = reference.objectReferenceValue != null;
        }
        else
        {
            var clip = data.FindPropertyRelative(nameof(FacialBlendShapeData.Clip));
            var option = data.FindPropertyRelative(nameof(FacialBlendShapeData.ClipOption));
            var optionWidth = GUIHelper.MaxLocalizedPopupWidth(new[]
            {
                "clipImportOption.option.all",
                "clipImportOption.option.nonZero"
            });
            var (clipRect, optionRect) = field.SplitRight(optionWidth);
            EditorGUI.PropertyField(clipRect, clip, GUIContent.none);
            using (new EditorGUI.DisabledScope(clip.objectReferenceValue == null))
                GUIHelper.LocalizedEnumPopup(optionRect, option, string.Empty, new[]
                {
                    "clipImportOption.option.all",
                    "clipImportOption.option.nonZero"
                });
            hasSource = clip.objectReferenceValue != null;
        }

        using var disabled = new EditorGUI.DisabledScope(
            !hasSource || data.serializedObject.targetObjects.Length != 1);
        if (GUI.Button(importButton, importLabel)) ImportSimpleBase(data);
    }

    private static void DrawFacialComponentField(Rect position, SerializedProperty property)
    {
        var current = property.objectReferenceValue as FaceTuneTagComponent;
        property.objectReferenceValue = ComponentReferenceGUI.Draw(
            position,
            GUIContent.none,
            current,
            source => source is ISettingProvider<FacialBlendShapeData>);
    }

    private static void ImportSimpleBase(SerializedProperty data)
    {
        if (data.serializedObject.targetObject is not Component owner
            || !AvatarContext.TryGet(owner.gameObject, out var avatar, out _))
            return;

        var values = new List<BlendShapeWeightAnimation>();
        var source = (FacialBlendShapeData.SimpleBaseSource)data
            .FindPropertyRelative(nameof(FacialBlendShapeData.BaseSource)).intValue;
        if (source == FacialBlendShapeData.SimpleBaseSource.Clip)
        {
            if (data.FindPropertyRelative(nameof(FacialBlendShapeData.Clip)).objectReferenceValue
                    is not AnimationClip clip)
                return;
            var option = (ClipImportOption)data
                .FindPropertyRelative(nameof(FacialBlendShapeData.ClipOption)).intValue;
            clip.GetBlendShapeAnimations(option, values, avatar.BodyPath);
            data.FindPropertyRelative(nameof(FacialBlendShapeData.Clip)).objectReferenceValue = null;
        }
        else
        {
            if (data.FindPropertyRelative(nameof(FacialBlendShapeData.ReferenceSource)).objectReferenceValue
                    is not FaceTuneTagComponent reference
                || !new FacialAnimationResolver(avatar.Root)
                    .TryResolve(reference, avatar.BodyPath, out var resolved))
                return;
            values.AddRange(resolved);
            data.FindPropertyRelative(nameof(FacialBlendShapeData.ReferenceSource)).objectReferenceValue = null;
        }

        var unavailable = AvatarContext.GetUnavailableBlendShapeNames(
            avatar.Root,
            FaceTuneWriteKind.FacialData);
        values.RemoveAll(animation => unavailable.Contains(animation.Name));
        var local = data.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations));
        foreach (var animation in values) MergeAnimation(local, animation, overwrite: false);
        data.serializedObject.ApplyModifiedProperties();
    }

    private static void DrawEditorRow(
        Rect position,
        SerializedProperty data,
        SerializedProperty animations)
    {
        var button = EditorGUI.PrefixLabel(position, "facialEditor.title".LG());
        using var disabled = new EditorGUI.DisabledScope(animations.serializedObject.targetObjects.Length != 1);
        if (GUI.Button(button, "facialEditor.open.button".LG())
            && animations.serializedObject.targetObject is Component component)
            OpenEditor(component, animations, null);
    }

    private static void DrawDirectKindPopup(Rect position, SerializedProperty animations)
    {
        var suffix = "." + nameof(FacialBlendShapeData.CompositeEntry.BlendShapeAnimations);
        if (!animations.propertyPath.EndsWith(suffix, StringComparison.Ordinal)) return;
        var entry = animations.serializedObject.FindProperty(
            animations.propertyPath[..^suffix.Length]);
        if (entry != null) FacialCompositeEntryDrawer.DrawKindPopup(position, entry);
    }

    private static void DrawDirectEditorButton(Rect position, SerializedProperty animations)
    {
        using var disabled = new EditorGUI.DisabledScope(animations.serializedObject.targetObjects.Length != 1);
        if (!GUI.Button(position, "facialEditor.edit.button".LG())
            || animations.serializedObject.targetObject is not Component component)
            return;
        var data = FindOwningFacialData(animations);
        var entryIndex = FindCompositeEntryIndex(animations);
        if (data != null && entryIndex >= 0)
            OpenEditor(component, animations, entryIndex);
    }

    private static void OpenEditor(
        Component component,
        SerializedProperty animations,
        int? compositeEntryIndex)
    {
        if (component is ExpressionComponent expression
            && expression.ExpressionDataReference.Mode == SettingsReferenceMode.Reference)
            return;
        if (!AvatarContext.TryGet(component.gameObject, out var avatar, out _)) return;

        IShapesEditorTargeting? targeting = component switch
        {
            ExpressionComponent expressionComponent => new FaceTuneDataTargeting { Target = expressionComponent },
            ExpressionDataComponent dataComponent => new ExpressionDataTargeting { Target = dataComponent },
            SettingsComponent settingsComponent => new SettingsFacialTargeting { Target = settingsComponent },
            _ => null
        };
        if (targeting is not IFacialSourceTargeting targetingSource) return;
        targetingSource.AnimationPropertyPath = animations.propertyPath;

        var resolver = new FacialAnimationResolver(avatar.Root);
        var incoming = resolver.ResolveIncoming(component.transform, avatar.BodyPath).ToList();
        BlendShapeWeightAnimationSet? resolvedBase;
        var hasBase = compositeEntryIndex is { } index
            ? resolver.TryResolveCompositeBase(component, avatar.BodyPath, index, out resolvedBase)
            : resolver.TryResolveBase(component, avatar.BodyPath, out resolvedBase);
        FacialShapesEditor.TryOpenEditor(
            avatar.FaceRenderer,
            targeting,
            incoming,
            hasBase ? resolvedBase!.ToList() : Array.Empty<BlendShapeWeightAnimation>(),
            ReadAnimations(animations).ToList(),
            AvatarContext.GetUnavailableBlendShapeNames(avatar.Root, FaceTuneWriteKind.FacialData));
    }

    private static void ConvertSimpleToComposite(SerializedProperty data)
    {
        var entries = data.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntries));
        entries.ClearArray();
        var baseSource = (FacialBlendShapeData.SimpleBaseSource)data
            .FindPropertyRelative(nameof(FacialBlendShapeData.BaseSource)).intValue;
        var hasBase = baseSource == FacialBlendShapeData.SimpleBaseSource.Clip
            ? data.FindPropertyRelative(nameof(FacialBlendShapeData.Clip)).objectReferenceValue != null
            : data.FindPropertyRelative(nameof(FacialBlendShapeData.ReferenceSource)).objectReferenceValue != null;
        if (hasBase)
        {
            entries.InsertArrayElementAtIndex(entries.arraySize);
            var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.CopyFrom(new FacialBlendShapeData.CompositeEntry());
            entry.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.EntryKind)).intValue =
                baseSource == FacialBlendShapeData.SimpleBaseSource.Clip
                    ? (int)FacialBlendShapeData.CompositeEntry.Kind.Clip
                    : (int)FacialBlendShapeData.CompositeEntry.Kind.Reference;
            if (baseSource == FacialBlendShapeData.SimpleBaseSource.Clip)
            {
                entry.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.Clip)).objectReferenceValue =
                    data.FindPropertyRelative(nameof(FacialBlendShapeData.Clip)).objectReferenceValue;
                entry.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.ClipOption)).intValue =
                    data.FindPropertyRelative(nameof(FacialBlendShapeData.ClipOption)).intValue;
            }
            else
            {
                entry.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.ReferenceSource)).objectReferenceValue =
                    data.FindPropertyRelative(nameof(FacialBlendShapeData.ReferenceSource)).objectReferenceValue;
            }
        }
        var local = data.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations));
        if (local.arraySize > 0)
        {
            entries.InsertArrayElementAtIndex(entries.arraySize);
            var direct = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            direct.CopyFrom(new FacialBlendShapeData.CompositeEntry());
            direct.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.EntryKind)).intValue =
                (int)FacialBlendShapeData.CompositeEntry.Kind.Direct;
            SetBlendShapeAnimations(
                direct.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.BlendShapeAnimations)),
                ReadAnimations(local).ToList());
        }
        ClearSimple(data);
    }

    private static void ConvertCompositeToSimple(SerializedProperty data)
    {
        var entries = data.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntries));
        var sourceCount = 0;
        var sawDirect = false;
        var requiresFlatten = false;
        for (var i = 0; i < entries.arraySize; i++)
        {
            var kind = (FacialBlendShapeData.CompositeEntry.Kind)entries
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.EntryKind))
                .intValue;
            if (kind == FacialBlendShapeData.CompositeEntry.Kind.Direct)
            {
                sawDirect = true;
                continue;
            }
            sourceCount++;
            requiresFlatten |= sourceCount > 1 || sawDirect;
        }

        IReadOnlyList<BlendShapeWeightAnimation>? flattened = null;
        if (requiresFlatten
            && data.serializedObject.targetObject is Component component
            && AvatarContext.TryGet(component.gameObject, out var avatar, out _)
            && new FacialAnimationResolver(avatar.Root)
                .TryResolve(component, avatar.BodyPath, out var resolved))
            flattened = resolved.ToList();

        ClearSimple(data);
        data.FindPropertyRelative(nameof(FacialBlendShapeData.BaseSource)).intValue =
            (int)FacialBlendShapeData.SimpleBaseSource.Clip;
        var local = data.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations));
        if (flattened != null)
        {
            SetBlendShapeAnimations(local, flattened);
            entries.ClearArray();
            return;
        }

        var copiedSource = false;
        for (var i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            var kind = (FacialBlendShapeData.CompositeEntry.Kind)entry
                .FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.EntryKind)).intValue;
            if (kind == FacialBlendShapeData.CompositeEntry.Kind.Direct)
            {
                foreach (var animation in ReadAnimations(entry.FindPropertyRelative(
                             nameof(FacialBlendShapeData.CompositeEntry.BlendShapeAnimations))))
                    MergeAnimation(local, animation);
                continue;
            }
            if (copiedSource) continue;
            copiedSource = true;
            if (kind == FacialBlendShapeData.CompositeEntry.Kind.Clip)
            {
                data.FindPropertyRelative(nameof(FacialBlendShapeData.Clip)).objectReferenceValue =
                    entry.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.Clip)).objectReferenceValue;
                data.FindPropertyRelative(nameof(FacialBlendShapeData.ClipOption)).intValue =
                    entry.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.ClipOption)).intValue;
            }
            else
            {
                data.FindPropertyRelative(nameof(FacialBlendShapeData.BaseSource)).intValue =
                    (int)FacialBlendShapeData.SimpleBaseSource.Reference;
                data.FindPropertyRelative(nameof(FacialBlendShapeData.ReferenceSource)).objectReferenceValue =
                    entry.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.ReferenceSource)).objectReferenceValue;
            }
        }
        entries.ClearArray();
    }

    private static void ClearSimple(SerializedProperty data)
    {
        data.FindPropertyRelative(nameof(FacialBlendShapeData.Clip)).objectReferenceValue = null;
        data.FindPropertyRelative(nameof(FacialBlendShapeData.ClipOption)).intValue = (int)ClipImportOption.NonZero;
        data.FindPropertyRelative(nameof(FacialBlendShapeData.ReferenceSource)).objectReferenceValue = null;
        data.FindPropertyRelative(nameof(FacialBlendShapeData.BlendShapeAnimations)).ClearArray();
    }

    private static void MergeAnimation(
        SerializedProperty animations,
        BlendShapeWeightAnimation value,
        bool overwrite = true)
    {
        for (var i = 0; i < animations.arraySize; i++)
        {
            var element = animations.GetArrayElementAtIndex(i);
            if (element.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue != value.Name) continue;
            if (overwrite) element.CopyFrom(value);
            return;
        }
        animations.InsertArrayElementAtIndex(animations.arraySize);
        animations.GetArrayElementAtIndex(animations.arraySize - 1).CopyFrom(value);
    }

    private static IEnumerable<BlendShapeWeightAnimation> ReadAnimations(SerializedProperty animations)
    {
        for (var i = 0; i < animations.arraySize; i++)
        {
            var element = animations.GetArrayElementAtIndex(i);
            yield return new BlendShapeWeightAnimation(
                element.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName).stringValue,
                element.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName).animationCurveValue);
        }
    }

    private static SerializedProperty? FindOwningFacialData(SerializedProperty property)
    {
        var marker = "." + nameof(FacialBlendShapeData.CompositeEntries) + ".Array";
        var index = property.propertyPath.IndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? null : property.serializedObject.FindProperty(property.propertyPath[..index]);
    }

    private static int FindCompositeEntryIndex(SerializedProperty property)
    {
        var marker = nameof(FacialBlendShapeData.CompositeEntries) + ".Array.data[";
        var start = property.propertyPath.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return -1;
        start += marker.Length;
        var end = property.propertyPath.IndexOf(']', start);
        return end > start && int.TryParse(property.propertyPath[start..end], out var index) ? index : -1;
    }
}

[CustomPropertyDrawer(typeof(FacialBlendShapeData.CompositeEntry))]
internal sealed class FacialCompositeEntryDrawer : PropertyDrawer
{
    private static readonly string[] KindKeys =
    {
        "expression.facials.entryKind.direct",
        "expression.facials.entryKind.clip",
        "expression.facials.entryKind.reference"
    };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUIHelper.RegisterPropertyRegion(position, property);
        var kind = (FacialBlendShapeData.CompositeEntry.Kind)property
            .FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.EntryKind)).intValue;
        if (kind == FacialBlendShapeData.CompositeEntry.Kind.Direct)
        {
            var animations = property.FindPropertyRelative(
                nameof(FacialBlendShapeData.CompositeEntry.BlendShapeAnimations));
            position.height = GUIHelper.GetListHeight(
                animations,
                FacialDataGUI.DirectEntryAnimationsOptions);
            GUIHelper.DrawList(
                position,
                animations,
                GUIContent.none,
                FacialDataGUI.DirectEntryAnimationsOptions);
            return;
        }

        position.SetSingleHeight();
        var popup = position;
        popup.width = GUIHelper.MaxLocalizedPopupWidth(KindKeys);
        var value = new Rect(
            popup.xMax + GUIHelper.HorizontalSpacing,
            position.y,
            Mathf.Max(0f, position.xMax - popup.xMax - GUIHelper.HorizontalSpacing),
            position.height);
        DrawKindPopup(popup, property);

        if (kind == FacialBlendShapeData.CompositeEntry.Kind.Reference)
        {
            var source = property.FindPropertyRelative(
                nameof(FacialBlendShapeData.CompositeEntry.ReferenceSource));
            var current = source.objectReferenceValue as FaceTuneTagComponent;
            source.objectReferenceValue = ComponentReferenceGUI.Draw(
                value,
                GUIContent.none,
                current,
                candidate => candidate is ISettingProvider<FacialBlendShapeData>);
            return;
        }

        var clip = property.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.Clip));
        var option = property.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.ClipOption));
        var optionWidth = GUIHelper.MaxLocalizedPopupWidth(new[]
        {
            "clipImportOption.option.all",
            "clipImportOption.option.nonZero"
        });
        var (clipRect, optionRect) = value.SplitRight(optionWidth);
        EditorGUI.PropertyField(clipRect, clip, GUIContent.none);
        using (new EditorGUI.DisabledScope(clip.objectReferenceValue == null))
            GUIHelper.LocalizedEnumPopup(optionRect, option, string.Empty, new[]
            {
                "clipImportOption.option.all",
                "clipImportOption.option.nonZero"
            });
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var kind = (FacialBlendShapeData.CompositeEntry.Kind)property
            .FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.EntryKind)).intValue;
        return kind == FacialBlendShapeData.CompositeEntry.Kind.Direct
            ? GUIHelper.GetListHeight(
                property.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.BlendShapeAnimations)),
                FacialDataGUI.DirectEntryAnimationsOptions)
            : GUIHelper.LineHeight;
    }

    internal static void DrawKindPopup(Rect position, SerializedProperty property)
    {
        var kind = property.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.EntryKind));
        position.width = Mathf.Min(
            position.width,
            GUIHelper.MaxLocalizedPopupWidth(KindKeys));
        var previous = kind.intValue;
        GUIHelper.LocalizedEnumPopup(
            position,
            kind,
            string.Empty,
            KindKeys);
        if (kind.intValue != previous) ClearInactivePayload(property);
    }

    private static void ClearInactivePayload(SerializedProperty property)
    {
        property.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.BlendShapeAnimations)).ClearArray();
        property.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.Clip)).objectReferenceValue = null;
        property.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.ClipOption)).intValue = (int)ClipImportOption.NonZero;
        property.FindPropertyRelative(nameof(FacialBlendShapeData.CompositeEntry.ReferenceSource)).objectReferenceValue = null;
    }
}
[CustomPropertyDrawer(typeof(MultiFrameSettings))]
internal sealed class MultiFrameSettingsDrawer : PropertyDrawer
{
    private const float ParameterWarningHeight = 30f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUIHelper.RegisterPropertyRegion(position, property);
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
        GUIHelper.RegisterPropertyRegion(position, property);
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
