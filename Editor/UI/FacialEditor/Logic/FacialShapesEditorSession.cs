using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class FacialShapesEditorContext : IDisposable
{
    public SkinnedMeshRenderer? Renderer { get; }
    public Object? Target { get; private set; }
    public string? AnimationPropertyPath { get; }
    public bool CanChangeTarget { get; }
    public bool CanChangeRenderer => CanChangeTarget;
    public bool ZeroUnspecifiedBlendShapes { get; set; } = true;
    public bool ZeroUnavailableBlendShapes { get; set; } = true;

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
        VisualElement root,
        SkinnedMeshRenderer? renderer,
        Object? target,
        string? animationPropertyPath,
        Func<SkinnedMeshRenderer?, bool> tryChangeRenderer,
        Action save)
    {
        if (dataManagers.Count == 0) throw new ArgumentException("At least one shape list is required.");

        SerializedObject = serializedObject;
        DataManagers = dataManagers;
        Renderer = renderer;
        Target = target;
        AnimationPropertyPath = animationPropertyPath;
        CanChangeTarget = target is AnimationClip;

        GroupManager = new BlendShapeGrouping(dataManagers[0]);
        PreviewManager = new PreviewManager(this, root);
        UI = new FacialShapeUI(root, this, tryChangeRenderer, save);
    }

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
