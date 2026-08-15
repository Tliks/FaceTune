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
            CreateAnimationSection(),
            CreateConditionSection(),
            CreateDirectMenuSection(),
            CreatePreviewSection(),
            CreateAdditionalSettingsSection()
        };

    private FaceTuneSection CreateExpressionSection()
        => CreateSection("expression.section.label", new FacialDataSectionDrawer(serializedObject, Component, targets.Length, nameof(ExpressionComponent.FacialBlendShapes)), true);

    private FaceTuneSection CreateBehaviorSection()
        => CreateSection("expression.behavior.section.label", new ExpressionBehaviorSectionDrawer(serializedObject), true);

    private FaceTuneSection CreateAnimationSection()
        => CreateSection("expression.animationSettings.section.label", new PropertiesSectionDrawer(
            new PropertiesSectionDrawer.Entry(serializedObject.FindProperty(nameof(ExpressionComponent.MultiFrame)), null)), false);

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
            new ExpressionOverrideSettingsGroupDrawer(serializedObject, Inheritance),
            false,
            spacingGroup: 2);

    private FaceTuneSection CreatePreviewSection()
        => CreateSection("expression.previewSettings.section.label", new PreviewSettingsSectionDrawer(serializedObject), false, spacingGroup: 2);

    private ExpressionSettingsInheritance Inheritance
        => _inheritance ??= new ExpressionSettingsInheritance(Component, targets.Length == 1);
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

    public SerializedProperty GetValue(ExpressionInheritedSettingKind kind) => kind switch
    {
        ExpressionInheritedSettingKind.EyeBlink => _eyeBlink,
        ExpressionInheritedSettingKind.LipSync => _lipSync,
        ExpressionInheritedSettingKind.Transition => _transition,
        ExpressionInheritedSettingKind.Priority => _priority,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public void InitializeOverride(SerializedProperty target, ExpressionInheritedSettingKind kind)
    {
        switch (kind)
        {
            case ExpressionInheritedSettingKind.EyeBlink:
                target.CopyFrom(new EyeBlinkSettingsSource());
                target.FindPropertyRelative(nameof(EyeBlinkSettingsSource.Direct)).CopyFrom(_preview.EyeBlink);
                break;
            case ExpressionInheritedSettingKind.LipSync:
                target.CopyFrom(new LipSyncSettingsSource());
                target.FindPropertyRelative(nameof(LipSyncSettingsSource.Direct)).CopyFrom(_preview.LipSync);
                break;
            case ExpressionInheritedSettingKind.Transition:
                target.CopyFrom(_preview.Transition);
                break;
            case ExpressionInheritedSettingKind.Priority:
                target.CopyFrom(_preview.Priority);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    public SettingsComponent? GetOwner(ExpressionInheritedSettingKind kind) => kind switch
    {
        ExpressionInheritedSettingKind.EyeBlink => _eyeBlinkOwner,
        ExpressionInheritedSettingKind.LipSync => _lipSyncOwner,
        ExpressionInheritedSettingKind.Transition => _transitionOwner,
        ExpressionInheritedSettingKind.Priority => _priorityOwner,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public bool CanCreateBatchOverride => _singleTarget && _batchOverrideTarget != null;

    public void CreateBatchOverride(ExpressionInheritedSettingKind kind)
    {
        if (_batchOverrideTarget == null) return;
        var owner = _batchOverrideTarget.GetComponent<SettingsComponent>();
        if (owner == null)
            owner = Undo.AddComponent<SettingsComponent>(_batchOverrideTarget.gameObject);
        Undo.RecordObject(owner, "expression.batchOverride.undo".LS());

        owner.enabled = true;
        var serializedOwner = new SerializedObject(owner);
        serializedOwner.Update();
        var (enabledPropertyName, valuePropertyName) = kind switch
        {
            ExpressionInheritedSettingKind.EyeBlink => (nameof(SettingsComponent.HasEyeBlink), nameof(SettingsComponent.EyeBlink)),
            ExpressionInheritedSettingKind.LipSync => (nameof(SettingsComponent.HasLipSync), nameof(SettingsComponent.LipSync)),
            ExpressionInheritedSettingKind.Transition => (nameof(SettingsComponent.HasTransition), nameof(SettingsComponent.Transition)),
            ExpressionInheritedSettingKind.Priority => (nameof(SettingsComponent.HasPriority), nameof(SettingsComponent.Priority)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
        InitializeOverride(serializedOwner.FindProperty(valuePropertyName), kind);
        serializedOwner.FindProperty(enabledPropertyName).boolValue = true;
        serializedOwner.ApplyModifiedProperties();

        EditorUtility.SetDirty(owner);
        Selection.activeObject = owner;
        EditorGUIUtility.PingObject(owner);
    }

    public void Dispose()
    {
        if (_preview != null) Object.DestroyImmediate(_preview);
    }

    private void ClearOwners()
    {
        (_eyeBlinkOwner, _lipSyncOwner, _transitionOwner, _priorityOwner) = (null, null, null, null);
        _batchOverrideTarget = null;
    }

    private static Transform? FindBatchOverrideTarget(ExpressionComponent expression)
        => expression.transform.parent;
}

internal sealed class ExpressionOverrideSettingsGroupDrawer : ISectionDrawer
{
    private readonly Entry[] _entries;

    public ExpressionOverrideSettingsGroupDrawer(
        SerializedObject serializedObject,
        ExpressionSettingsInheritance inheritance)
    {
        _entries = new[]
        {
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
        var height = 0f;
        foreach (var entry in _entries)
            height += GUIHelper.GetShurikenSectionHeight(entry.Foldout, entry.GetContentHeight());
        return height;
    }

    public void Draw(Rect position)
    {
        foreach (var entry in _entries)
        {
            var contentHeight = entry.GetContentHeight();
            var section = new Rect(
                position.x,
                position.y,
                position.width,
                GUIHelper.GetShurikenSectionHeight(entry.Foldout, contentHeight));
            var headerWidth = entry.Foldout.Expanded ? entry.GetHeaderWidth() : 0f;
            if (GUIHelper.DrawShurikenSection(
                    section,
                    entry.Foldout,
                    entry.LabelKey.LG(),
                    contentHeight,
                    out var content,
                    drawHeader: entry.Foldout.Expanded ? entry.DrawHeader : null,
                    headerWidth: headerWidth))
            {
                content.height = contentHeight;
                entry.Draw(content);
            }
            position.y = section.yMax;
        }
    }

    private sealed class Entry
    {
        private readonly SerializedProperty _enabled;
        private readonly SerializedProperty _local;
        private readonly ExpressionInheritedSettingKind _kind;
        private readonly ExpressionSettingsInheritance _inheritance;

        public readonly string LabelKey;
        public FoldoutState Foldout { get; } = new(false);

        public Entry(
            SerializedProperty enabled,
            SerializedProperty local,
            string labelKey,
            ExpressionInheritedSettingKind kind,
            ExpressionSettingsInheritance inheritance)
            => (_enabled, _local, LabelKey, _kind, _inheritance) =
                (enabled, local, labelKey, kind, inheritance);

        private bool IsReferenceable
            => _kind is ExpressionInheritedSettingKind.EyeBlink or ExpressionInheritedSettingKind.LipSync;

        private bool ShowsLocalValue => _enabled.boolValue && !_enabled.hasMultipleDifferentValues;

        private SerializedProperty? SourceMode
            => IsReferenceable ? _local.FindPropertyRelative("SourceMode") : null;

        public float GetContentHeight()
        {
            var value = GetDisplayedValue();
            if (!ShowsLocalValue || !IsReferenceable)
                return EditorGUI.GetPropertyHeight(value, GUIContent.none, true);
            var direct = _local.FindPropertyRelative("Direct");
            return SettingsSourceGUI.GetHeight(
                _local,
                EditorGUI.GetPropertyHeight(direct, GUIContent.none, true),
                false);
        }

        public void Draw(Rect position)
        {
            var value = GetDisplayedValue();
            using var disabled = new EditorGUI.DisabledScope(!ShowsLocalValue);
            if (!ShowsLocalValue || !IsReferenceable)
            {
                EditorGUI.PropertyField(position, value, GUIContent.none, true);
                return;
            }

            var direct = _local.FindPropertyRelative("Direct");
            SettingsSourceGUI.Draw(
                position,
                _local,
                EditorGUI.GetPropertyHeight(direct, GUIContent.none, true),
                rect => EditorGUI.PropertyField(rect, direct, GUIContent.none, true),
                false);
        }

        public float GetHeaderWidth()
        {
            var keys = IsReferenceable
                ? new[]
                {
                    "expression.settingSource.short.standard",
                    "expression.settingSource.short.batch",
                    "settingsSourceMode.short.direct",
                    "settingsSourceMode.short.reference"
                }
                : new[]
                {
                    "expression.settingSource.short.standard",
                    "expression.settingSource.short.batch",
                    "settingsSourceMode.short.direct"
                };
            return GUIHelper.CompactPopupWidth(keys.Select(key => key.LG()));
        }

        public void DrawHeader(Rect position)
        {
            var keys = GetModeKeys();
            var hasOwner = _inheritance.GetOwner(_kind) != null;
            var localOffset = hasOwner ? 1 : 2;
            var selected = !ShowsLocalValue ? 0 : localOffset + (SourceMode?.enumValueIndex ?? 0);
            var currentKey = !ShowsLocalValue
                ? hasOwner ? "expression.settingSource.short.batch" : "expression.settingSource.short.standard"
                : SourceMode?.enumValueIndex == (int)SettingsSourceMode.Reference
                    ? "settingsSourceMode.short.reference"
                    : "settingsSourceMode.short.direct";
            GUIHelper.CompactPopup(
                position,
                _enabled.hasMultipleDifferentValues ? EditorGUIUtility.TrTextContent("—") : currentKey.LG(),
                keys.Select(key => key.LG()).ToArray(),
                selected,
                index => SetMode(index, hasOwner),
                _enabled.hasMultipleDifferentValues);
        }

        private string[] GetModeKeys()
        {
            var inherited = _inheritance.GetOwner(_kind) != null
                ? new[] { "expression.settingSource.option.batch" }
                : new[] { "expression.settingSource.option.standard", "expression.settingSource.option.batch" };
            var local = IsReferenceable
                ? new[] { "settingsSourceMode.option.direct", "settingsSourceMode.option.reference" }
                : new[] { "settingsSourceMode.option.direct" };
            return inherited.Concat(local).ToArray();
        }

        private void SetMode(int mode, bool hasOwner)
        {
            if (mode == 0)
            {
                SetLocalOverride(false);
                return;
            }
            if (!hasOwner && mode == 1)
            {
                if (_inheritance.CanCreateBatchOverride) _inheritance.CreateBatchOverride(_kind);
                return;
            }

            if (!ShowsLocalValue) SetLocalOverride(true);
            if (SourceMode != null)
            {
                _enabled.serializedObject.UpdateIfRequiredOrScript();
                SourceMode.enumValueIndex = mode - (hasOwner ? 1 : 2);
                _enabled.serializedObject.ApplyModifiedProperties();
            }
        }

        private void SetLocalOverride(bool enabled)
        {
            _enabled.serializedObject.UpdateIfRequiredOrScript();
            if (enabled)
                _inheritance.InitializeOverride(_local, _kind);
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
        GUIHelper.DrawLocalizedEnum(ref position, _eyeBlink, "facialSettings.allowEyeBlink.label", nameof(TrackingPermission));
        GUIHelper.DrawLocalizedEnum(ref position, _lipSync, "facialSettings.allowLipSync.label", nameof(TrackingPermission));
        GUIHelper.LocalizedEnumPopup(position, _writeMode, "expression.application.label", new[] { "expression.application.replace.label", "expression.application.blend.label" });
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
