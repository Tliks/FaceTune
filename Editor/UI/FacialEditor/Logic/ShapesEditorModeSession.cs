using Aoyon.FaceTune.Preview;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal abstract class ShapesEditorModeSession
{
    public abstract ShapesEditorMode Kind { get; }
    public virtual bool CanImportClip => true;
    public virtual bool UsesFacialIgnoredNames => false;
    public virtual float InitialPreviewTime => 0f;
    public virtual bool HasChanges => false;
    public virtual float GetPreviewOpacity(float normalizedTime) => 1f;
    public event Action? Changed;

    protected void NotifyChanged() => Changed?.Invoke();

    public abstract void BuildPreview(BlendShapeWeightSet result, float normalizedTime);
    public abstract void ImportClip(
        IReadOnlyList<BlendShapeWeightAnimation> animations,
        int activeListIndex);
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
}

internal sealed class EyeBlinkModeSession : ShapesEditorModeSession
{
    private readonly BlendShapeOverrideManager[] _managers;
    private readonly bool _simple;

    public override ShapesEditorMode Kind
        => _simple ? ShapesEditorMode.EyeBlinkSimple : ShapesEditorMode.EyeBlinkCustom;
    public override float InitialPreviewTime => _simple ? 1f : 0f;
    public override float GetPreviewOpacity(float normalizedTime)
        => _simple ? normalizedTime : 1f;

    public EyeBlinkModeSession(BlendShapeOverrideManager[] managers, bool simple)
    {
        _managers = managers;
        _simple = simple;
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
    public LipSyncEditing Editing { get; }

    public override ShapesEditorMode Kind => ShapesEditorMode.LipSync;
    public override bool HasChanges => Editing.HasChanges;

    public LipSyncModeSession(
        BlendShapeOverrideManager canceller,
        LipSyncEditing editing)
    {
        _canceller = canceller;
        Editing = editing;
        Editing.Changed += NotifyChanged;
    }

    public override void BuildPreview(BlendShapeWeightSet result, float normalizedTime)
    {
        AddTargetValues(result, _canceller);
        if (Editing.PreviewShapes is not { } lipSync) return;
        foreach (var shape in lipSync.GetOrderedShapes()[Editing.PreviewViseme])
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
