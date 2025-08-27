namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(ExpressionComponent))]
internal class ExpressionEditor : FaceTuneIMGUIEditorBase<ExpressionComponent>
{
    private SerializedProperty _expressionSettingsProperty = null!;
    private SerializedProperty _facialSettingsProperty = null!;
    private SerializedProperty _enableRealTimePreviewProperty = null!;

    private bool _showExpressionSettings = false;

    public override void OnEnable()
    {
        base.OnEnable();
        _expressionSettingsProperty = serializedObject.FindProperty(nameof(ExpressionComponent.ExpressionSettings));
        _facialSettingsProperty = serializedObject.FindProperty(nameof(ExpressionComponent.FacialSettings));
        _enableRealTimePreviewProperty = serializedObject.FindProperty(nameof(ExpressionComponent.EnableRealTimePreview));
    }

    protected override void OnInnerInspectorGUI()
    {
        EditorGUILayout.LabelField(Localization.S("ExpressionComponent:FacialSettings"), EditorStyles.boldLabel);

        LocalizedPropertyDrawer(_facialSettingsProperty);
        LocalizedPropertyField(_enableRealTimePreviewProperty);

        EditorGUILayout.Space();

        _showExpressionSettings = EditorGUILayout.Foldout(
            _showExpressionSettings,
            Localization.S($"{ComponentName}:ExpressionSettings"),
            true,
            GUIStyleHelper.BoldFoldout
        );

        EditorGUILayout.Space();

        if (_showExpressionSettings)
        {
            LocalizedPropertyDrawer(_expressionSettingsProperty);
        }
    }
}