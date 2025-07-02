namespace aoyon.facetune.ui;

[CanEditMultipleObjects]
[CustomEditor(typeof(AdvancedEyeBlinkComponent))]
internal class AdvancedEyeBlinkEditor : FaceTuneCustomEditorBase<AdvancedEyeBlinkComponent>
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Open Editor"))
        {
            var getProperty = (SerializedObject so) => so.FindProperty(nameof(AdvancedEyeBlinkComponent.AdvancedEyeBlinkSettings)).FindPropertyRelative(AdvancedEyeBlinkSettings.CancelerBlendShapeNamesPropName);
            CustomEditorUtility.OpenEditorAndApplyBlendShapeNames(Component, getProperty);
        }
    }
}