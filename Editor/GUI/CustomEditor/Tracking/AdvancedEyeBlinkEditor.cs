namespace aoyon.facetune.ui;

[CanEditMultipleObjects]
[CustomEditor(typeof(AdvancedEyeBlinkComponent))]
internal class AdvancedEyeBlinkEditor : FaceTuneCustomEditorBase<AdvancedEyeBlinkComponent>
{
    private SerializedProperty _advancedEyeBlinkSettingsProperty = null!;

    public override void OnEnable()
    {
        base.OnEnable();
        _advancedEyeBlinkSettingsProperty = serializedObject.FindProperty(nameof(AdvancedEyeBlinkComponent.AdvancedEyeBlinkSettings));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(_advancedEyeBlinkSettingsProperty);
        serializedObject.ApplyModifiedProperties();

        if (GUILayout.Button("Open Editor"))
        {
            CustomEditorUtility.OpenEditor(Component.gameObject, RecieveEditorResult);
        }
    }

    private void RecieveEditorResult(BlendShapeSet result)
    {
        var so = new SerializedObject(Component);
        so.Update();
        var animationProperty = so.FindProperty(nameof(AdvancedEyeBlinkComponent.AdvancedEyeBlinkSettings)).FindPropertyRelative(AdvancedEyeBlinkSettings.CancelerBlendShapeNamesPropName);
        foreach (var shape in result.BlendShapes)
        {
            animationProperty.arraySize++;
            var element = animationProperty.GetArrayElementAtIndex(animationProperty.arraySize - 1);
            element.stringValue = shape.Name;
        }
        so.ApplyModifiedProperties();
    }
}