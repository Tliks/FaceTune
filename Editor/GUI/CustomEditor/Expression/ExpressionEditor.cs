namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(ExpressionComponent))]
internal class ExpressionEditor : FaceTuneIMGUIEditorBase<ExpressionComponent>
{
    private SerializedProperty _expressionSettingsProperty = null!;
    private SerializedProperty _facialSettingsProperty = null!;
    private SerializedProperty _enableRealTimePreviewProperty = null!;
    private bool _showExpressionSettings = false;

    private FacialStyleComponent? _facialStyleComponent;
    private ConditionComponent[] _conditionComponents = null!;
    private ExpressionDataComponent[] _expressionDataComponents = null!;
    private bool _showConnectedComponents = true;

    public override void OnEnable()
    {
        base.OnEnable();

        _expressionSettingsProperty = serializedObject.FindProperty(nameof(ExpressionComponent.ExpressionSettings));
        _facialSettingsProperty = serializedObject.FindProperty(nameof(ExpressionComponent.FacialSettings));
        _enableRealTimePreviewProperty = serializedObject.FindProperty(nameof(ExpressionComponent.EnableRealTimePreview));

        RefreshConnectedComponents();
    }

    private void RefreshConnectedComponents()
    {
        _facialStyleComponent = Component.GetComponentInParent<FacialStyleComponent>();
        _conditionComponents = Component.GetComponentsInParent<ConditionComponent>();
        _expressionDataComponents = Component.GetComponentsInChildren<ExpressionDataComponent>();
    }

    protected override void OnInnerInspectorGUI()
    {
        DrawProperties();
        EditorGUILayout.Space();
        DrawConnectedComponents();
    }

    private void DrawProperties()
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

    private void DrawConnectedComponents()
    {
        _showConnectedComponents = EditorGUILayout.Foldout(_showConnectedComponents, "ExpressionComponent:label:ConnectedComponents".LG(), true, GUIStyleHelper.BoldFoldout);

        if (!_showConnectedComponents) return;

        EditorGUILayout.LabelField("ExpressionComponent:label:ConnectedComponents:ConditionComponent".LG());
        GUI.enabled = false;
        foreach (var conditionComponent in _conditionComponents)
        {
            EditorGUILayout.ObjectField(conditionComponent, typeof(ConditionComponent), true);
        }
        GUI.enabled = true;

        EditorGUILayout.LabelField("ExpressionComponent:label:ConnectedComponents:FacialStyleComponent".LG());
        GUI.enabled = false;
        EditorGUILayout.ObjectField(_facialStyleComponent, typeof(FacialStyleComponent), true);
        GUI.enabled = true;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("ExpressionComponent:label:ConnectedComponents:ExpressionDataComponent".LG());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("ExpressionComponent:button:ConnectedComponents:ExpressionDataComponent:Add".LG()))
            {
                AddExpressionDataComponent();
            }
        }
        foreach (var expressionDataComponent in _expressionDataComponents)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = false;
                EditorGUILayout.ObjectField(expressionDataComponent, typeof(ExpressionDataComponent), true);
                GUI.enabled = true;
                if (GUILayout.Button("ExpressionComponent:button:ConnectedComponents:ExpressionDataComponent:OpenEditor".LG(), GUILayout.Width(100)))
                {
                    OpenExpressionDataComponentEditor(expressionDataComponent);
                }
            }
        }
    }

    private void AddExpressionDataComponent()
    {
        var newExpresionGameObject = new GameObject("New Expression Data");
        newExpresionGameObject.transform.SetParent(Component.transform);
        newExpresionGameObject.AddComponent<ExpressionDataComponent>();
        Undo.RegisterCreatedObjectUndo(newExpresionGameObject, "Add ExpressionDataComponent");
        Selection.activeGameObject = newExpresionGameObject;
        EditorGUIUtility.PingObject(newExpresionGameObject);
        RefreshConnectedComponents();
    }

    private void OpenExpressionDataComponentEditor(ExpressionDataComponent expressionDataComponent)
    {
        ExpressionDataEditor.OpenEditor(expressionDataComponent);
    }
}