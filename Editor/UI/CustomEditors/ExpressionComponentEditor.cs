using Aoyon.FaceTune.Settings;

namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(ExpressionComponent))]
internal sealed class ExpressionComponentEditor : FaceTuneSectionEditorBase<ExpressionComponent>
{
    private ExpressionSettingsInheritance? _inheritance;

    protected override bool ShowLanguageSwitcher => true;

    protected override void PrepareInspector()
        => Inheritance.Refresh();

    protected override void OnDisable()
    {
        _inheritance?.Dispose();
        _inheritance = null;
    }

    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[]
        {
            CreateDefinitionSection(),
            CreateConditionSection(),
            CreateDirectMenuSection(),
            CreateAdditionalSettingsSection(),
            CreatePreviewSection()
        };

    private FaceTuneSection CreateDefinitionSection()
        => CreateSection(
            "expression.content.section.label",
            new ExpressionDefinitionSectionDrawer(serializedObject, Inheritance),
            defaultExpanded: true,
            spacingGroup: 0);

    private FaceTuneSection CreateConditionSection()
    {
        var enabled = serializedObject.FindProperty(nameof(ExpressionComponent.HasCondition));
        return CreateSection(
            "expression.condition.section.label",
            new ConditionSectionDrawer(serializedObject.FindProperty(nameof(ExpressionComponent.Condition))),
            enabled.boolValue,
            enabled,
            spacingGroup: 1);
    }

    private FaceTuneSection CreateDirectMenuSection()
    {
        var enabled = serializedObject.FindProperty(nameof(ExpressionComponent.DirectMenuEnabled));
        return CreateSection(
            "expression.directMenu.label",
            new DirectMenuSectionDrawer(serializedObject.FindProperty(nameof(ExpressionComponent.DirectMenuSettings))),
            false,
            enabled,
            spacingGroup: 1);
    }

    private FaceTuneSection CreateAdditionalSettingsSection()
        => CreateSection(
            "common.options.section.label",
            new ExpressionScopedSettingsGroupDrawer(serializedObject, Inheritance),
            false,
            spacingGroup: 2);

    private ExpressionSettingsInheritance Inheritance
        => _inheritance ??= new ExpressionSettingsInheritance(Component, targets.Length == 1);

    private FaceTuneSection CreatePreviewSection()
        => CreateSection(
            "expression.previewSettings.section.label",
            new PreviewSettingsSectionDrawer(serializedObject),
            serializedObject.FindProperty(nameof(ExpressionComponent.AlwaysOnPreviewEnabled)).boolValue,
            spacingGroup: 2);
}

internal sealed class ExpressionDefinitionSectionDrawer : ISectionDrawer, ICollapsedSectionHeaderDrawer, ISectionHeaderMenuDrawer
{
    private readonly SerializedObject _serializedObject;
    private readonly SerializedProperty _reference;
    private readonly SerializedProperty _mode;
    private readonly SerializedProperty _source;
    private readonly SerializedObject _preview;
    private readonly DefinitionChild[] _children;

    private static readonly string[] CopiedProperties =
    {
        nameof(ExpressionComponent.FacialBlendShapes),
        nameof(ExpressionComponent.NonFacialAnimations),
        nameof(ExpressionComponent.WriteMode),
        nameof(ExpressionComponent.MultiFrame),
        nameof(ExpressionComponent.AllowEyeBlink),
        nameof(ExpressionComponent.AllowLipSync),
        nameof(ExpressionComponent.HasEyeBlink),
        nameof(ExpressionComponent.EyeBlinkReference),
        nameof(ExpressionComponent.EyeBlink),
        nameof(ExpressionComponent.HasLipSync),
        nameof(ExpressionComponent.LipSyncReference),
        nameof(ExpressionComponent.LipSync)
    };

