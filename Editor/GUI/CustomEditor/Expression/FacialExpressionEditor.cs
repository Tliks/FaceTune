namespace com.aoyon.facetune.ui;

[CanEditMultipleObjects]
[CustomEditor(typeof(FacialExpressionComponent))]
internal class FacialExpressionEditor : FaceTuneCustomEditorBase<FacialExpressionComponent>
{
    private SerializedProperty _blendShapesProperty = null!;
    private SerializedProperty _enableBlendingProperty = null!;
    private SerializedProperty _allowLipSyncProperty = null!;
    private SerializedProperty _allowEyeBlinkProperty = null!;

    public override void OnEnable()
    {
        base.OnEnable();
        _blendShapesProperty = serializedObject.FindProperty("_blendShapes");
        _enableBlendingProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.EnableBlending));
        _allowLipSyncProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.AllowLipSync));
        _allowEyeBlinkProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.AllowEyeBlink));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_allowEyeBlinkProperty);
        EditorGUILayout.PropertyField(_allowLipSyncProperty);
        EditorGUILayout.PropertyField(_enableBlendingProperty);
        EditorGUILayout.PropertyField(_blendShapesProperty);

        serializedObject.ApplyModifiedProperties();

        if (GUILayout.Button("Open Editor"))
        {
            OpenFacialShapesEditor();
        }
    }

    private void OpenFacialShapesEditor()
    {
        var mainComponent = Component.GetComponentInParentNullable<FaceTuneComponent>();
        if (mainComponent == null) return;
        if (!mainComponent.TryGetSessionContext(out var context)) return;

        var window = FacialShapesEditor.OpenEditor(context.FaceRenderer, context.FaceMesh, context.DefaultBlendShapes, new(Component.BlendShapes));
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
