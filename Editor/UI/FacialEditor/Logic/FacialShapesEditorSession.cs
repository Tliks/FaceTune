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
    public ShapesEditorModeSession ModeSession { get; }
    public ShapesEditorMode Mode => ModeSession.Kind;
    public SkinnedMeshRenderer? Renderer { get; }
    public Object? Target { get; private set; }
    public string? AnimationPropertyPath { get; }
    public bool CanChangeTarget { get; }
    public bool CanChangeRenderer => CanChangeTarget;
    public bool CanImportClip => ModeSession.CanImportClip;
    public bool UsesFacialIgnoredNames => ModeSession.UsesFacialIgnoredNames;
    public float InitialPreviewTime => ModeSession.InitialPreviewTime;
    public bool ZeroUnspecifiedBlendShapes { get; set; } = true;
    public bool ZeroUnavailableBlendShapes { get; set; } = true;
    public ImmutableBlendShapeWeightSet Background { get; }
    public float? BackgroundDefaultValue { get; }
    public ImmutableHashSet<string> IgnoredNames { get; }
    public int EditableListCount { get; }
    private readonly Action<int> _initializeList;

    private readonly SerializedObject _serializedObject;
    public IReadOnlyList<BlendShapeOverrideManager> DataManagers { get; }
    public BlendShapeOverrideManager DataManager => DataManagers[ActiveListIndex];
    public int ActiveListIndex { get; private set; }
    public BlendShapeCatalog Catalog { get; }
    public BlendShapeGrouping GroupManager { get; }
    public PreviewManager PreviewManager { get; }
    public FacialShapeUI UI { get; }

    public event Action? ActiveListChanged;

    public FacialShapesEditorContext(
        SerializedObject serializedObject,
        IReadOnlyList<BlendShapeOverrideManager> dataManagers,
        ShapesEditorModeSession modeSession,
        VisualElement root,
        SkinnedMeshRenderer? renderer,
        Object? target,
        string? animationPropertyPath,
        IEnumerable<BlendShapeWeightAnimation>? background,
        float? backgroundDefaultValue,
        ImmutableHashSet<string> ignoredNames,
        int editableListCount,
        Action<int> initializeList,
        Func<SkinnedMeshRenderer?, bool> tryChangeRenderer,
        Action save)
    {
        if (dataManagers.Count == 0) throw new ArgumentException("At least one shape list is required.");

        _serializedObject = serializedObject;
        DataManagers = dataManagers;
        ModeSession = modeSession;
        Renderer = renderer;
        Target = target;
        AnimationPropertyPath = animationPropertyPath;
        CanChangeTarget = target is AnimationClip;
        EditableListCount = Mathf.Clamp(editableListCount, 0, dataManagers.Count);
        _initializeList = initializeList;
        Background = new ImmutableBlendShapeWeightSet(
            background?.Select(animation => animation.ToFirstFrameBlendShape())
            ?? Enumerable.Empty<BlendShapeWeight>());
        BackgroundDefaultValue = backgroundDefaultValue;
        IgnoredNames = ignoredNames;

        Catalog = new BlendShapeCatalog(renderer);
        GroupManager = new BlendShapeGrouping(Catalog.Names);
        PreviewManager = new PreviewManager(this, root);
        UI = new FacialShapeUI(root, this, tryChangeRenderer, save);
    }

    public bool IsListEditable(int index) => index < EditableListCount;


    public void SetActiveList(int index)
    {
        if ((uint)index >= (uint)DataManagers.Count) return;
        _initializeList(index);
        if (ActiveListIndex == index) return;
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
        _serializedObject.Dispose();
    }
}
