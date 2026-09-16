using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune.Gui;

internal enum ExpressionInheritedSettingKind
{
    EyeBlink,
    LipSync,
    Transition,
    Priority
}

internal sealed class ExpressionSettingsPreviewState : ScriptableObject
{
    [NonSerialized] internal ExpressionComponent? Component;

    public EyeBlinkSettings EyeBlink = new();
    public LipSyncSettings LipSync = new();
    public TransitionSettings Transition = new();
    public PrioritySettings Priority = new();
}

internal sealed class ExpressionDefinitionPreviewState : ScriptableObject
{
    [NonSerialized] internal ExpressionComponent? Component;

    public FacialBlendShapeData FacialBlendShapes = new();
    public NonFacialAnimationData NonFacialAnimations = new();
    public ExpressionWriteMode WriteMode = ExpressionBehavior.Default.WriteMode;
    public MultiFrameSettings MultiFrame = new();
    public TrackingPermission AllowEyeBlink = ExpressionBehavior.Default.AllowEyeBlink;
    public TrackingPermission AllowLipSync = ExpressionBehavior.Default.AllowLipSync;
    public bool HasEyeBlink = true;
    public SettingsReference EyeBlinkReference = new();
    public EyeBlinkSettings EyeBlink = new();
    public bool HasLipSync = true;
    public SettingsReference LipSyncReference = new();
    public LipSyncSettings LipSync = new();
}

internal sealed class ExpressionSettingsInheritance : IDisposable
{
    private readonly ExpressionComponent _component;
    private readonly bool _singleTarget;
    private readonly Action _repaint;
    private readonly ExpressionSettingsPreviewState _preview;
    private readonly SerializedObject _serializedPreview;
    private readonly ExpressionDefinitionPreviewState _definitionPreview;
    private readonly SerializedObject _serializedDefinitionPreview;
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
    private InheritanceSession? _session;
    private bool _dirty = true;

