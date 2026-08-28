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
            CreateExpressionSection(),
            CreateBehaviorSection(),
            CreateConditionSection(),
            CreateDirectMenuSection(),
            CreateAdditionalSettingsSection(),
            CreatePreviewSection()
        };

    private FaceTuneSection CreateExpressionSection()
        => CreateSection("expression.section.label", new FacialDataSectionDrawer(
            serializedObject,
            nameof(ExpressionComponent.FacialBlendShapesReference),
            nameof(ExpressionComponent.FacialBlendShapes)), true);

    private FaceTuneSection CreateBehaviorSection()
        => CreateSection("expression.behavior.section.label", new ExpressionBehaviorSectionDrawer(serializedObject), true,
            spacingGroup: 1);

    private FaceTuneSection CreateConditionSection()
    {
        var enabled = serializedObject.FindProperty(nameof(ExpressionComponent.HasCondition));
        return CreateSection(
            "expression.condition.section.label",
            new ConditionSectionDrawer(serializedObject.FindProperty(nameof(ExpressionComponent.Condition))),
            enabled.boolValue,
            enabled,
            spacingGroup: 2);
    }

    private FaceTuneSection CreateDirectMenuSection()
    {
        var enabled = serializedObject.FindProperty(nameof(ExpressionComponent.DirectMenuEnabled));
        return CreateSection(
            "expression.directMenu.label",
            new DirectMenuSectionDrawer(serializedObject.FindProperty(nameof(ExpressionComponent.DirectMenuSettings))),
            false,
            enabled,
            spacingGroup: 2);
    }

    private FaceTuneSection CreateAdditionalSettingsSection()
        => CreateSection(
            "common.options.section.label",
            new ExpressionOverrideSettingsGroupDrawer(serializedObject, Inheritance),
            false,
            spacingGroup: 3);

    private FaceTuneSection CreatePreviewSection()
        => CreateSection(
            "expression.previewSettings.section.label",
            new PreviewSettingsSectionDrawer(serializedObject),
            serializedObject.FindProperty(nameof(ExpressionComponent.AlwaysOnPreviewEnabled)).boolValue,
            spacingGroup: 3);

    private ExpressionSettingsInheritance Inheritance
        => _inheritance ??= new ExpressionSettingsInheritance(Component, targets.Length == 1);
}

internal sealed class NonFacialAnimationDataSectionDrawer : ISectionDrawer, ISectionHeaderDrawer
{
    private static readonly ReorderableListOptions ListOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label);

    private readonly SerializedReferenceableSettings _source;

    public NonFacialAnimationDataSectionDrawer(
        SerializedObject serializedObject,
        string referencePropertyName,
        string directPropertyName)
    {
        _source = new SerializedReferenceableSettings(
            serializedObject,
            referencePropertyName,
            directPropertyName);
    }

    public float GetHeight()
        => SettingsReferenceGUI.GetHeight(_source, GetDirectHeight());

    public void Draw(Rect position)
        => SettingsReferenceGUI.Draw(position, _source, GetDirectHeight(), DrawDirect);

    public float GetHeaderWidth() => SettingsReferenceGUI.GetHeaderWidth();
    public void DrawHeader(Rect position) => SettingsReferenceGUI.DrawHeader(position, _source);

    private float GetDirectHeight()
    {
        var animationClips = _source.Direct.FindPropertyRelative(
            nameof(NonFacialAnimationData.AnimationClips));
        var transformAnimations = _source.Direct.FindPropertyRelative(
            nameof(NonFacialAnimationData.TransformAnimations));
        return GUIHelper.GetListHeight(animationClips, ListOptions)
             + GUIHelper.VerticalSpacing
             + GUIHelper.GetListHeight(transformAnimations, ListOptions);
    }

    private void DrawDirect(Rect position)
    {
        var animationClips = _source.Direct.FindPropertyRelative(
            nameof(NonFacialAnimationData.AnimationClips));
        var transformAnimations = _source.Direct.FindPropertyRelative(
            nameof(NonFacialAnimationData.TransformAnimations));
        position.height = GUIHelper.GetListHeight(animationClips, ListOptions);
        GUIHelper.DrawList(
            position,
            animationClips,
            "expression.additionalAnimations.clips.label".LG(),
            ListOptions);
        position.NewLine();
        position.height = GUIHelper.GetListHeight(transformAnimations, ListOptions);
        GUIHelper.DrawList(
            position,
            transformAnimations,
            "expression.additionalAnimations.transforms.label".LG(),
            ListOptions);
    }
}

