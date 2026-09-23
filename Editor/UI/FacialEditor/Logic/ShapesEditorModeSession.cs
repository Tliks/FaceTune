using Aoyon.FaceTune.Preview;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal abstract class ShapesEditorModeSession
{
    public abstract ShapesEditorMode Kind { get; }
    public virtual bool CanImportClip => true;
    public virtual bool UsesFacialIgnoredNames => false;
    public virtual float InitialPreviewTime => 0f;
    public virtual bool HasChanges => false;
    public virtual bool CanRedoDraft => false;
    public abstract bool CanRestoreInitial { get; }
    public abstract bool CanRestoreEdited { get; }
    public virtual float GetPreviewOpacity(float normalizedTime) => 1f;
    public event Action? Changed;
    public event Action? PreviewChanged;

    protected void NotifyChanged() => Changed?.Invoke();
    protected void NotifyPreviewChanged() => PreviewChanged?.Invoke();

    public abstract void BuildPreview(BlendShapeWeightSet result, float normalizedTime);
    public abstract void ImportClip(
        IReadOnlyList<BlendShapeWeightAnimation> animations,
        int activeListIndex);
    public abstract void RestoreInitial();
    public abstract void RestoreEdited();
    public virtual void SaveSettings(SerializedProperty settings)
        => throw new InvalidOperationException("This mode does not edit component settings.");
    public virtual bool SynchronizeAfterUndo() => false;
    public virtual void MarkSaved() { }

    protected static void AddTargetValues(
        BlendShapeWeightSet result,
        BlendShapeOverrideManager manager)
    {
        foreach (var index in manager.GetTargetIndices(i => !manager.IsUnavailable(i)))
        {
            result.Add(new BlendShapeWeight(
                manager.AllKeys[index],
                manager.GetShapeWeight(index)));
        }
    }
}

internal sealed class FacialModeSession : ShapesEditorModeSession
{
    private readonly BlendShapeOverrideManager _manager;

    public override ShapesEditorMode Kind => ShapesEditorMode.Facial;
    public override bool UsesFacialIgnoredNames => true;
    public override bool CanRestoreInitial => _manager.IsChangedFromInitialState;
    public override bool CanRestoreEdited => _manager.CanRestoreEditedOverrides;

    public FacialModeSession(BlendShapeOverrideManager manager) => _manager = manager;

    public override void BuildPreview(BlendShapeWeightSet result, float normalizedTime)
    {
        result.AddRange(_manager.EffectiveBaseSet);
        _manager.GetTargetValues(result);
    }

    public override void ImportClip(
        IReadOnlyList<BlendShapeWeightAnimation> animations,
        int activeListIndex)
        => _manager.AddShapesWithAnimations(animations);

    public override void RestoreInitial() => _manager.TryRestoreInitialOverrides();
    public override void RestoreEdited() => _manager.TryRestoreEditedOverrides();
}

internal sealed class EyeBlinkModeSession : ShapesEditorModeSession
{
    private readonly BlendShapeOverrideManager[] _managers;
    private readonly IReadOnlyList<BlendShapeWeightAnimation>? _builtIn;
    private readonly BlendShapeWeight[] _builtInClosed;
    private readonly EyeBlinkSettings _draft;
    private readonly SerializedObject _serializedObject;
    private readonly SerializedProperty _modeProperty;
    private readonly Action<int> _initializeList;
    private EyeBlinkSettings.Kind _mode;
    private bool _restoring;
    private bool _canRestoreEdited;
    private EyeBlinkSettings.Kind _editedModeBeforeRestore;
    private bool _canRedoMode;

    public override ShapesEditorMode Kind
        => _mode == EyeBlinkSettings.Kind.CustomAnimation
            ? ShapesEditorMode.EyeBlinkCustom : ShapesEditorMode.EyeBlinkSimple;
    public EyeBlinkSettings.Kind Mode => _mode;
    public override bool CanImportClip => _mode != EyeBlinkSettings.Kind.BuiltIn;
    public IReadOnlyList<BlendShapeWeightAnimation>? BuiltIn => _builtIn;
    public event Action? ModeChanged;
    public override float InitialPreviewTime => 1f;
    public float PlaybackDurationSeconds => _mode switch
    {
        EyeBlinkSettings.Kind.BuiltIn => _builtIn == null ? 0f : _builtIn
            .Select(EyeBlinkModeConversion.ClosingDuration).DefaultIfEmpty(0f).Max(),
        EyeBlinkSettings.Kind.SimpleAnimation => Mathf.Max(0f, _draft.SimpleDurationsSeconds.x),
        EyeBlinkSettings.Kind.CustomAnimation => BlendShapeAnimationPreview.GetDuration(
            GetAvailableCustomAnimations()),
        _ => 0f
    };
    public override float GetPreviewOpacity(float normalizedTime)
        => _mode == EyeBlinkSettings.Kind.CustomAnimation ? 1f : normalizedTime;
    public override bool CanRestoreInitial
        => _managers.Any(manager => manager.IsChangedFromInitialState) || HasChanges;
    private EyeBlinkSettings.Kind _initialMode;
    public override bool HasChanges
        => (EyeBlinkSettings.Kind)_modeProperty.intValue != _initialMode;
    public override bool CanRedoDraft => _canRedoMode;
    public override bool CanRestoreEdited
        => _canRestoreEdited
           && (_editedModeBeforeRestore != _mode
               || _managers.Any(manager => manager.CanRestoreEditedOverrides));

