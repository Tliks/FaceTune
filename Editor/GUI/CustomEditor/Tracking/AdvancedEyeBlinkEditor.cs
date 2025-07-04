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
            var defaultOverride = new BlendShapeSet(Component.AdvancedEyeBlinkSettings.CancelerBlendShapeNames.Select(x => new BlendShape(x, 0.0f)));
            var getProperty = (SerializedObject so) => so.FindProperty(nameof(AdvancedEyeBlinkComponent.AdvancedEyeBlinkSettings)).FindPropertyRelative(AdvancedEyeBlinkSettings.CancelerBlendShapeNamesPropName);
            CustomEditorUtility.OpenEditorAndApplyBlendShapeNames(Component, defaultOverride, getProperty);
        }
    }
}