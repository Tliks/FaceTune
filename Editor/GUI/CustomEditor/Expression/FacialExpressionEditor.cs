namespace com.aoyon.facetune.ui;

[CanEditMultipleObjects]
[CustomEditor(typeof(FacialExpressionComponent))]
internal class FacialExpressionEditor : FaceTuneCustomEditorBase<FacialExpressionComponent>
{
    private SerializedProperty _allowLipSyncProperty = null!;
    private SerializedProperty _allowEyeBlinkProperty = null!;
    private SerializedProperty _enableBlendingProperty = null!;

    private SerializedProperty _expressionTypeProperty = null!;

    private SerializedProperty _blendShapesProperty = null!;

    private SerializedProperty _clipProperty = null!;
    private SerializedProperty _clipExcludeOptionProperty = null!;

    public override void OnEnable()
    {
        base.OnEnable();
        _blendShapesProperty = serializedObject.FindProperty("_blendShapes");
        _expressionTypeProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.ExpressionType));
        _enableBlendingProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.EnableBlending));
        _allowLipSyncProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.AllowLipSync));
        _allowEyeBlinkProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.AllowEyeBlink));
        _clipProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.Clip));
        _clipExcludeOptionProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.ClipExcludeOption));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_allowEyeBlinkProperty);
        EditorGUILayout.PropertyField(_allowLipSyncProperty);
        EditorGUILayout.PropertyField(_enableBlendingProperty);
        EditorGUILayout.PropertyField(_expressionTypeProperty);
        if (_expressionTypeProperty.enumValueIndex == (int)FacialExpressionType.Manual)
        {
            EditorGUILayout.PropertyField(_blendShapesProperty);
        }
        else if (_expressionTypeProperty.enumValueIndex == (int)FacialExpressionType.FromClip)
        {
            EditorGUILayout.PropertyField(_clipProperty);
            EditorGUILayout.PropertyField(_clipExcludeOptionProperty);
        }
        serializedObject.ApplyModifiedProperties();

        if (_expressionTypeProperty.enumValueIndex == (int)FacialExpressionType.Manual)
        {
            if (GUILayout.Button("Open Editor"))
            {
                OpenFacialShapesEditor();
            }
        }
        else if (_expressionTypeProperty.enumValueIndex == (int)FacialExpressionType.FromClip)
        {
            if (GUILayout.Button("Convert to Manual"))
            {
                ConvertToManual();
            }
        }
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
        Undo.RecordObject(Component, "RecieveEditorResult");
        serializedObject.Update();
        FacialExpressionEditorUtility.UpdateShapes(Component, result.BlendShapes.ToList().AsReadOnly());
        serializedObject.ApplyModifiedProperties();
    }

    [MenuItem($"CONTEXT/{nameof(FacialExpressionComponent)}/ToClip")]
    private static void ToClip(MenuCommand command)
    {
        var component = (command.context as FacialExpressionComponent)!;
        CustomEditorUtility.ToClip(component.gameObject, dfc => (component as IExpressionProvider)!.ToExpression(dfc.GetDefaultExpression(component.gameObject), new NonObserveContext())?.GetBlendShapeSet().BlendShapes);
    }

    private void ConvertToManual()
    {
        if (!CustomEditorUtility.TryGetContext(Component.gameObject, out var context)) return;
        Undo.RecordObject(Component, "ConvertToManual");
        var defaultExpression = context.DEC.GetDefaultExpression(Component.gameObject);
        var shapes = Component.GetBlendShapeSet(defaultExpression.BlendShapeSet, new NonObserveContext());
        if (shapes == null) return;
        FacialExpressionEditorUtility.UpdateShapes(Component, shapes.BlendShapes.ToList());
        Component.ExpressionType = FacialExpressionType.Manual;
    }
}

public class FacialExpressionEditorUtility
{
    public static void UpdateShapes(FacialExpressionComponent component, IReadOnlyList<BlendShape> newShapes)
    {
        var serializedObject = new SerializedObject(component);
        var blendShapesProperty = serializedObject.FindProperty("_blendShapes");
        UpdateShapes(blendShapesProperty, newShapes);
        serializedObject.ApplyModifiedProperties();
    }

    internal static void UpdateShapes(SerializedProperty blendShapesProperty, IReadOnlyList<BlendShape> newShapes)
    {
        blendShapesProperty.ClearArray();

        for (int i = 0; i < newShapes.Count; i++)
        {
            blendShapesProperty.InsertArrayElementAtIndex(i);
            SerializedProperty elementProperty = blendShapesProperty.GetArrayElementAtIndex(i);
            BlendShape currentShape = newShapes[i];

            SerializedProperty nameProp = elementProperty.FindPropertyRelative(nameof(BlendShape.Name));
            nameProp.stringValue = currentShape.Name;

            SerializedProperty weightProp = elementProperty.FindPropertyRelative(nameof(BlendShape.Weight));
            weightProp.floatValue = currentShape.Weight;
        }
    }
}
