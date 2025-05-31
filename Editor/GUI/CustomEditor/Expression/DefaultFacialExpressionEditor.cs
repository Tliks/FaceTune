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
        TogglablePreviewDrawer.Draw(DefaultShapesPreview.ToggleNode);
        if (_context != null && Component.BlendShapes.Count != _context.FaceRenderer.sharedMesh.blendShapeCount)
        {
            if (GUILayout.Button("Update Default Expression"))
            {
                UpdateDefaultExpression();
            }
        }
    }

    private void UpdateDefaultExpression()
    {
        if (_context == null) return;
        var defaultBlendShapes = _context.FaceRenderer.GetBlendShapes(_context.FaceMesh);
        var blendShapesProperty = serializedObject.FindProperty(nameof(DefaultFacialExpressionComponent.BlendShapes));
        FacialExpressionEditorUtility.UpdateShapes(blendShapesProperty, defaultBlendShapes);
        serializedObject.ApplyModifiedProperties();
    }

    [MenuItem($"CONTEXT/{nameof(DefaultFacialExpressionComponent)}/ToClip")]
    private static void ToClip(MenuCommand command)
    {
        var component = (command.context as DefaultFacialExpressionComponent)!;
        CustomEditorUtility.ToClip(component.gameObject, _ => component.BlendShapes);
    }
}