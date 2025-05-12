namespace com.aoyon.facetune.ui;

[CanEditMultipleObjects]
[CustomEditor(typeof(FacialExpressionComponent))]
internal class FacialExpressionEditor : Editor
{
    private FacialExpressionComponent _component = null!;
    private SerializedProperty _blendShapesProperty = null!;
    private SerializedProperty _addDefaultProperty = null!;
    private SerializedProperty _allowLipSyncProperty = null!;
    private SerializedProperty _allowEyeBlinkProperty = null!;

    void OnEnable()
    {
        _component = (target as FacialExpressionComponent)!;
        _blendShapesProperty = serializedObject.FindProperty("_blendShapes");
        _addDefaultProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.AddDefault));
        _allowLipSyncProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.AllowLipSync));
        _allowEyeBlinkProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.AllowEyeBlink));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_allowEyeBlinkProperty);
        EditorGUILayout.PropertyField(_allowLipSyncProperty);
        EditorGUILayout.PropertyField(_addDefaultProperty);
        EditorGUILayout.PropertyField(_blendShapesProperty);

        serializedObject.ApplyModifiedProperties();

        if (GUILayout.Button("Open Editor"))
        {
            OpenFacialShapesEditor();
        }
    }

    private void OpenFacialShapesEditor()
    {
        var mainComponent = _component.GetComponentInParentNullable<FaceTuneComponent>();
        if (mainComponent == null) return;
        if (!mainComponent.TryGetSessionContext(out var context)) return;

        var window = FacialShapesEditor.OpenEditor(context.FaceRenderer, context.FaceMesh, context.DefaultBlendShapes, new(_component.BlendShapes));
        window.RegisterApplyCallback(RecieveEditorResult);
    }

    private void RecieveEditorResult(BlendShapeSet result)
    {
        Undo.RecordObject(_component, "RecieveEditorResult");
        _component.BlendShapes = result.BlendShapes.ToList(); // 同じKeyの場合は上書きにした方が良い
    }
}
