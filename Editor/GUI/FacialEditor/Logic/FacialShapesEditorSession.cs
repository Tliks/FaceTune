using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class FacialShapesEditorContext : IDisposable
{
    public SkinnedMeshRenderer? Renderer { get; }
    public IShapesEditorTargeting Targeting { get; }
    public bool CanChangeRenderer { get; }

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
        IShapesEditorTargeting targeting,
        bool canChangeRenderer,
        Func<SkinnedMeshRenderer?, bool> tryChangeRenderer,
        Action save)
    {
        SerializedObject = serializedObject;
        DataManager = dataManager;
        Renderer = renderer;
        Targeting = targeting;
        CanChangeRenderer = canChangeRenderer;

        GroupManager = new BlendShapeGrouping(DataManager);

        PreviewManager = new PreviewManager(DataManager, root, Renderer);

        UI = new FacialShapeUI(root, this, tryChangeRenderer, save);
    }

    public static FacialShapesEditorContext Create(
        EditorWindow owner,
        VisualElement root,
        string dataManagerPropertyName,
        SkinnedMeshRenderer? renderer,
        IShapesEditorTargeting? targeting,
        IReadOnlyBlendShapeSet? styleSet,
        IReadOnlyBlendShapeSet? baseSet,
        IReadOnlyBlendShapeSet? defaultOverrides,
        Func<SkinnedMeshRenderer?, bool> tryChangeRenderer,
        Action save)
    {
        targeting ??= new AnimationClipTargeting();
        var serializedObject = new SerializedObject(owner);
        var dataManager = new BlendShapeOverrideManager(
            serializedObject,
            serializedObject.FindProperty(dataManagerPropertyName),
            renderer,
            styleSet,
            baseSet,
            defaultOverrides);
        return new FacialShapesEditorContext(
            serializedObject,
            dataManager,
            root,
            renderer,
            targeting,
            targeting is AnimationClipTargeting,
            tryChangeRenderer,
            save);
    }

    public void Dispose()
    {
        UI.Dispose();
        PreviewManager.Dispose();
        DataManager.Dispose();
        SerializedObject.Dispose();
    }
}