    public ExpressionDefinitionSectionDrawer(
        SerializedObject serializedObject,
        ExpressionSettingsInheritance inheritance)
    {
        _serializedObject = serializedObject;
        _reference = serializedObject.FindProperty(nameof(ExpressionComponent.ExpressionDataReference));
        _mode = _reference.FindPropertyRelative(nameof(SettingsReference.Mode));
        _source = _reference.FindPropertyRelative(nameof(SettingsReference.Source));

        _preview = inheritance.DefinitionPreview;
        var preview = _preview;
        _children = new[]
        {
            new DefinitionChild(
                "expression.section.label",
                new FacialDataSectionDrawer(serializedObject, nameof(ExpressionComponent.FacialBlendShapes)),
                new FacialDataSectionDrawer(preview, nameof(ExpressionDefinitionPreviewState.FacialBlendShapes)),
                true,
                false),
            new DefinitionChild(
                "expression.behavior.section.label",
                new ExpressionBehaviorSectionDrawer(serializedObject),
                new ExpressionBehaviorSectionDrawer(preview),
                true,
                false),
            new DefinitionChild(
                "common.options.section.label",
                CreateOptionsDrawer(serializedObject, inheritance, readOnly: false),
                CreateOptionsDrawer(preview, inheritance, readOnly: true),
                false,
                true)
        };

        Actions = new SectionActionSet(
            serializedObject,
            new[] { SectionActionField.From(_reference, () => new SettingsReference()) }
                .Concat(_children.SelectMany(child => child.Direct.Actions.Fields)));
    }

    private static ISectionDrawer CreateOptionsDrawer(
        SerializedObject serializedObject,
        ExpressionSettingsInheritance inheritance,
        bool readOnly)
    {
        return new NestedSectionGroupDrawer(
            serializedObject,
            new[]
            {
                new NestedSection(
                    "expression.multiFrame.section.label",
                    new MultiFrameDefinitionSectionDrawer(
                        serializedObject.FindProperty(nameof(ExpressionComponent.MultiFrame)))),
                new NestedSection(
                    "eyeBlink.section.label",
                    new ExpressionScopedSettingSectionDrawer(
                        serializedObject,
                        nameof(ExpressionComponent.HasEyeBlink),
                        nameof(ExpressionComponent.EyeBlink),
                        nameof(ExpressionComponent.EyeBlinkReference),
                        ExpressionInheritedSettingKind.EyeBlink,
                        inheritance,
                        () => new EyeBlinkSettings())),
                new NestedSection(
                    "lipSync.section.label",
                    new ExpressionScopedSettingSectionDrawer(
                        serializedObject,
                        nameof(ExpressionComponent.HasLipSync),
                        nameof(ExpressionComponent.LipSync),
                        nameof(ExpressionComponent.LipSyncReference),
                        ExpressionInheritedSettingKind.LipSync,
                        inheritance,
                        () => new LipSyncSettings())),
                new NestedSection(
                    "expression.additionalAnimations.section.label",
                    new NonFacialAnimationDataSectionDrawer(
                        serializedObject,
                        nameof(ExpressionComponent.NonFacialAnimations)),
                    ShowHeader: false)
            },
            ChildHeaderWidth,
            readOnly);
    }

    internal static float ChildHeaderWidth
        => GUIHelper.CompactPopupWidth(new[]
        {
            "expression.settingSource.short.standard".LG(),
            "expression.settingSource.short.batch".LG(),
            "expression.settingSource.short.setting".LG(),
            "expression.settingSource.short.reference".LG()
        });

    public SectionActionSet Actions { get; }

    public float GetHeight()
    {
        var height = 0f;
        if (_mode.intValue == (int)SettingsReferenceMode.Reference)
            height += GUIHelper.LineHeight + GUIHelper.VerticalSpacing;
        if (_children.Length == 0)
            return height;

        for (var i = 0; i < _children.Length; i++)
        {
            height += GUIHelper.GetShurikenSectionHeight(
                _children[i].Foldout,
                _children[i].GetDrawer(IsReference).GetHeight());
            if (i + 1 < _children.Length)
                height += GUIHelper.VerticalSpacing;
        }
        return height;
    }

    public float GetHeaderWidth()
        => SettingsReferenceGUI.GetHeaderWidth();

