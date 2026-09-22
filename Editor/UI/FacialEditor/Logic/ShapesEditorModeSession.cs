using Aoyon.FaceTune.Preview;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal abstract class ShapesEditorModeSession
{
    public abstract ShapesEditorMode Kind { get; }
    public virtual bool CanImportClip => true;
    public virtual bool UsesFacialIgnoredNames => false;
    public virtual float InitialPreviewTime => 0f;
    public virtual bool HasChanges => false;
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
    private readonly bool _simple;
    private bool _restoring;
    private bool _canRestoreEdited;

    public override ShapesEditorMode Kind
        => _simple ? ShapesEditorMode.EyeBlinkSimple : ShapesEditorMode.EyeBlinkCustom;
    public override float InitialPreviewTime => _simple ? 1f : 0f;
    public override float GetPreviewOpacity(float normalizedTime)
        => _simple ? normalizedTime : 1f;
    public override bool CanRestoreInitial
        => _managers.Any(manager => manager.IsChangedFromInitialState);
    public override bool CanRestoreEdited
        => _canRestoreEdited
           && _managers.Any(manager => manager.CanRestoreEditedOverrides);

    public EyeBlinkModeSession(BlendShapeOverrideManager[] managers, bool simple)
    {
        _managers = managers;
        _simple = simple;
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
        if (_simple)
        {
            AddTargetValues(result, _managers[1]);
            AddTargetValues(result, _managers[0]);
            return;
        }

        var manager = _managers[0];
        var animations = new List<BlendShapeWeightAnimation>();
        manager.GetTargetAnimations(animations);
        animations.RemoveAll(animation =>
        {
            var index = manager.GetIndexForShape(animation.Name);
            return index < 0 || manager.IsUnavailable(index);
        });
        result.AddRange(BlendShapeAnimationPreview.Evaluate(
            animations,
            BlendShapeAnimationPreview.GetDuration(animations) * normalizedTime));
    }

    public override void ImportClip(
        IReadOnlyList<BlendShapeWeightAnimation> animations,
        int activeListIndex)
    {
        var manager = _managers[Mathf.Clamp(activeListIndex, 0, _managers.Length - 1)];
        if (!_simple)
        {
            manager.AddShapesWithAnimations(animations);
            return;
        }
        manager.AddShapesWithWeight(animations.Select(animation =>
            (manager.GetIndexForShape(animation.Name), animation.Weight(0f))));
    }

    public override void RestoreInitial()
    {
        var hadChanges = CanRestoreInitial;
        _restoring = true;
        foreach (var manager in _managers)
            manager.TryRestoreInitialOverrides();
        _restoring = false;
        _canRestoreEdited = hadChanges;
    }

    public override void RestoreEdited()
    {
        if (!CanRestoreEdited) return;
        _restoring = true;
        foreach (var manager in _managers)
            manager.TryRestoreEditedOverrides();
        _restoring = false;
        _canRestoreEdited = false;
    }

    public override void SaveSettings(SerializedProperty settings)
    {
        if (_simple)
        {
            ShapeListSerialization.Save(
                settings.FindPropertyRelative(nameof(EyeBlinkSettings.SimpleBlinkBlendShapes)),
                _managers[0],
                animations: false);
            ShapeListSerialization.Save(
                settings.FindPropertyRelative(nameof(EyeBlinkSettings.SimpleConflictPreventionBlendShapes)),
                _managers[1],
                animations: false);
            return;
        }
        ShapeListSerialization.Save(
            settings.FindPropertyRelative(nameof(EyeBlinkSettings.Animations)),
            _managers[0],
            animations: true);
    }
}

internal sealed class LipSyncModeSession : ShapesEditorModeSession
{
    private readonly BlendShapeOverrideManager _canceller;
    private bool _restoring;
    private bool _canRestoreEdited;
    public LipSyncEditing Editing { get; }

    public override ShapesEditorMode Kind => ShapesEditorMode.LipSync;
    public override bool HasChanges => Editing.HasChanges;
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
