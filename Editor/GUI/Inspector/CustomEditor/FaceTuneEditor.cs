namespace com.aoyon.facetune.ui;

[CustomEditor(typeof(FaceTuneComponent))]
internal class FaceTuneEditor : Editor
{
    private FaceTuneComponent _component = null!;

    void OnEnable()
    {
        _component = (target as FaceTuneComponent)!;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var faceRenderer = _component.GetFaceRenderer()?.sharedMesh;
        if (faceRenderer == null)
        {
            EditorGUILayout.HelpBox("Valid FaceObject is not set", MessageType.Error);
            return;
        }

        if (_component.DefaultExpressionComponent == null)
        {
            if (GUILayout.Button("Create Default Expression"))
            {
                CraateDefaultExpression();
            }
        }
        
        if (_component.DefaultExpressionComponent != null && _component.DefaultExpressionComponent.BlendShapes.Count != faceRenderer.blendShapeCount)
        {
            if (GUILayout.Button("Update Default Expression"))
            {
                UpdateDefaultExpression();
            }
        }
    }

    private void CraateDefaultExpression()
    {
        if (_component.TryGetSessionContext(out var context) is false) return;

        var defaultBlendShapes = BlendShape.GetShapesFor(context.FaceRenderer, context.FaceMesh);

        var defaultObject = new GameObject("Default");
        defaultObject.transform.SetParent(_component.transform, false);
        defaultObject.transform.SetAsFirstSibling();

        var defaultExpressionComponent = defaultObject.AddComponent<FacialExpressionComponent>();
        defaultExpressionComponent.BlendShapes.AddRange(defaultBlendShapes);

        _component.DefaultExpressionComponent = defaultExpressionComponent;

        Undo.RegisterCreatedObjectUndo(defaultObject, "Create Default Expression Component");
    }

    private void UpdateDefaultExpression()
    {
        if (_component.TryGetSessionContext(out var context) is false) return;

        var defaultBlendShapes = BlendShape.GetShapesFor(context.FaceRenderer, context.FaceMesh);

        Undo.RecordObject(_component.DefaultExpressionComponent, "Update Default Expression");
        _component.DefaultExpressionComponent!.BlendShapes = new List<BlendShape>(defaultBlendShapes);
    }
}