    public void DrawHeader(Rect position)
    {
        var selected = _mode.enumValueIndex;
        GUIHelper.CompactPopup(
            position,
            _mode.hasMultipleDifferentValues
                ? EditorGUIUtility.TrTextContent("—")
                : (selected == 0
                    ? "settingsReferenceMode.short.direct"
                    : "settingsReferenceMode.short.reference").LG(),
            new[]
            {
                "settingsReferenceMode.option.direct".LG(),
                "settingsReferenceMode.option.reference".LG()
            },
            selected,
            SetMode,
            _mode.hasMultipleDifferentValues,
            centered: true);
    }

    public void DrawCollapsedHeader(Rect position) => DrawHeader(position);

    private void SetMode(int mode)
    {
        _serializedObject.UpdateIfRequiredOrScript();
        if (mode == (int)SettingsReferenceMode.Direct)
        {
            if (_mode.intValue == (int)SettingsReferenceMode.Reference
                && _serializedObject.targetObjects.Length == 1)
            {
                _preview.UpdateIfRequiredOrScript();
                foreach (var propertyName in CopiedProperties)
                {
                    var source = _preview.FindProperty(propertyName);
                    if (source != null)
                        _serializedObject.CopyFromSerializedProperty(source);
                }
            }
            _source.objectReferenceValue = null;
        }
        _mode.enumValueIndex = mode;
        _serializedObject.ApplyModifiedProperties();
    }

    public void Draw(Rect position)
    {
        var isReference = IsReference;
        if (isReference)
        {
            position.height = GUIHelper.LineHeight;
            EditorGUI.PropertyField(position, _source, "common.component.label".LG());
            position.NewLine();
        }

        if (_children.Length == 0)
            return;

        position.Indent(GUIHelper.NestedSectionIndent);
        position.width += GUIHelper.ContentPadding;
        var sharedHeaderWidth = ChildHeaderWidth;
        for (var i = 0; i < _children.Length; i++)
        {
            var child = _children[i];
            var drawer = child.GetDrawer(isReference);
            var contentHeight = drawer.GetHeight();
            var section = new Rect(
                position.x,
                position.y,
                position.width,
                GUIHelper.GetShurikenSectionHeight(child.Foldout, contentHeight));
            var headerDrawer = child.ShowHeader ? drawer as ISectionHeaderDrawer : null;
            var drawHeader = isReference && headerDrawer is ICollapsedSectionHeaderDrawer collapsed
                ? collapsed.DrawCollapsedHeader
                : SectionHeaderGUI.GetDrawAction(headerDrawer, child.Foldout.Expanded);
            var headerWidth = drawHeader == null ? 0f : sharedHeaderWidth;
            Func<GenericMenu>? createMenu = isReference
                ? null
                : () => SectionHeaderMenu.Create(drawer.Actions);
            var drawn = GUIHelper.DrawShurikenSection(
                section,
                child.Foldout,
                child.LabelKey.LG(),
                contentHeight,
                out var content,
                createMenu,
                drawHeader,
                headerWidth,
                drawer.Actions.ScopeProperty);
            if (drawn)
            {
                content.height = GUIHelper.LineHeight;
                var disabledValue = isReference && drawer is not NestedSectionGroupDrawer;
                using var disabled = new EditorGUI.DisabledScope(disabledValue);
                drawer.Draw(content);
            }
            position.y = section.yMax;
            if (i + 1 < _children.Length)
                position.y += GUIHelper.VerticalSpacing;
        }
    }

    private bool IsReference
        => _mode.intValue == (int)SettingsReferenceMode.Reference;

    public void PopulateHeaderMenu(GenericMenu menu)
    {
        var canSeparate = _serializedObject.targetObjects.Length == 1
                          && _mode.intValue == (int)SettingsReferenceMode.Direct
                          && _serializedObject.targetObject is ExpressionComponent expression
                          && !EditorUtility.IsPersistent(expression.gameObject);
        if (canSeparate)
            menu.AddItem("expression.separate.menu".LG(), false, Separate);
        else
            menu.AddDisabledItem("expression.separate.menu".LG());
    }

