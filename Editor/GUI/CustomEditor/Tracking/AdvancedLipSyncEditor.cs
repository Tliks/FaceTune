namespace aoyon.facetune.ui;

[CanEditMultipleObjects]
[CustomEditor(typeof(AdvancedLipSyncComponent))]
internal class AdvancedLipSyncEditor : FaceTuneCustomEditorBase<AdvancedLipSyncComponent>
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Open Editor for Canceler BlendShape Names"))
        {
            var defaultOverride = new BlendShapeSet(Component.AdvancedLipSyncSettings.CancelerBlendShapeNames.Select(x => new BlendShape(x, 0.0f)));
            var getProperty = (SerializedObject so) => so.FindProperty(nameof(AdvancedLipSyncComponent.AdvancedLipSyncSettings)).FindPropertyRelative(AdvancedLipSyncSettings.CancelerBlendShapeNamesPropName);
            CustomEditorUtility.OpenEditorAndApplyBlendShapeNames(Component, defaultOverride, getProperty);
        }
    }
}