    public ExpressionSettingsInheritance(
        ExpressionComponent component,
        bool singleTarget,
        Action repaint)
    {
        _component = component;
        _singleTarget = singleTarget;
        _repaint = repaint;
        _preview = ScriptableObject.CreateInstance<ExpressionSettingsPreviewState>();
        _preview.hideFlags = HideFlags.HideAndDontSave;
        _preview.Component = component;
        _serializedPreview = new SerializedObject(_preview);
        _definitionPreview = ScriptableObject.CreateInstance<ExpressionDefinitionPreviewState>();
        _definitionPreview.hideFlags = HideFlags.HideAndDontSave;
        _definitionPreview.Component = component;
        _serializedDefinitionPreview = new SerializedObject(_definitionPreview);
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

    public void RefreshIfNeeded()
    {
        if (!_dirty) return;

        _dirty = false;
        _session?.Dispose();
        _session = null;
        if (!_singleTarget || _component == null)
        {
            ClearOwners();
            _serializedPreview.Update();
            _serializedDefinitionPreview.Update();
            return;
        }

        _session = new InheritanceSession(OnInvalidated);
        var context = _session.Context;
        context.Observe(_component);
        var root = _component.transform.root.gameObject;
        var eyeBlink = new EyeBlinkResolver(root, context);
        var lipSync = new LipSyncResolver(root, context);
        var transition = new TransitionResolver(root, context);
        var priority = new PriorityResolver(root, context);
        var definition = new ExpressionDefinitionResolver(context).Resolve(_component);
        if (definition is Component definitionComponent)
            context.Observe(definitionComponent);
        RefreshDefinitionPreview(
            definition,
            new ExpressionBehaviorResolver(context).Resolve(_component),
            new MultiFrameResolver(context).Resolve(_component),
            eyeBlink.ResolveDefinition(_component),
            lipSync.ResolveDefinition(_component),
            eyeBlink.Resolve(_component),
            lipSync.Resolve(_component));
        (_preview.EyeBlink, _eyeBlinkOwner) = eyeBlink.ResolveInherited(_component);
        (_preview.LipSync, _lipSyncOwner) = lipSync.ResolveInherited(_component);
        (_preview.Transition, _transitionOwner) = transition.ResolveInherited(_component);
        (_preview.Priority, _priorityOwner) = priority.ResolveInherited(_component);
        _batchOverrideTarget = _component.transform.parent;
        _serializedPreview.Update();
        _serializedDefinitionPreview.Update();
    }

    public SerializedObject DefinitionPreview => _serializedDefinitionPreview;

    private void OnInvalidated()
    {
        _dirty = true;
        _repaint();
    }

    private void RefreshDefinitionPreview(
        IExpressionDefinitionProvider? definition,
        ExpressionBehavior behavior,
        MultiFrameSettings multiFrame,
        EyeBlinkSettings? definitionEyeBlink,
        LipSyncSettings? definitionLipSync,
        EyeBlinkSettings eyeBlink,
        LipSyncSettings lipSync)
    {
        _definitionPreview.WriteMode = behavior.WriteMode;
        _definitionPreview.MultiFrame = multiFrame;
        _definitionPreview.AllowEyeBlink = behavior.AllowEyeBlink;
        _definitionPreview.AllowLipSync = behavior.AllowLipSync;
        _definitionPreview.HasEyeBlink = definitionEyeBlink != null;
        _definitionPreview.EyeBlinkReference = new SettingsReference();
        _definitionPreview.EyeBlink = eyeBlink;
        _definitionPreview.HasLipSync = definitionLipSync != null;
        _definitionPreview.LipSyncReference = new SettingsReference();
        _definitionPreview.LipSync = lipSync;

        switch (definition)
        {
            case ExpressionComponent expression:
                _definitionPreview.FacialBlendShapes = expression.FacialBlendShapes;
                _definitionPreview.NonFacialAnimations = expression.NonFacialAnimations;
                break;
            case ExpressionDataComponent data:
                _definitionPreview.FacialBlendShapes = data.HasFacialBlendShapes
                    ? data.FacialBlendShapes
                    : new FacialBlendShapeData();
                _definitionPreview.NonFacialAnimations = data.HasNonFacialAnimations
                    ? data.NonFacialAnimations
                    : new NonFacialAnimationData();
                break;
            default:
                _definitionPreview.FacialBlendShapes = new FacialBlendShapeData();
                _definitionPreview.NonFacialAnimations = new NonFacialAnimationData();
                break;
        }
    }

    public SerializedProperty GetValue(ExpressionInheritedSettingKind kind)
        => GetBinding(kind).Value;

    public void InitializeOverride(SerializedProperty target, ExpressionInheritedSettingKind kind)
        => GetBinding(kind).CopyPreview(target);

    public SettingsComponent? GetOwner(ExpressionInheritedSettingKind kind)
        => GetBinding(kind).GetOwner();

    public bool CanCreateBatchOverride => _singleTarget && _batchOverrideTarget != null;

    public bool CanOpenBatchOverride(ExpressionInheritedSettingKind kind)
        => GetOwner(kind) != null || CanCreateBatchOverride;

    public void OpenBatchOverride(ExpressionInheritedSettingKind kind)
    {
        var owner = GetOwner(kind);
        if (owner == null)
        {
            CreateBatchOverride(kind);
            return;
        }

        SettingsComponentEditor.RequestSectionExpansion(owner, kind);
        Selection.activeObject = owner;
        EditorGUIUtility.PingObject(owner);
    }

    public void CreateBatchOverride(ExpressionInheritedSettingKind kind)
    {
        if (_batchOverrideTarget == null) return;

        var owner = _component.transform.root.gameObject
            .GetComponentsInParentExcludingSelf<SettingsComponent>(_component, true)
            .LastOrDefault();
        if (owner == null)
            owner = Undo.AddComponent<SettingsComponent>(_batchOverrideTarget.gameObject);

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
        SettingsComponentEditor.RequestSectionExpansion(owner, kind);
        Selection.activeObject = owner;
        EditorGUIUtility.PingObject(owner);
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
        if (_preview != null)
            Object.DestroyImmediate(_preview);
        if (_definitionPreview != null)
            Object.DestroyImmediate(_definitionPreview);
    }

    private SettingBinding GetBinding(ExpressionInheritedSettingKind kind)
        => _bindings.TryGetValue(kind, out var binding)
            ? binding
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, null);