    private void Separate()
    {
        if (_serializedObject.targetObject is not ExpressionComponent expression)
            return;

        ExpressionDataComponent? data = null;
        SectionOperations.RunUndo("expression.separate.menu".LS(), () =>
        {
            data = FaceTuneRecipes.AddExpressionData(expression.transform.parent);
            using var dataObject = new SerializedObject(data);
            _serializedObject.UpdateIfRequiredOrScript();
            dataObject.UpdateIfRequiredOrScript();
            foreach (var propertyName in CopiedProperties)
            {
                var source = _serializedObject.FindProperty(propertyName);
                var target = dataObject.FindProperty(propertyName);
                if (source != null && target != null)
                    dataObject.CopyFromSerializedProperty(source);
            }
            dataObject.FindProperty(nameof(ExpressionDataComponent.HasFacialBlendShapes)).boolValue = true;
            dataObject.FindProperty(nameof(ExpressionDataComponent.HasNonFacialAnimations)).boolValue = true;
            dataObject.FindProperty(nameof(ExpressionDataComponent.HasFacialBehavior)).boolValue = true;
            dataObject.FindProperty(nameof(ExpressionDataComponent.HasMultiFrame)).boolValue = true;
            dataObject.ApplyModifiedProperties();

            var reference = _serializedObject.FindProperty(nameof(ExpressionComponent.ExpressionDataReference));
            reference.FindPropertyRelative(nameof(SettingsReference.Mode)).intValue =
                (int)SettingsReferenceMode.Reference;
            reference.FindPropertyRelative(nameof(SettingsReference.Source)).objectReferenceValue = data.transform;
            _serializedObject.ApplyModifiedProperties();
        });

        if (data != null)
            EditorGUIUtility.PingObject(data);
    }

    private sealed record DefinitionChild(
        string LabelKey,
        ISectionDrawer Direct,
        ISectionDrawer Preview,
        bool Expanded,
        bool ShowHeader)
    {
        public FoldoutState Foldout { get; } = new(Expanded);
        public ISectionDrawer GetDrawer(bool preview) => preview ? Preview : Direct;
    }
}

internal sealed class NonFacialAnimationDataSectionDrawer : ISectionDrawer
{
    private static readonly ReorderableListOptions ReferenceAnimationsOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        InitializeElement: property => property.objectReferenceValue = null,
        ElementHeight: GUIHelper.LineHeight,
        SingleLineWhenEmpty: true);
    private static readonly ReorderableListOptions AnimationClipsOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        InitializeElement: property => property.objectReferenceValue = null,
        ElementHeight: GUIHelper.LineHeight,
        SingleLineWhenEmpty: true);
    private static readonly ReorderableListOptions TransformAnimationsOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        InitializeElement: property => property.CopyFrom(new TransformAnimation()),
        SingleLineWhenEmpty: true);

    private readonly SerializedProperty _data;

    public NonFacialAnimationDataSectionDrawer(
        SerializedObject serializedObject,
        string directPropertyName)
    {
        _data = serializedObject.FindProperty(directPropertyName);
        Actions = new SectionActionSet(
            serializedObject,
            new[] { SectionActionField.From(_data, () => new NonFacialAnimationData()) });
    }

    public SectionActionSet Actions { get; }

    public float GetHeight()
    {
        var references = _data.FindPropertyRelative(nameof(NonFacialAnimationData.ReferenceAnimations));
        var clips = _data.FindPropertyRelative(nameof(NonFacialAnimationData.AnimationClips));
        var transforms = _data.FindPropertyRelative(nameof(NonFacialAnimationData.TransformAnimations));
        return GUIHelper.GetListHeight(references, ReferenceAnimationsOptions)
             + GUIHelper.VerticalSpacing
             + GUIHelper.GetListHeight(clips, AnimationClipsOptions)
             + GUIHelper.VerticalSpacing
             + GUIHelper.GetListHeight(transforms, TransformAnimationsOptions);
    }

    public void Draw(Rect position)
    {
        var references = _data.FindPropertyRelative(nameof(NonFacialAnimationData.ReferenceAnimations));
        var clips = _data.FindPropertyRelative(nameof(NonFacialAnimationData.AnimationClips));
        var transforms = _data.FindPropertyRelative(nameof(NonFacialAnimationData.TransformAnimations));

        position.height = GUIHelper.GetListHeight(references, ReferenceAnimationsOptions);
        GUIHelper.DrawList(
            position,
            references,
            "expression.additionalAnimations.references.label".LG(),
            ReferenceAnimationsOptions);
        position.NewLine();
        position.height = GUIHelper.GetListHeight(clips, AnimationClipsOptions);
        GUIHelper.DrawList(
            position,
            clips,
            "expression.additionalAnimations.clips.label".LG(),
            AnimationClipsOptions);
        position.NewLine();
        position.height = GUIHelper.GetListHeight(transforms, TransformAnimationsOptions);
        GUIHelper.DrawList(
            position,
            transforms,
            "expression.additionalAnimations.transforms.label".LG(),
            TransformAnimationsOptions);
    }
}