internal enum ExpressionInheritedSettingKind
{
    EyeBlink,
    LipSync,
    Transition,
    Priority
}

internal sealed class ExpressionSettingsPreviewState : ScriptableObject
{
    public EyeBlinkSettings EyeBlink = new();
    public LipSyncSettings LipSync = new();
    public TransitionSettings Transition = new();
    public PrioritySettings Priority = new();
}

internal sealed class ExpressionSettingsInheritance : IDisposable
{
    private readonly ExpressionComponent _component;
    private readonly bool _singleTarget;
    private readonly ExpressionSettingsPreviewState _preview;
    private readonly SerializedObject _serializedPreview;
    private readonly SerializedProperty _eyeBlink;
    private readonly SerializedProperty _lipSync;
    private readonly SerializedProperty _transition;
    private readonly SerializedProperty _priority;
    private SettingsComponent? _eyeBlinkOwner;
    private SettingsComponent? _lipSyncOwner;
    private SettingsComponent? _transitionOwner;
    private SettingsComponent? _priorityOwner;
    private Transform? _batchOverrideTarget;
    private readonly IReadOnlyDictionary<ExpressionInheritedSettingKind, SettingBinding> _bindings;

    public ExpressionSettingsInheritance(ExpressionComponent component, bool singleTarget)
    {
        _component = component;
        _singleTarget = singleTarget;
        _preview = ScriptableObject.CreateInstance<ExpressionSettingsPreviewState>();
        _preview.hideFlags = HideFlags.HideAndDontSave;
        _serializedPreview = new SerializedObject(_preview);
        _eyeBlink = _serializedPreview.FindProperty(nameof(ExpressionSettingsPreviewState.EyeBlink));
        _lipSync = _serializedPreview.FindProperty(nameof(ExpressionSettingsPreviewState.LipSync));
        _transition = _serializedPreview.FindProperty(nameof(ExpressionSettingsPreviewState.Transition));
        _priority = _serializedPreview.FindProperty(nameof(ExpressionSettingsPreviewState.Priority));
        _bindings = new Dictionary<ExpressionInheritedSettingKind, SettingBinding>
        {
            [ExpressionInheritedSettingKind.EyeBlink] = new(
                _eyeBlink,
                () => _eyeBlinkOwner,
                nameof(SettingsComponent.HasEyeBlink),
                nameof(SettingsComponent.EyeBlink),
                nameof(SettingsComponent.EyeBlinkReference),
                target => target.CopyFrom(_preview.EyeBlink)),
            [ExpressionInheritedSettingKind.LipSync] = new(
                _lipSync,
                () => _lipSyncOwner,
                nameof(SettingsComponent.HasLipSync),
                nameof(SettingsComponent.LipSync),
                nameof(SettingsComponent.LipSyncReference),
                target => target.CopyFrom(_preview.LipSync)),
            [ExpressionInheritedSettingKind.Transition] = new(
                _transition,
                () => _transitionOwner,
                nameof(SettingsComponent.HasTransition),
                nameof(SettingsComponent.Transition),
                null,
                target => target.CopyFrom(_preview.Transition)),
            [ExpressionInheritedSettingKind.Priority] = new(
                _priority,
                () => _priorityOwner,
                nameof(SettingsComponent.HasPriority),
                nameof(SettingsComponent.Priority),
                null,
                target => target.CopyFrom(_preview.Priority))
        };
    }

    public void Refresh()
    {
        if (!_singleTarget || _component == null)
        {
            ClearOwners();
            _serializedPreview.Update();
            return;
        }

        var resolver = new FaceTuneResolver(_component.transform.root.gameObject);
        _preview.EyeBlink = resolver.EyeBlink.GetIncoming(_component, out _eyeBlinkOwner);
        _preview.LipSync = resolver.LipSync.GetIncoming(_component, out _lipSyncOwner);
        _preview.Transition = resolver.Transition.GetIncoming(_component, out _transitionOwner);
        _preview.Priority = resolver.Priority.GetIncoming(_component, out _priorityOwner);
        _batchOverrideTarget = FindBatchOverrideTarget(_component);
        _serializedPreview.Update();
    }

