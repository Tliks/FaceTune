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
    public bool UsesFacialIgnoredNames => Mode == ShapesEditorMode.Facial;
    public float InitialPreviewTime => Mode == ShapesEditorMode.EyeBlinkSimple ? 1f : 0f;
    public bool ZeroUnspecifiedBlendShapes { get; set; } = true;
    public bool ZeroUnavailableBlendShapes { get; set; } = true;
    public ImmutableBlendShapeWeightSet Background { get; }
    public float? BackgroundDefaultValue { get; }
    public ImmutableHashSet<string> IgnoredNames { get; }
    public ISet<string> LipSyncUnavailableNames { get; }
    public int EditableListCount { get; }
    public LipSyncSettings? LipSync { get; }
    public VrcVisemeLipSyncShapes? BuiltInLipSync { get; }
    public SerializedProperty? LipSyncProperty { get; }
    public int SelectedViseme { get; private set; }
    public int HoveredViseme { get; private set; } = -1;
    public int PreviewViseme => HoveredViseme >= 0 ? HoveredViseme : SelectedViseme;
    private readonly Action<int> _initializeList;

    public SerializedObject SerializedObject { get; }
    public IReadOnlyList<BlendShapeOverrideManager> DataManagers { get; }
    public BlendShapeOverrideManager DataManager => DataManagers[ActiveListIndex];
    public int ActiveListIndex { get; private set; }
    public BlendShapeGrouping GroupManager { get; }
    public PreviewManager PreviewManager { get; }
    public FacialShapeUI UI { get; }

    public event Action? ActiveListChanged;
    public event Action? LipSyncChanged;

    public FacialShapesEditorContext(
        SerializedObject serializedObject,
        IReadOnlyList<BlendShapeOverrideManager> dataManagers,
        ShapesEditorMode mode,
        VisualElement root,
        SkinnedMeshRenderer? renderer,
        Object? target,
        string? animationPropertyPath,
        IEnumerable<BlendShapeWeightAnimation>? background,
        float? backgroundDefaultValue,
        ImmutableHashSet<string> ignoredNames,
        ISet<string> lipSyncUnavailableNames,
        int editableListCount,
        Action<int> initializeList,
        LipSyncSettings? lipSync,
        VrcVisemeLipSyncShapes? builtInLipSync,
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
        _initializeList = initializeList;
        LipSync = lipSync;
        BuiltInLipSync = builtInLipSync;
        LipSyncProperty = lipSync == null
            ? null
            : serializedObject.FindProperty("_lipSyncDraft");
        Background = new ImmutableBlendShapeWeightSet(
            background?.Select(animation => animation.ToFirstFrameBlendShape())
            ?? Enumerable.Empty<BlendShapeWeight>());
        BackgroundDefaultValue = backgroundDefaultValue;
        IgnoredNames = ignoredNames;
        LipSyncUnavailableNames = lipSyncUnavailableNames;

        GroupManager = new BlendShapeGrouping(dataManagers[0]);
        PreviewManager = new PreviewManager(this, root);
        UI = new FacialShapeUI(root, this, tryChangeRenderer, save);
    }

    public bool IsListEditable(int index) => index < EditableListCount;

    public VrcVisemeLipSyncShapes? PreviewLipSync
        => LipSync?.Mode == LipSyncSettings.Kind.Custom
            ? LipSync.Shapes
            : BuiltInLipSync;

    public void SetSelectedViseme(int index)
    {
        index = Mathf.Clamp(index, 0, VrcVisemeLipSyncShapes.Count - 1);
        if (SelectedViseme == index) return;
        SelectedViseme = index;
        LipSyncChanged?.Invoke();
    }

    public void SetHoveredViseme(int index)
    {
        index = Mathf.Clamp(index, -1, VrcVisemeLipSyncShapes.Count - 1);
        if (HoveredViseme == index) return;
        HoveredViseme = index;
        LipSyncChanged?.Invoke();
    }

    public void NotifyLipSyncChanged() => LipSyncChanged?.Invoke();

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
        SerializedObject.Dispose();
    }
}
