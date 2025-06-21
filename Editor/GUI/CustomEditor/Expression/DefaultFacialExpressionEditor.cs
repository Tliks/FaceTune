using com.aoyon.facetune.preview;

namespace com.aoyon.facetune.ui;

[CustomEditor(typeof(DefaultFacialExpressionComponent))]
internal class DefaultFacialExpressionEditor : FaceTuneCustomEditorBase<DefaultFacialExpressionComponent>
{
    private SessionContext? _context;
    public override void OnEnable()
    {
        base.OnEnable();
        CustomEditorUtility.TryGetContext(Component.gameObject, out _context);
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (_context != null)
        {
            if (GUILayout.Button("Update From Scene"))
            {
                UpdateDefaultExpression();
            }
            if (GUILayout.Button("Open Editor"))
            {
                OpenFacialShapesEditor();
            }
        }

        TogglablePreviewDrawer.Draw(DefaultShapesPreview.ToggleNode);
    }

    private void UpdateDefaultExpression()
    {
        if (_context == null) return;
        var defaultBlendShapes = _context.FaceRenderer.GetBlendShapes(_context.FaceMesh);
        DefaultFacialExpressionEditorUtility.UpdateShapes(Component, defaultBlendShapes);
        serializedObject.ApplyModifiedProperties();
    }

    private void OpenFacialShapesEditor()
    {
        if (!CustomEditorUtility.TryGetContext(Component.gameObject, out var context)) return;
        var defaultBlendShapes = context.DEC.GetDefaultBlendShapeSet(Component.gameObject);
        var window = FacialShapesEditor.OpenEditor(context.FaceRenderer, context.FaceMesh, defaultBlendShapes, new(Component.GetFirstFrameBlendShapeSet(context).BlendShapes));
        if (window == null) return;
        window.RegisterApplyCallback(RecieveEditorResult);
    }

    private void RecieveEditorResult(BlendShapeSet result)
    {
        serializedObject.Update();
        DefaultFacialExpressionEditorUtility.UpdateShapes(Component, result.BlendShapes.ToList());
        serializedObject.ApplyModifiedProperties();
    }

    [MenuItem($"CONTEXT/{nameof(DefaultFacialExpressionComponent)}/ToClip")]
    private static void ToClip(MenuCommand command)
    {
        var component = (command.context as DefaultFacialExpressionComponent)!;
        CustomEditorUtility.ToClip(component.gameObject, context => component.GetFirstFrameBlendShapeSet(context).BlendShapes);
    }
}

internal static class DefaultFacialExpressionEditorUtility
{
    public static void UpdateShapes(DefaultFacialExpressionComponent component, IReadOnlyList<BlendShape> newShapes)
    {
        Undo.RecordObject(component, "Update Default Shapes");
        var animations = newShapes.Select(shape => BlendShapeAnimation.SingleFrame(shape.Name, shape.Weight)).ToList();
        component.BlendShapeAnimations = animations;
    }
}