    public SerializedProperty GetValue(ExpressionInheritedSettingKind kind)
        => GetBinding(kind).Value;

    public void InitializeOverride(SerializedProperty target, ExpressionInheritedSettingKind kind)
        => GetBinding(kind).CopyPreview(target);

    public SettingsComponent? GetOwner(ExpressionInheritedSettingKind kind)
        => GetBinding(kind).GetOwner();

    public bool CanCreateBatchOverride => _singleTarget && _batchOverrideTarget != null;

    public void CreateBatchOverride(ExpressionInheritedSettingKind kind)
    {
        if (_batchOverrideTarget == null) return;

        var owner = _component.transform.root.gameObject
            .GetComponentsInParentExcludingSelf<SettingsComponent>(_component, true)
            .LastOrDefault();
        if (owner == null)
        {
            owner = Undo.AddComponent<SettingsComponent>(_batchOverrideTarget.gameObject);
        }
        Undo.RecordObject(owner, "expression.batchOverride.undo".LS());

        owner.enabled = true;
        var serializedOwner = new SerializedObject(owner);
        serializedOwner.Update();
        var binding = GetBinding(kind);
        InitializeOverride(serializedOwner.FindProperty(binding.ValuePropertyName), kind);
        if (binding.ReferencePropertyName != null)
        {
            var source = new SerializedReferenceableSettings(
                serializedOwner,
                binding.ReferencePropertyName,
                binding.ValuePropertyName);
            source.Mode.intValue = (int)SettingsReferenceMode.Direct;
            source.Source.objectReferenceValue = null;
        }
        serializedOwner.FindProperty(binding.EnabledPropertyName).boolValue = true;
        serializedOwner.ApplyModifiedProperties();

        EditorUtility.SetDirty(owner);
        Selection.activeObject = owner;
        EditorGUIUtility.PingObject(owner);
    }

    public void Dispose()
    {
        if (_preview != null) Object.DestroyImmediate(_preview);
    }

    private SettingBinding GetBinding(ExpressionInheritedSettingKind kind)
        => _bindings.TryGetValue(kind, out var binding)
            ? binding
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, null);

    private void ClearOwners()
    {
        (_eyeBlinkOwner, _lipSyncOwner, _transitionOwner, _priorityOwner) = (null, null, null, null);
        _batchOverrideTarget = null;
    }

    private static Transform? FindBatchOverrideTarget(ExpressionComponent expression)
        => expression.transform.parent;

    private sealed record SettingBinding(
        SerializedProperty Value,
        Func<SettingsComponent?> GetOwner,
        string EnabledPropertyName,
        string ValuePropertyName,
        string? ReferencePropertyName,
        Action<SerializedProperty> CopyPreview);
}

internal sealed class ExpressionOverrideSettingsGroupDrawer : ISectionDrawer
{
    private const float EntryGroupSpacing = 6f;
    private readonly IOptionEntry[] _entries;

