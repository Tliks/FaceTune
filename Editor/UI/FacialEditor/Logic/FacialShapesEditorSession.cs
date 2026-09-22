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
    public BlendShapeOverrideManager DataManager { get; }
    public BlendShapeGrouping GroupManager { get; }
    public PreviewManager PreviewManager { get; }
    public FacialShapeUI UI { get; }

    public FacialShapesEditorContext(
        SerializedObject serializedObject,
        BlendShapeOverrideManager dataManager,
        VisualElement root,
        SkinnedMeshRenderer? renderer,
        Object? target,
        string? animationPropertyPath,
        Func<SkinnedMeshRenderer?, bool> tryChangeRenderer,
        Action save)
    {
        SerializedObject = serializedObject;
        DataManager = dataManager;
        Renderer = renderer;
        Target = target;
        AnimationPropertyPath = animationPropertyPath;
        CanChangeTarget = target is AnimationClip;

        GroupManager = new BlendShapeGrouping(DataManager);
        PreviewManager = new PreviewManager(DataManager, root, Renderer);
        UI = new FacialShapeUI(root, this, tryChangeRenderer, save);
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
        DataManager.Dispose();
        SerializedObject.Dispose();
    }
}
