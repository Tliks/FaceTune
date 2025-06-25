namespace com.aoyon.facetune.ui;

[CanEditMultipleObjects]
[CustomEditor(typeof(MergeExpressionComponent))]
internal class MergeExpressionEditor : FaceTuneCustomEditorBase<MergeExpressionComponent>
{
    private SerializedProperty _expressionSettingsProperty = null!;
    private SerializedProperty _facialSettingsProperty = null!;
    private bool _hasFacialExpression = false;
    public override void OnEnable()
    {
        base.OnEnable();
        _expressionSettingsProperty = serializedObject.FindProperty(nameof(MergeExpressionComponent.ExpressionSettings));
        _facialSettingsProperty = serializedObject.FindProperty(nameof(MergeExpressionComponent.FacialSettings));
        _hasFacialExpression = Component.GetComponent<FacialExpressionComponent>() != null;
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(_expressionSettingsProperty);
        GUI.enabled = _hasFacialExpression;
        EditorGUILayout.PropertyField(_facialSettingsProperty);
        GUI.enabled = true;
        serializedObject.ApplyModifiedProperties();
    }
}