    private void ClearOwners()
    {
        (_eyeBlinkOwner, _lipSyncOwner, _transitionOwner, _priorityOwner) =
            (null, null, null, null);
        _batchOverrideTarget = null;
    }

    private sealed record SettingBinding(
        SerializedProperty Value,
        Func<SettingsComponent?> GetOwner,
        string EnabledPropertyName,
        string ValuePropertyName,
        string? ReferencePropertyName,
        Action<SerializedProperty> CopyPreview);

    private sealed class InheritanceSession : IDisposable
    {
        private bool _disposed;

        public InheritanceSession(Action onInvalidate)
        {
            Context = new ComputeContext(
                $"{nameof(ExpressionSettingsInheritance)}:{nameof(InheritanceSession)}");
            Context.InvokeOnInvalidate(this, session =>
            {
                if (!session._disposed) onInvalidate();
            });
        }

        public ComputeContext Context { get; }

        public void Dispose()
            => _disposed = true;
    }
}

internal sealed class ExpressionScopedSettingsGroupDrawer : ISectionDrawer
{
    private readonly Entry[] _entries;

    public ExpressionScopedSettingsGroupDrawer(
        SerializedObject serializedObject,
        ExpressionSettingsInheritance inheritance)
    {
        _entries = new[]
        {
            new Entry(
                "transition.section.label",
                new ExpressionScopedSettingSectionDrawer(
                    serializedObject,
                    nameof(ExpressionComponent.HasTransition),
                    nameof(ExpressionComponent.Transition),
                    null,
                    ExpressionInheritedSettingKind.Transition,
                    inheritance,
                    () => new TransitionSettings())),
            new Entry(
                "priority.section.label",
                new ExpressionScopedSettingSectionDrawer(
                    serializedObject,
                    nameof(ExpressionComponent.HasPriority),
                    nameof(ExpressionComponent.Priority),
                    null,
                    ExpressionInheritedSettingKind.Priority,
                    inheritance,
                    () => new PrioritySettings()))
        };
        Actions = new SectionActionSet(
            serializedObject,
            _entries.SelectMany(entry => entry.Drawer.Actions.Fields));
    }

    public SectionActionSet Actions { get; }

    public float GetHeight()
        => _entries.Sum(entry => GUIHelper.GetShurikenSectionHeight(
            entry.Foldout,
            entry.Drawer.GetHeight()))
         + GUIHelper.VerticalSpacing * (_entries.Length - 1);

    public void Draw(Rect position)
    {
        position.Indent(GUIHelper.NestedSectionIndent);
        var sharedHeaderWidth = _entries
            .Select(entry => entry.Drawer.GetHeaderWidth())
            .DefaultIfEmpty()
            .Max();
        for (var i = 0; i < _entries.Length; i++)
        {
            var entry = _entries[i];
            var contentHeight = entry.Drawer.GetHeight();
            var section = new Rect(
                position.x,
                position.y,
                position.width,
                GUIHelper.GetShurikenSectionHeight(entry.Foldout, contentHeight));
            var drawHeader = SectionHeaderGUI.GetDrawAction(
                entry.Drawer,
                entry.Foldout.Expanded);
            var headerWidth = drawHeader == null ? 0f : sharedHeaderWidth;
            if (GUIHelper.DrawShurikenSection(
                    section,
                    entry.Foldout,
                    entry.LabelKey.LG(),
                    contentHeight,
                    out var content,
                    createHeaderMenu: () => SectionHeaderMenu.Create(
                        entry.Actions,
                        enabled: SectionHeaderMenu.ActionsEnabled(entry.Drawer)),
                    drawHeader: drawHeader,
                    headerWidth: headerWidth,
                    propertyScope: entry.Drawer.Actions.ScopeProperty))
            {
                content.height = GUIHelper.LineHeight;
                entry.Drawer.Draw(content);
            }
            position.y = section.yMax;
            if (i + 1 < _entries.Length)
                position.y += GUIHelper.VerticalSpacing;
        }
    }