    public EyeBlinkModeSession(BlendShapeOverrideManager[] managers, EyeBlinkSettings draft,
        IReadOnlyList<BlendShapeWeightAnimation>? builtIn, SerializedObject serializedObject,
        Action<int> initializeList)
    {
        _managers = managers;
        _draft = draft;
        _serializedObject = serializedObject;
        _initializeList = initializeList;
        _modeProperty = serializedObject.FindProperty("_eyeBlinkDraft")
            .FindPropertyRelative(nameof(EyeBlinkSettings.EyeBlinkMode));
        _mode = draft.EyeBlinkMode;
        _initialMode = _mode;
        _builtIn = builtIn;
        _builtInClosed = builtIn?.Select(animation => new BlendShapeWeight(
            animation.Name, EyeBlinkModeConversion.ClosedWeight(animation)))
            .ToArray() ?? Array.Empty<BlendShapeWeight>();
        foreach (var manager in managers)
        {
            manager.OnAnyDataChange += () =>
            {
                if (!_restoring) _canRestoreEdited = false;
            };
        }
    }

    public override void BuildPreview(BlendShapeWeightSet result, float normalizedTime)
    {
        if (_mode == EyeBlinkSettings.Kind.SimpleAnimation)
        {
            AddTargetValues(result, _managers[1]);
            AddTargetValues(result, _managers[0]);
            return;
        }
        if (_mode == EyeBlinkSettings.Kind.BuiltIn)
        {
            result.AddRange(_builtInClosed);
            return;
        }

        var animations = GetAvailableCustomAnimations();
        result.AddRange(BlendShapeAnimationPreview.Evaluate(
            animations,
            BlendShapeAnimationPreview.GetDuration(animations) * normalizedTime));
    }

    private List<BlendShapeWeightAnimation> GetAvailableCustomAnimations()
    {
        var manager = _managers[2];
        var animations = GetAnimations(manager);
        animations.RemoveAll(animation =>
        {
            var index = manager.GetIndexForShape(animation.Name);
            return index < 0 || manager.IsUnavailable(index);
        });
        return animations;
    }

    public void SetMode(EyeBlinkSettings.Kind mode)
    {
        if (_mode == mode) return;
        _canRestoreEdited = false;
        _canRedoMode = false;
        if (mode == EyeBlinkSettings.Kind.SimpleAnimation)
        {
            _initializeList(0);
            _initializeList(1);
        }
        else if (mode == EyeBlinkSettings.Kind.CustomAnimation)
            _initializeList(2);

        IReadOnlyList<BlendShapeWeightAnimation>? source = _mode switch
        {
            EyeBlinkSettings.Kind.BuiltIn => _builtIn,
            EyeBlinkSettings.Kind.SimpleAnimation => GetAnimations(_managers[0]),
            EyeBlinkSettings.Kind.CustomAnimation => GetAnimations(_managers[2]),
            _ => null
        };
        if (source is { Count: > 0 } && mode != EyeBlinkSettings.Kind.BuiltIn)
        {
            var converted = mode == EyeBlinkSettings.Kind.SimpleAnimation
                ? EyeBlinkModeConversion.ToSimple(source, _mode == EyeBlinkSettings.Kind.CustomAnimation)
                : _mode == EyeBlinkSettings.Kind.SimpleAnimation
                    ? EyeBlinkModeConversion.ToCustom(source, _draft.SimpleDurationsSeconds)
                    : source;
            var target = _managers[mode == EyeBlinkSettings.Kind.SimpleAnimation ? 0 : 2];
            target.RemoveShapes(target.GetTargetIndices(_ => true).ToArray());
            target.AddShapesWithAnimations(converted);
        }
        SetDraftMode(mode);
        NotifyChanged();
        NotifyPreviewChanged();
        ModeChanged?.Invoke();
    }

