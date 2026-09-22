using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal enum ShapesEditorMode
{
    Facial,
    EyeBlinkSimple,
    EyeBlinkCustom,
    LipSync
}

internal sealed class FacialShapesEditorContext : IDisposable
{
    public ShapesEditorMode Mode { get; }
    public SkinnedMeshRenderer? Renderer { get; }
    public Object? Target { get; private set; }
    public string? AnimationPropertyPath { get; }
    public bool CanChangeTarget { get; }
    public bool CanChangeRenderer => CanChangeTarget;
    public bool CanImportClip => Mode is ShapesEditorMode.Facial or ShapesEditorMode.EyeBlinkCustom;
    public bool UsesRendererBackground => Mode != ShapesEditorMode.Facial;
    public bool UsesFacialIgnoredNames => Mode == ShapesEditorMode.Facial;
    public float InitialPreviewTime => Mode == ShapesEditorMode.EyeBlinkSimple ? 1f : 0f;
    public bool ZeroUnspecifiedBlendShapes { get; set; } = true;
    public bool ZeroUnavailableBlendShapes { get; set; } = true;
    public ImmutableBlendShapeWeightSet Background { get; }
    public int EditableListCount { get; }

    public SerializedObject SerializedObject { get; }
    public IReadOnlyList<BlendShapeOverrideManager> DataManagers { get; }
    public BlendShapeOverrideManager DataManager => DataManagers[ActiveListIndex];
    public int ActiveListIndex { get; private set; }
    public BlendShapeGrouping GroupManager { get; }
    public PreviewManager PreviewManager { get; }
    public FacialShapeUI UI { get; }

    public event Action? ActiveListChanged;

    public FacialShapesEditorContext(
        SerializedObject serializedObject,
        IReadOnlyList<BlendShapeOverrideManager> dataManagers,
        ShapesEditorMode mode,
        VisualElement root,
        SkinnedMeshRenderer? renderer,
        Object? target,
        string? animationPropertyPath,
        IEnumerable<BlendShapeWeightAnimation>? background,
        int editableListCount,
        Func<SkinnedMeshRenderer?, bool> tryChangeRenderer,
        Action save)
    {
        if (dataManagers.Count == 0) throw new ArgumentException("At least one shape list is required.");

        SerializedObject = serializedObject;
        DataManagers = dataManagers;
        Mode = mode;
        Renderer = renderer;
        Target = target;
        AnimationPropertyPath = animationPropertyPath;
        CanChangeTarget = target is AnimationClip;
        EditableListCount = Mathf.Clamp(editableListCount, 0, dataManagers.Count);
        var backgroundShapes = background?.Select(animation => animation.ToFirstFrameBlendShape())
            ?? Enumerable.Empty<BlendShapeWeight>();
        Background = new ImmutableBlendShapeWeightSet(
            !UsesRendererBackground || renderer == null
                ? backgroundShapes
                : renderer.GetBlendShapeWeights(renderer.sharedMesh).Concat(backgroundShapes));

        GroupManager = new BlendShapeGrouping(dataManagers[0]);
        PreviewManager = new PreviewManager(this, root);
        UI = new FacialShapeUI(root, this, tryChangeRenderer, save);
    }

    public bool IsListEditable(int index) => index < EditableListCount;

    public void SetActiveList(int index)
    {
        if ((uint)index >= (uint)DataManagers.Count || ActiveListIndex == index) return;
        ActiveListIndex = index;
        ActiveListChanged?.Invoke();
    }

    public void SetTarget(Object? target)
    {
        if (!CanChangeTarget || target != null && target is not AnimationClip) return;
        Target = target;
    }

    public void Dispose()
    {
        UI.Dispose();
        PreviewManager.Dispose();
        foreach (var dataManager in DataManagers) dataManager.Dispose();
        SerializedObject.Dispose();
    }
}