    private sealed class Entry
    {
        public Entry(string labelKey, ExpressionScopedSettingSectionDrawer drawer)
        {
            LabelKey = labelKey;
            Drawer = drawer;
            Actions = drawer.Actions.WithKey(labelKey);
        }

        public string LabelKey { get; }
        public ExpressionScopedSettingSectionDrawer Drawer { get; }
        public SectionActionSet Actions { get; }
        public FoldoutState Foldout { get; } = new(true);
    }
}

internal sealed class ExpressionScopedSettingSectionDrawer
    : ISectionDrawer, ICollapsedSectionHeaderDrawer, ISectionActionAvailability
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
    private readonly bool _showSourceActions;

    public ExpressionScopedSettingSectionDrawer(
        SerializedObject serializedObject,
        string enabledPropertyName,
        string localPropertyName,
        string? referencePropertyName,
        ExpressionInheritedSettingKind kind,
        ExpressionSettingsInheritance inheritance,
        Func<object?> createDefault,
        bool showSourceActions = false)
    {
        _enabled = serializedObject.FindProperty(enabledPropertyName);
        _local = serializedObject.FindProperty(localPropertyName);
        _kind = kind;
        _inheritance = inheritance;
        _showSourceActions = showSourceActions;
        if (referencePropertyName != null)
            _source = new SerializedReferenceableSettings(
                serializedObject,
                referencePropertyName,
                localPropertyName);
        var fields = new List<SectionActionField>
        {
            SectionActionField.From(_enabled, () => false)
        };
        if (_source == null)
        {
            fields.Add(SectionActionField.From(_local, createDefault));
        }
        else
        {
            fields.Add(SectionActionField.From(_source.Reference, () => new SettingsReference()));
            fields.Add(SectionActionField.From(_source.Direct, createDefault));
        }
        Actions = new SectionActionSet(serializedObject, fields);
    }

    public SectionActionSet Actions { get; }
    public bool ActionsEnabled => ShowsLocalValue;

    public float GetHeight()
        => GetValueHeight()
         + (_showSourceActions
             ? GUIHelper.LineHeight + GUIHelper.VerticalSpacing
             : ShowsInheritedValue
                 ? GUIHelper.LineHeight + GUIHelper.VerticalSpacing
                 : 0f);

    public float GetHeaderWidth()
        => GUIHelper.CompactPopupWidth(
            (_source == null ? DirectShortModeKeys : ReferenceableShortModeKeys)
            .Select(key => key.LG()));

    public void DrawHeader(Rect position)
    {
        var hasOwner = _inheritance.GetOwner(_kind) != null;
        var keys = GetModeKeys(hasOwner);
        var selected = !ShowsLocalValue
            ? 0
            : (hasOwner ? 1 : 2) + (_source?.Mode.enumValueIndex ?? 0);
        GUIHelper.CompactPopup(
            position,
            GetCurrentModeLabel(hasOwner),
            keys.Select(key => key.LG()).ToArray(),
            selected,
            index => SetMode(index, hasOwner),
            _enabled.hasMultipleDifferentValues,
            separatorBefore: _source == null ? -1 : keys.Length - 1,
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

    public void Draw(Rect position)
    {
        if (!_showSourceActions && ShowsInheritedValue)
        {
            using var disabled = new EditorGUI.DisabledScope(true);
            EditorGUI.ObjectField(
                position.SetSingleHeight(),
                "expression.settingSource.label".LG(),
                _inheritance.GetOwner(_kind),
                typeof(SettingsComponent),
                true);
            position.NewLine();
        }

        var value = GetDisplayedValue();
        var valueHeight = GetValueHeight();
        position.height = valueHeight;
        using (new EditorGUI.DisabledScope(!ShowsLocalValue))
        {
            if (!ShowsLocalValue || _source == null)
            {
                EditorGUI.PropertyField(position, value, GUIContent.none, true);
            }
            else
            {
                SettingsReferenceGUI.Draw(
                    position,
                    _source,
                    EditorGUI.GetPropertyHeight(_local, GUIContent.none, true),
                    rect => EditorGUI.PropertyField(rect, _local, GUIContent.none, true));
            }
        }

        if (!_showSourceActions) return;
        position.y += valueHeight + GUIHelper.VerticalSpacing;
        DrawSourceActions(position.SetSingleHeight());
    }

    private void DrawSourceActions(Rect position)
    {
        if (ShowsLocalValue)
        {
            if (GUI.Button(position, "expression.settingSource.action.useBatch".LG()))
                SetLocalOverride(false);
            return;
        }

        var buttons = position.FlexHorizontalSpaced(
            GUIHelper.HorizontalSpacing,
            new[] { 1f, 1f });
        using (new EditorGUI.DisabledScope(!_inheritance.CanOpenBatchOverride(_kind)))
        {
            if (GUI.Button(buttons[0], "expression.settingSource.action.editBatch".LG()))
                _inheritance.OpenBatchOverride(_kind);
        }
        if (GUI.Button(buttons[1], "expression.settingSource.action.editLocal".LG()))
            SetLocalOverride(true);
    }

    private float GetValueHeight()
    {
        var value = GetDisplayedValue();
        if (!ShowsLocalValue || _source == null)
            return EditorGUI.GetPropertyHeight(value, GUIContent.none, true);
        return SettingsReferenceGUI.GetHeight(
            _source,
            EditorGUI.GetPropertyHeight(_local, GUIContent.none, true));
    }

    private bool ShowsLocalValue
        => _enabled.boolValue && !_enabled.hasMultipleDifferentValues;

    private bool ShowsInheritedValue
        => !ShowsLocalValue && _inheritance.GetOwner(_kind) != null;

    private SerializedProperty GetDisplayedValue()
        => ShowsLocalValue ? _local : _inheritance.GetValue(_kind);

    private string[] GetModeKeys(bool hasOwner)
        => (hasOwner, _source != null) switch
        {
            (true, true) => InheritedReferenceableModeKeys,
            (false, true) => DefaultReferenceableModeKeys,
            (true, false) => InheritedDirectModeKeys,
            (false, false) => DefaultDirectModeKeys
        };

    private GUIContent GetCurrentModeLabel(bool hasOwner)
    {
        if (_enabled.hasMultipleDifferentValues)
            return EditorGUIUtility.TrTextContent("—");
        var key = !ShowsLocalValue
            ? hasOwner
                ? "expression.settingSource.short.batch"
                : "expression.settingSource.short.standard"
            : _source?.Mode.intValue == (int)SettingsReferenceMode.Reference
                ? "expression.settingSource.short.reference"
                : "expression.settingSource.short.setting";
        return key.LG();
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
            if (_inheritance.CanCreateBatchOverride)
                _inheritance.CreateBatchOverride(_kind);
            SetLocalOverride(false);
            return;
        }

        SetLocalOverride(true);
        if (_source != null)
        {
            _source.Mode.serializedObject.UpdateIfRequiredOrScript();
            _source.Mode.enumValueIndex = mode - (hasOwner ? 1 : 2);
            _source.Mode.serializedObject.ApplyModifiedProperties();
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
}