    private void SetDraftMode(EyeBlinkSettings.Kind mode)
    {
        _serializedObject.UpdateIfRequiredOrScript();
        _modeProperty.intValue = (int)mode;
        _serializedObject.ApplyModifiedProperties();
        _mode = mode;
    }

    private static List<BlendShapeWeightAnimation> GetAnimations(BlendShapeOverrideManager manager)
    {
        var result = new List<BlendShapeWeightAnimation>();
        manager.GetTargetAnimations(result);
        return result;
    }

    public override void ImportClip(
        IReadOnlyList<BlendShapeWeightAnimation> animations,
        int activeListIndex)
    {
        if (_mode == EyeBlinkSettings.Kind.BuiltIn) return;
        var manager = _managers[_mode == EyeBlinkSettings.Kind.CustomAnimation
            ? 2 : Mathf.Clamp(activeListIndex, 0, 1)];
        if (_mode == EyeBlinkSettings.Kind.CustomAnimation)
        {
            manager.AddShapesWithAnimations(animations);
            return;
        }
        manager.AddShapesWithWeight(animations.Select(animation =>
            (manager.GetIndexForShape(animation.Name), activeListIndex == 0
                ? EyeBlinkModeConversion.ClosedWeight(animation)
                : animation.Weight(0f))));
    }

    public override void RestoreInitial()
    {
        var hadChanges = CanRestoreInitial;
        _canRedoMode = false;
        _editedModeBeforeRestore = _mode;
        _restoring = true;
        foreach (var manager in _managers)
            manager.TryRestoreInitialOverrides();
        _restoring = false;
        _canRestoreEdited = hadChanges;
        SetDraftMode(_initialMode);
        ModeChanged?.Invoke();
        NotifyChanged();
        NotifyPreviewChanged();
    }

    public override void RestoreEdited()
    {
        if (!CanRestoreEdited) return;
        _canRedoMode = false;
        _restoring = true;
        foreach (var manager in _managers)
            manager.TryRestoreEditedOverrides();
        _restoring = false;
        SetDraftMode(_editedModeBeforeRestore);
        _canRestoreEdited = false;
        ModeChanged?.Invoke();
        NotifyChanged();
        NotifyPreviewChanged();
    }

    public override void MarkSaved()
    {
        _initialMode = _mode;
        _canRestoreEdited = false;
        _canRedoMode = false;
        NotifyChanged();
    }

    public override bool SynchronizeAfterUndo()
    {
        _serializedObject.UpdateIfRequiredOrScript();
        var current = (EyeBlinkSettings.Kind)_modeProperty.intValue;
        if (_mode == current) return false;
        var previous = _mode;
        _mode = current;
        _canRedoMode = _mode == _initialMode && previous != _initialMode;
        ModeChanged?.Invoke();
        NotifyChanged();
        NotifyPreviewChanged();
        return false;
    }

    public override void SaveSettings(SerializedProperty settings)
    {
        settings.FindPropertyRelative(nameof(EyeBlinkSettings.EyeBlinkMode)).intValue = (int)_mode;
        if (_managers[0].IsInitialized)
            ShapeListSerialization.Save(
                settings.FindPropertyRelative(nameof(EyeBlinkSettings.SimpleBlinkBlendShapes)),
                _managers[0],
                animations: false);
        if (_managers[1].IsInitialized)
            ShapeListSerialization.Save(
                settings.FindPropertyRelative(nameof(EyeBlinkSettings.SimpleConflictPreventionBlendShapes)),
                _managers[1],
                animations: false);
        if (_managers[2].IsInitialized)
            ShapeListSerialization.Save(
                settings.FindPropertyRelative(nameof(EyeBlinkSettings.Animations)),
                _managers[2],
                animations: true);
    }
}

internal static class EyeBlinkModeConversion
{
    public static float ClosingDuration(BlendShapeWeightAnimation animation)
    {
        var keys = animation.Curve.keys;
        if (keys.Length == 0) return 0f;
        var closedWeight = keys.Max(key => key.value);
        return keys.First(key => key.value == closedWeight).time;
    }

    public static float ClosedWeight(BlendShapeWeightAnimation animation)
        => animation.Curve.keys.Select(key => key.value).DefaultIfEmpty(0f).Max();

    public static IReadOnlyList<BlendShapeWeightAnimation> ToSimple(
        IReadOnlyList<BlendShapeWeightAnimation> source, bool fromCustom)
        => source.Select(animation => BlendShapeWeightAnimation.SingleFrame(
            animation.Name,
            fromCustom ? 100f : ClosedWeight(animation)))
            .ToArray();