internal sealed class MultiFrameDefinitionSectionDrawer : ISectionDrawer, ICollapsedSectionHeaderDrawer
{
    private readonly SerializedProperty _multiFrame;

    public MultiFrameDefinitionSectionDrawer(SerializedProperty multiFrame)
    {
        _multiFrame = multiFrame;
        Actions = new SectionActionSet(
            multiFrame.serializedObject,
            new[] { SectionActionField.From(multiFrame, () => new MultiFrameSettings()) });
    }

    public SectionActionSet Actions { get; }
    public float GetHeight() => EditorGUI.GetPropertyHeight(_multiFrame, GUIContent.none, true);

    public void Draw(Rect position)
    {
        position.height = GetHeight();
        EditorGUI.PropertyField(position, _multiFrame, true);
    }

    public float GetHeaderWidth()
        => GUIHelper.CompactPopupWidth(new[]
        {
            "expression.settingSource.short.standard".LG(),
            "expression.settingSource.short.setting".LG()
        });

    public void DrawHeader(Rect position)
    {
        var mode = _multiFrame.FindPropertyRelative(nameof(MultiFrameSettings.MultiFrameMode));
        var label = mode.hasMultipleDifferentValues
            ? EditorGUIUtility.TrTextContent("—")
            : (mode.intValue == (int)MultiFrameSettings.Kind.Default
                ? "expression.settingSource.short.standard"
                : "expression.settingSource.short.setting").LG();
        GUIHelper.CompactHeaderValue(position, label, mode.hasMultipleDifferentValues, centered: true);
    }

    public void DrawCollapsedHeader(Rect position) => DrawHeader(position);
}

internal sealed class ExpressionBehaviorSectionDrawer : ISectionDrawer, ICollapsedSectionHeaderDrawer
{
    private readonly SerializedProperty _eyeBlink;
    private readonly SerializedProperty _lipSync;
    private readonly SerializedProperty _writeMode;

    public ExpressionBehaviorSectionDrawer(SerializedObject serializedObject)
    {
        _eyeBlink = serializedObject.FindProperty(nameof(ExpressionComponent.AllowEyeBlink));
        _lipSync = serializedObject.FindProperty(nameof(ExpressionComponent.AllowLipSync));
        _writeMode = serializedObject.FindProperty(nameof(ExpressionComponent.WriteMode));
        Actions = new SectionActionSet(
            serializedObject,
            new[]
            {
                SectionActionField.From(_eyeBlink, () => ExpressionComponent.DefaultAllowEyeBlink),
                SectionActionField.From(_lipSync, () => ExpressionComponent.DefaultAllowLipSync),
                SectionActionField.From(_writeMode, () => ExpressionComponent.DefaultWriteMode)
            });
    }

    public SectionActionSet Actions { get; }

    public float GetHeight() => GUIHelper.GetLinesHeight(3);

    public float GetHeaderWidth()
        => GUIHelper.CompactPopupWidth(new[]
        {
            "expression.settingSource.short.standard".LG(),
            "expression.settingSource.short.setting".LG()
        });

    public void DrawHeader(Rect position) => DrawCollapsedHeader(position);

