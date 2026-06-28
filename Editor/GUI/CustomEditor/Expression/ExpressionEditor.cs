namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(FaceTuneComponent))]
internal class ExpressionEditor : FaceTuneIMGUIEditorBase<FaceTuneComponent>
{
    private SerializedProperty _expressionSettingsProperty = null!;
    private SerializedProperty _facialSettingsProperty = null!;
    private SerializedProperty _enableRealTimePreviewProperty = null!;

    private bool _showExpressionSettings = false;

    public override void OnEnable()
    {
        base.OnEnable();
        _expressionSettingsProperty = serializedObject.FindProperty(nameof(FaceTuneComponent.ExpressionSettings));
        _facialSettingsProperty = serializedObject.FindProperty(nameof(FaceTuneComponent.FacialSettings));
        _enableRealTimePreviewProperty = serializedObject.FindProperty(nameof(FaceTuneComponent.EnableRealTimePreview));
    }

    protected override void OnInnerInspectorGUI()
    {
        EditorGUILayout.LabelField("FacialSettings".LG(), EditorStyles.boldLabel);

        LocalizedPropertyField(_facialSettingsProperty);
        LocalizedPropertyField(_enableRealTimePreviewProperty);

        EditorGUILayout.Space();

        _showExpressionSettings = EditorGUILayout.Foldout(
            _showExpressionSettings,
            "ExpressionSettings".LG(),
            true,
            GUIStyleHelper.BoldFoldout
        );

        EditorGUILayout.Space();

        if (_showExpressionSettings)
        {
            LocalizedPropertyField(_expressionSettingsProperty);
        }
    }
}