    public ExpressionOverrideSettingsGroupDrawer(
        SerializedObject serializedObject,
        ExpressionSettingsInheritance inheritance)
    {
        _entries = new IOptionEntry[]
        {
            new NonFacialAnimationEntry(serializedObject),
            new SummaryEntry(
                "expression.animationSettings.section.label",
                new PropertiesSectionDrawer(new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(ExpressionComponent.MultiFrame)), null)),
                new[] { "expression.settingSource.short.standard", "expression.settingSource.short.setting" },
                () => GetMultiFrameSummary(serializedObject)),
            new Entry(
                serializedObject.FindProperty(nameof(ExpressionComponent.HasEyeBlink)),
                serializedObject.FindProperty(nameof(ExpressionComponent.EyeBlink)),
                "eyeBlink.section.label",
                ExpressionInheritedSettingKind.EyeBlink,
                inheritance),
            new Entry(
                serializedObject.FindProperty(nameof(ExpressionComponent.HasLipSync)),
                serializedObject.FindProperty(nameof(ExpressionComponent.LipSync)),
                "lipSync.section.label",
                ExpressionInheritedSettingKind.LipSync,
                inheritance),
            new Entry(
                serializedObject.FindProperty(nameof(ExpressionComponent.HasTransition)),
                serializedObject.FindProperty(nameof(ExpressionComponent.Transition)),
                "transition.section.label",
                ExpressionInheritedSettingKind.Transition,
                inheritance),
            new Entry(
                serializedObject.FindProperty(nameof(ExpressionComponent.HasPriority)),
                serializedObject.FindProperty(nameof(ExpressionComponent.Priority)),
                "priority.section.label",
                ExpressionInheritedSettingKind.Priority,
                inheritance)
        };
    }

    public float GetHeight()
    {
        var height = EntryGroupSpacing * 2f;
        foreach (var entry in _entries)
            height += GUIHelper.GetShurikenSectionHeight(entry.Foldout, entry.GetContentHeight());
        return height;
    }

    public void Draw(Rect position)
    {
        position.Indent(.5f);
        var sharedHeaderWidth = _entries.Max(entry => entry.GetHeaderWidth());
        for (var i = 0; i < _entries.Length; i++)
        {
            var entry = _entries[i];
            var contentHeight = entry.GetContentHeight();
            var section = new Rect(
                position.x,
                position.y,
                position.width,
                GUIHelper.GetShurikenSectionHeight(entry.Foldout, contentHeight));
            var drawHeader = SectionHeaderGUI.GetDrawAction(entry, entry.Foldout.Expanded);
            var headerWidth = drawHeader == null ? 0f : sharedHeaderWidth;
            if (GUIHelper.DrawShurikenSection(
                    section,
                    entry.Foldout,
                    entry.LabelKey.LG(),
                    contentHeight,
                    out var content,
                    drawHeader: drawHeader,
                    headerWidth: headerWidth))
            {
                content.height = contentHeight;
                entry.Draw(content);
            }
            position.y = section.yMax;
            if (i is 1 or 3) position.y += EntryGroupSpacing;
        }
    }

    private static GUIContent GetMultiFrameSummary(SerializedObject serializedObject)
    {
        var mode = serializedObject.FindProperty(nameof(ExpressionComponent.MultiFrame))
            .FindPropertyRelative(nameof(MultiFrameSettings.MultiFrameMode));
        if (mode.hasMultipleDifferentValues)
            return EditorGUIUtility.TrTextContent("—");
        return (mode.intValue == (int)MultiFrameSettings.Kind.Default
            ? "expression.settingSource.short.standard"
            : "expression.settingSource.short.setting").LG();
    }

    private interface IOptionEntry : ICollapsedSectionHeaderDrawer
    {
        string LabelKey { get; }
        FoldoutState Foldout { get; }
        float GetContentHeight();
        void Draw(Rect position);
    }

    private sealed class NonFacialAnimationEntry : IOptionEntry
    {
        private static readonly string[] SummaryKeys =
        {
            "expression.settingSource.short.standard",
            "expression.settingSource.short.setting",
            "expression.settingSource.short.reference"
        };
        private static readonly string[] ModeKeys =
        {
            "expression.settingSource.option.direct",
            "expression.settingSource.option.reference"
        };

        private readonly NonFacialAnimationDataSectionDrawer _drawer;
        private readonly SerializedReferenceableSettings _source;

        public string LabelKey => "expression.additionalAnimations.section.label";
        public FoldoutState Foldout { get; } = new(false);

        public NonFacialAnimationEntry(SerializedObject serializedObject)
        {
            _drawer = new NonFacialAnimationDataSectionDrawer(
                serializedObject,
                nameof(ExpressionComponent.NonFacialAnimationsReference),
                nameof(ExpressionComponent.NonFacialAnimations));
            _source = new SerializedReferenceableSettings(
                serializedObject,
                nameof(ExpressionComponent.NonFacialAnimationsReference),
                nameof(ExpressionComponent.NonFacialAnimations));
        }

        public float GetContentHeight() => _drawer.GetHeight();
        public void Draw(Rect position) => _drawer.Draw(position);
        public float GetHeaderWidth()
            => GUIHelper.CompactPopupWidth(SummaryKeys.Select(key => key.LG()));

        public void DrawHeader(Rect position)
        {
            var selected = _source.Mode.enumValueIndex;
            GUIHelper.CompactPopup(
                position,
                GetSummary(),
                ModeKeys.Select(key => key.LG()).ToArray(),
                selected,
                SetMode,
                _source.Mode.hasMultipleDifferentValues,
                separatorBefore: 1,
                centered: true);
        }

        public void DrawCollapsedHeader(Rect position)
            => GUIHelper.CompactHeaderValue(
                position,
                GetSummary(),
                _source.Mode.hasMultipleDifferentValues,
                centered: true);

        private GUIContent GetSummary()
        {
            if (_source.Mode.hasMultipleDifferentValues)
                return EditorGUIUtility.TrTextContent("—");
            if (_source.Mode.intValue == (int)SettingsReferenceMode.Reference)
                return "expression.settingSource.short.reference".LG();

            var clips = _source.Direct.FindPropertyRelative(nameof(NonFacialAnimationData.AnimationClips));
            var transforms = _source.Direct.FindPropertyRelative(nameof(NonFacialAnimationData.TransformAnimations));
            if (clips.hasMultipleDifferentValues || transforms.hasMultipleDifferentValues)
                return EditorGUIUtility.TrTextContent("—");
            return (clips.arraySize > 0 || transforms.arraySize > 0
                ? "expression.settingSource.short.setting"
                : "expression.settingSource.short.standard").LG();
        }

        private void SetMode(int mode)
        {
            _source.Mode.serializedObject.UpdateIfRequiredOrScript();
            _source.Mode.enumValueIndex = mode;
            _source.Mode.serializedObject.ApplyModifiedProperties();
        }
    }

    private sealed class SummaryEntry : IOptionEntry
    {
        private readonly ISectionDrawer _drawer;
        private readonly string[] _summaryKeys;
        private readonly Func<GUIContent> _getSummary;

        public string LabelKey { get; }
        public FoldoutState Foldout { get; } = new(false);

        public SummaryEntry(
            string labelKey,
            ISectionDrawer drawer,
            string[] summaryKeys,
            Func<GUIContent> getSummary)
            => (LabelKey, _drawer, _summaryKeys, _getSummary) =
                (labelKey, drawer, summaryKeys, getSummary);

        public float GetContentHeight() => _drawer.GetHeight();
        public void Draw(Rect position) => _drawer.Draw(position);
        public float GetHeaderWidth()
            => GUIHelper.CompactPopupWidth(_summaryKeys.Select(key => key.LG()));
        public void DrawHeader(Rect position)
            => GUIHelper.CompactHeaderValue(position, _getSummary(), centered: true);
        public void DrawCollapsedHeader(Rect position) => DrawHeader(position);
    }

    private sealed class Entry : IOptionEntry
    {
        private static readonly string[] ReferenceableShortModeKeys =
        {
            "expression.settingSource.short.standard",
            "expression.settingSource.short.batch",
            "expression.settingSource.short.setting",
            "expression.settingSource.short.reference"
        };
        private static readonly string[] DirectShortModeKeys =
        {
            "expression.settingSource.short.standard",
            "expression.settingSource.short.batch",
            "expression.settingSource.short.setting"
        };
        private static readonly string[] InheritedReferenceableModeKeys =
        {
            "expression.settingSource.option.batch",
            "expression.settingSource.option.direct",
            "expression.settingSource.option.reference"
        };
        private static readonly string[] DefaultReferenceableModeKeys =
        {
            "expression.settingSource.option.standard",
            "expression.settingSource.option.batch",
            "expression.settingSource.option.direct",
            "expression.settingSource.option.reference"
        };
        private static readonly string[] InheritedDirectModeKeys =
        {
            "expression.settingSource.option.batch",
            "expression.settingSource.option.direct"
        };
        private static readonly string[] DefaultDirectModeKeys =
        {
            "expression.settingSource.option.standard",
            "expression.settingSource.option.batch",
            "expression.settingSource.option.direct"
        };

        private readonly SerializedProperty _enabled;
        private readonly SerializedProperty _local;
        private readonly SerializedReferenceableSettings? _source;
        private readonly ExpressionInheritedSettingKind _kind;
        private readonly ExpressionSettingsInheritance _inheritance;

        public string LabelKey { get; }
        public FoldoutState Foldout { get; } = new(false);

        public Entry(
            SerializedProperty enabled,
            SerializedProperty local,
            string labelKey,
            ExpressionInheritedSettingKind kind,
            ExpressionSettingsInheritance inheritance)
        {
            (_enabled, _local, LabelKey, _kind, _inheritance) =
                (enabled, local, labelKey, kind, inheritance);
            if (IsReferenceable)
            {
                var referencePropertyName = kind switch
                {
                    ExpressionInheritedSettingKind.EyeBlink => nameof(ExpressionComponent.EyeBlinkReference),
                    ExpressionInheritedSettingKind.LipSync => nameof(ExpressionComponent.LipSyncReference),
                    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
                };
                _source = new SerializedReferenceableSettings(
                    local.serializedObject,
                    referencePropertyName,
                    local.name);
            }
        }

        private bool IsReferenceable
            => _kind is ExpressionInheritedSettingKind.EyeBlink or ExpressionInheritedSettingKind.LipSync;

        private bool ShowsLocalValue => _enabled.boolValue && !_enabled.hasMultipleDifferentValues;

        private SerializedProperty? ReferenceMode => _source?.Mode;

        public float GetContentHeight()
        {
            var height = GetValueHeight();
            if (ShowsInheritedValue)
                height += GUIHelper.LineHeight + GUIHelper.VerticalSpacing;
            return height;
        }

        public void Draw(Rect position)
        {
            if (ShowsInheritedValue)
            {
                DrawSource(position.SetSingleHeight(), _inheritance.GetOwner(_kind));
                position.NewLine();
            }

            var value = GetDisplayedValue();
            using var disabled = new EditorGUI.DisabledScope(!ShowsLocalValue);
            if (!ShowsLocalValue || !IsReferenceable)
            {
                position.height = EditorGUI.GetPropertyHeight(value, GUIContent.none, true);
                EditorGUI.PropertyField(position, value, GUIContent.none, true);
                return;
            }

            SettingsReferenceGUI.Draw(
                position,
                _source!,
                EditorGUI.GetPropertyHeight(_local, GUIContent.none, true),
                rect => EditorGUI.PropertyField(rect, _local, GUIContent.none, true));
        }

        private float GetValueHeight()
        {
            var value = GetDisplayedValue();
            if (!ShowsLocalValue || !IsReferenceable)
                return EditorGUI.GetPropertyHeight(value, GUIContent.none, true);
            return SettingsReferenceGUI.GetHeight(
                _source!,
                EditorGUI.GetPropertyHeight(_local, GUIContent.none, true));
        }

        private bool ShowsInheritedValue
            => !ShowsLocalValue && _inheritance.GetOwner(_kind) != null;

        private static void DrawSource(Rect position, SettingsComponent? source)
        {
            using var disabled = new EditorGUI.DisabledScope(true);
            EditorGUI.ObjectField(
                position,
                "expression.settingSource.label".LG(),
                source,
                typeof(SettingsComponent),
                true);
        }

        public float GetHeaderWidth()
            => GUIHelper.CompactPopupWidth(
                (IsReferenceable ? ReferenceableShortModeKeys : DirectShortModeKeys)
                .Select(key => key.LG()));

        public void DrawHeader(Rect position)
        {
            var keys = GetModeKeys();
            var hasOwner = _inheritance.GetOwner(_kind) != null;
            var localOffset = hasOwner ? 1 : 2;
            var selected = !ShowsLocalValue ? 0 : localOffset + (ReferenceMode?.enumValueIndex ?? 0);
            GUIHelper.CompactPopup(
                position,
                GetCurrentModeLabel(hasOwner),
                keys.Select(key => key.LG()).ToArray(),
                selected,
                index => SetMode(index, hasOwner),
                _enabled.hasMultipleDifferentValues,
                separatorBefore: IsReferenceable ? keys.Length - 1 : -1,
                centered: true);
        }

        public void DrawCollapsedHeader(Rect position)
        {
            var hasOwner = _inheritance.GetOwner(_kind) != null;
            GUIHelper.CompactHeaderValue(
                position,
                GetCurrentModeLabel(hasOwner),
                _enabled.hasMultipleDifferentValues,
                centered: true);
        }

        private GUIContent GetCurrentModeLabel(bool hasOwner)
        {
            if (_enabled.hasMultipleDifferentValues)
                return EditorGUIUtility.TrTextContent("—");
            var key = !ShowsLocalValue
                ? hasOwner
                    ? "expression.settingSource.short.batch"
                    : "expression.settingSource.short.standard"
                : ReferenceMode?.intValue == (int)SettingsReferenceMode.Reference
                    ? "expression.settingSource.short.reference"
                    : "expression.settingSource.short.setting";
            return key.LG();
        }

        private string[] GetModeKeys()
            => (_inheritance.GetOwner(_kind) != null, IsReferenceable) switch
            {
                (true, true) => InheritedReferenceableModeKeys,
                (false, true) => DefaultReferenceableModeKeys,
                (true, false) => InheritedDirectModeKeys,
                (false, false) => DefaultDirectModeKeys
            };

        private void SetMode(int mode, bool hasOwner)
        {
            if (mode == 0)
            {
                SetLocalOverride(false);
                return;
            }
            if (!hasOwner && mode == 1)
            {
                if (_inheritance.CanCreateBatchOverride)
                {
                    _inheritance.CreateBatchOverride(_kind);
                    SetLocalOverride(false);
                }
                return;
            }

            if (!ShowsLocalValue) SetLocalOverride(true);
            if (ReferenceMode != null)
            {
                _enabled.serializedObject.UpdateIfRequiredOrScript();
                ReferenceMode.enumValueIndex = mode - (hasOwner ? 1 : 2);
                _enabled.serializedObject.ApplyModifiedProperties();
            }
        }

        private void SetLocalOverride(bool enabled)
        {
            _enabled.serializedObject.UpdateIfRequiredOrScript();
            if (enabled)
            {
                _inheritance.InitializeOverride(_local, _kind);
                if (_source != null)
                    _source.Reference.CopyFrom(new SettingsReference());
            }
            _enabled.boolValue = enabled;
            _enabled.serializedObject.ApplyModifiedProperties();
        }

        private SerializedProperty GetDisplayedValue()
            => _enabled.boolValue && !_enabled.hasMultipleDifferentValues
                ? _local
                : _inheritance.GetValue(_kind);
    }
}

