namespace com.aoyon.facetune.ui;

[CanEditMultipleObjects]
[CustomEditor(typeof(FaceTuneComponent))]
internal class FaceTuneEditor : FaceTuneCustomEditorBase<FaceTuneComponent>
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (Context == null) return;

        if (Component.DefaultExpressionComponent == null)
        {
            if (GUILayout.Button("Create Default Expression"))
            {
                CreateDefaultExpression();
                return;
            }
        }
        else if (Component.DefaultExpressionComponent.BlendShapes.Count != Context.FaceRenderer.sharedMesh.blendShapeCount)
        {
            if (GUILayout.Button("Update Default Expression"))
            {
                UpdateDefaultExpression();
                return;
            }
        }
    }

    private void CreateDefaultExpression()
    {
        if (Context == null) return;

        var defaultBlendShapes = Context.FaceRenderer.GetBlendShapes(Context.FaceMesh);

        var defaultObject = new GameObject("Default");
        defaultObject.transform.SetParent(Component.transform, false);
        defaultObject.transform.SetAsFirstSibling();

        var defaultExpressionComponent = defaultObject.AddComponent<FacialExpressionComponent>();
        FacialExpressionEditorUtility.UpdateShapes(defaultExpressionComponent, defaultBlendShapes);

        Component.DefaultExpressionComponent = defaultExpressionComponent;

        Undo.RegisterCreatedObjectUndo(defaultObject, "Create Default Expression Component");
    }

    private void UpdateDefaultExpression()
    {
        if (Context == null) return;

        var defaultBlendShapes = Context.FaceRenderer.GetBlendShapes(Context.FaceMesh);
        FacialExpressionEditorUtility.UpdateShapes(Component.DefaultExpressionComponent!, defaultBlendShapes);
    }
}