    public static IReadOnlyList<BlendShapeWeightAnimation> ToCustom(
        IReadOnlyList<BlendShapeWeightAnimation> source, Vector3 durations)
    {
        var closing = Mathf.Max(0f, durations.x);
        var hold = Mathf.Max(0f, durations.y);
        var opening = Mathf.Max(0f, durations.z);
        return source.Select(animation =>
        {
            var weight = animation.Weight(0f);
            var keys = new List<Keyframe> { new(0f, closing <= 0f ? weight : 0f) };
            if (closing > 0f) keys.Add(new Keyframe(closing, weight));
            if (hold > 0f) keys.Add(new Keyframe(closing + hold, weight));
            if (opening > 0f) keys.Add(new Keyframe(closing + hold + opening, 0f));
            return new BlendShapeWeightAnimation(animation.Name, new AnimationCurve(keys.ToArray()));
        }).ToArray();
    }
}

internal sealed class LipSyncModeSession : ShapesEditorModeSession
{
    private readonly BlendShapeOverrideManager _canceller;
    private bool _restoring;
    private bool _canRestoreEdited;
    public LipSyncEditing Editing { get; }

    public override ShapesEditorMode Kind => ShapesEditorMode.LipSync;
    public override bool CanImportClip
        => Editing.CancellerSelected || Editing.Draft.Mode == LipSyncSettings.Kind.Custom;
    public override bool HasChanges => Editing.HasChanges;
    public override bool CanRedoDraft => Editing.CanRedoDraft;
    public override bool CanRestoreInitial
        => Editing.HasChanges || _canceller.IsChangedFromInitialState;
    public override bool CanRestoreEdited
        => _canRestoreEdited
           && (Editing.CanRestoreEdited || _canceller.CanRestoreEditedOverrides);

    public LipSyncModeSession(
        BlendShapeOverrideManager canceller,
        LipSyncEditing editing)
    {
        _canceller = canceller;
        Editing = editing;
        Editing.DataChanged += () =>
        {
            if (!_restoring) _canRestoreEdited = false;
            NotifyChanged();
        };
        Editing.PreviewChanged += NotifyPreviewChanged;
        _canceller.OnAnyDataChange += () =>
        {
            if (!_restoring) _canRestoreEdited = false;
        };
    }

    public override void BuildPreview(BlendShapeWeightSet result, float normalizedTime)
    {
        AddTargetValues(result, _canceller);
        if (Editing.PreviewShapes is not { } lipSync) return;
        foreach (var shape in lipSync.GetShapes(Editing.PreviewViseme))
        {
            if (!Editing.UnavailableNames.Contains(shape.Name))
                result.Add(shape);
        }
    }

    public override void ImportClip(
        IReadOnlyList<BlendShapeWeightAnimation> animations,
        int activeListIndex)
    {
        if (!CanImportClip) return;
        if (Editing.CancellerSelected)
        {
            _canceller.AddShapesWithWeight(animations
                .Select(animation =>
                    (_canceller.GetIndexForShape(animation.Name), animation.Weight(0f)))
                .Where(value => value.Item1 >= 0 && !_canceller.IsUnavailable(value.Item1)));
            return;
        }
        Editing.SetShapes(
            animations
                .Where(animation => _canceller.GetIndexForShape(animation.Name) >= 0)
                .Select(animation =>
                    new BlendShapeWeight(animation.Name, animation.Weight(0f))),
            replaceExisting: false);
    }

    public override void RestoreInitial()
    {
        var hadChanges = CanRestoreInitial;
        _restoring = true;
        Editing.TryRestoreInitial();
        _canceller.TryRestoreInitialOverrides();
        _restoring = false;
        _canRestoreEdited = hadChanges;
        NotifyChanged();
    }

    public override void RestoreEdited()
    {
        if (!CanRestoreEdited) return;
        _restoring = true;
        Editing.TryRestoreEdited();
        _canceller.TryRestoreEditedOverrides();
        _restoring = false;
        _canRestoreEdited = false;
        NotifyChanged();
    }

    public override void SaveSettings(SerializedProperty settings)
    {
        settings.FindPropertyRelative(nameof(LipSyncSettings.Mode)).intValue =
            (int)Editing.Draft.Mode;
        settings.FindPropertyRelative(nameof(LipSyncSettings.Shapes))
            .CopyFrom(Editing.Draft.Shapes);
        ShapeListSerialization.Save(
            settings.FindPropertyRelative(nameof(LipSyncSettings.CancellerBlendShapes)),
            _canceller,
            animations: false);
    }

    public override bool SynchronizeAfterUndo() => Editing.SynchronizeAfterUndo();
    public override void MarkSaved() => Editing.MarkSaved();
}