internal sealed class ExpressionBehaviorSectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _eyeBlink;
    private readonly SerializedProperty _lipSync;
    private readonly SerializedProperty _writeMode;

    public ExpressionBehaviorSectionDrawer(SerializedObject serializedObject)
    {
        _eyeBlink = serializedObject.FindProperty(nameof(ExpressionComponent.AllowEyeBlink));
        _lipSync = serializedObject.FindProperty(nameof(ExpressionComponent.AllowLipSync));
        _writeMode = serializedObject.FindProperty(nameof(ExpressionComponent.WriteMode));
    }

    public float GetHeight() => GUIHelper.GetLinesHeight(3);

    public void Draw(Rect position)
    {
        GUIHelper.LocalizedEnumPopup(position, _writeMode, "expression.application.label", new[] { "expression.application.replace.label", "expression.application.blend.label" });
        position.NewLine();
        GUIHelper.DrawLocalizedEnum(ref position, _eyeBlink, "facialSettings.allowEyeBlink.label", nameof(TrackingPermission));
        GUIHelper.DrawLocalizedEnum(ref position, _lipSync, "facialSettings.allowLipSync.label", nameof(TrackingPermission));
    }
}

internal sealed class ConditionSectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _condition;
    public ConditionSectionDrawer(SerializedProperty condition) => _condition = condition;
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
    public DirectMenuSectionDrawer(SerializedProperty settings) => _settings = settings;
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
    public PreviewSettingsSectionDrawer(SerializedObject serializedObject) => _enabled = serializedObject.FindProperty(nameof(ExpressionComponent.AlwaysOnPreviewEnabled));
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
