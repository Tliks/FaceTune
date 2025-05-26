using com.aoyon.facetune.preview;

namespace com.aoyon.facetune.ui;

[CustomEditor(typeof(DefaultExpressionComponent))]
internal class DefaultExpressionEditor : FaceTuneCustomEditorBase<DefaultExpressionComponent>
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        TogglablePreviewDrawer.Draw(DefaultShapesPreview.ToggleNode);
        if (Context == null) return;
        if (Component.BlendShapes.Count != Context.FaceRenderer.sharedMesh.blendShapeCount)
        {
            if (GUILayout.Button("Update Default Expression"))
            {
                UpdateDefaultExpression();
            }
        }
    }

    private void UpdateDefaultExpression()
    {
        if (Context == null) return;
        var defaultBlendShapes = Context.FaceRenderer.GetBlendShapes(Context.FaceMesh);
        var blendShapesProperty = serializedObject.FindProperty(nameof(DefaultExpressionComponent.BlendShapes));
        FacialExpressionEditorUtility.UpdateShapes(blendShapesProperty, defaultBlendShapes);
        serializedObject.ApplyModifiedProperties();
    }
}
