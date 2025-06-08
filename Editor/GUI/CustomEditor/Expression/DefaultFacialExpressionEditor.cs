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
        var blendShapesProperty = serializedObject.FindProperty(nameof(DefaultFacialExpressionComponent.BlendShapes));
        FacialExpressionEditorUtility.UpdateShapes(blendShapesProperty, defaultBlendShapes);
        serializedObject.ApplyModifiedProperties();
    }

    private void OpenFacialShapesEditor()
    {
        if (!CustomEditorUtility.TryGetContext(Component.gameObject, out var context)) return;
        var defaultBlendShapes = context.DEC.GetDefaultBlendShapeSet(Component.gameObject);
        var window = FacialShapesEditor.OpenEditor(context.FaceRenderer, context.FaceMesh, defaultBlendShapes.BlendShapes, new(Component.BlendShapes));
        if (window == null) return;
        window.RegisterApplyCallback(RecieveEditorResult);
    }

    private void RecieveEditorResult(BlendShapeSet result)
    {
        serializedObject.Update();
        var blendShapesProperty = serializedObject.FindProperty(nameof(DefaultFacialExpressionComponent.BlendShapes));
        FacialExpressionEditorUtility.UpdateShapes(blendShapesProperty, result.BlendShapes.ToList());
        serializedObject.ApplyModifiedProperties();
    }

    [MenuItem($"CONTEXT/{nameof(DefaultFacialExpressionComponent)}/ToClip")]
    private static void ToClip(MenuCommand command)
    {
        var component = (command.context as DefaultFacialExpressionComponent)!;
        CustomEditorUtility.ToClip(component.gameObject, _ => component.BlendShapes);
    }
}