    public void DrawCollapsedHeader(Rect position)
    {
        var hasMixedValues = _writeMode.hasMultipleDifferentValues
                             || _eyeBlink.hasMultipleDifferentValues
                             || _lipSync.hasMultipleDifferentValues;
        var isDefault = _writeMode.intValue == (int)ExpressionComponent.DefaultWriteMode
                        && _eyeBlink.intValue == (int)ExpressionComponent.DefaultAllowEyeBlink
                        && _lipSync.intValue == (int)ExpressionComponent.DefaultAllowLipSync;
        var label = hasMixedValues
            ? EditorGUIUtility.TrTextContent("—")
            : (isDefault
                ? "expression.settingSource.short.standard"
                : "expression.settingSource.short.setting").LG();
        GUIHelper.CompactHeaderValue(position, label, hasMixedValues, centered: true);
    }

    public void Draw(Rect position)
        => ExpressionBehaviorGUI.Draw(position, _writeMode, _eyeBlink, _lipSync);
}

internal static class ExpressionBehaviorGUI
{
    private static readonly string[] WriteModeKeys =
    {
        "expression.application.replace.label",
        "expression.application.blend.label"
    };

    public static void Draw(
        Rect position,
        SerializedProperty writeMode,
        SerializedProperty eyeBlink,
        SerializedProperty lipSync)
    {
        position.height = GUIHelper.LineHeight;
        GUIHelper.LocalizedEnumPopup(
            position,
            writeMode,
            "expression.application.label",
            WriteModeKeys);
        position.NewLine();
        GUIHelper.DrawLocalizedEnum(
            ref position,
            eyeBlink,
            "facialSettings.allowEyeBlink.label",
            nameof(TrackingPermission));
        GUIHelper.DrawLocalizedEnum(
            ref position,
            lipSync,
            "facialSettings.allowLipSync.label",
            nameof(TrackingPermission));
    }
}

internal sealed class ConditionSectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _condition;
    public ConditionSectionDrawer(SerializedProperty condition)
    {
        _condition = condition;
        Actions = new SectionActionSet(
            condition.serializedObject,
            new[] { SectionActionField.From(
                condition,
                () => ExpressionComponent.CreateDefaultCondition()) });
    }

    public SectionActionSet Actions { get; }
    public float GetHeight() => EditorGUI.GetPropertyHeight(_condition, GUIContent.none, true);
    public void Draw(Rect position)
    {
        position.height = GetHeight();
        EditorGUI.PropertyField(position, _condition, GUIContent.none, true);
    }
}

internal sealed class DirectMenuSectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _settings;
    public DirectMenuSectionDrawer(SerializedProperty settings)
    {
        _settings = settings;
        Actions = new SectionActionSet(
            settings.serializedObject,
            new[] { SectionActionField.From(
                settings,
                () => ExpressionComponent.CreateDefaultDirectMenuSettings()) });
    }

    public SectionActionSet Actions { get; }
    public float GetHeight() => EditorGUI.GetPropertyHeight(_settings, GUIContent.none, true);
    public void Draw(Rect position)
    {
        position.height = GetHeight();
        EditorGUI.PropertyField(position, _settings, GUIContent.none, true);
    }
}

internal sealed class PreviewSettingsSectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _enabled;
    public PreviewSettingsSectionDrawer(SerializedObject serializedObject)
    {
        _enabled = serializedObject.FindProperty(nameof(ExpressionComponent.AlwaysOnPreviewEnabled));
        Actions = new SectionActionSet(
            serializedObject,
            new[] { SectionActionField.From(
                _enabled,
                () => ExpressionComponent.DefaultAlwaysOnPreviewEnabled) });
    }

    public SectionActionSet Actions { get; }
    public float GetHeight() => GUIHelper.GetLinesHeight(3);
    public void Draw(Rect position)
    {
        GUIHelper.LocalizedPropertyField(position, _enabled, "expression.realTimePreview.label");
        position.NewLine();
        ProjectSettings.EnableHierarchySelectedExpressionPreview = GUIHelper.DrawToggleLeft(position, ProjectSettings.EnableHierarchySelectedExpressionPreview, "expression.selectedExpressionPreview.label".LG());
        position.NewLine();
        ProjectSettings.EnableProjectSelectedExpressionPreview = GUIHelper.DrawToggleLeft(position, ProjectSettings.EnableProjectSelectedExpressionPreview, "expression.selectedProjectExpressionPreview.label".LG());
    }
}
