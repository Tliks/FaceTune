using Aoyon.FaceTune.Gui.ShapesEditor;

namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(AdvancedLipSyncComponent))]
internal class AdvancedLipSyncEditor : FaceTuneIMGUIEditorBase<AdvancedLipSyncComponent>
{
    protected override void OnInnerInspectorGUI()
    {
        DrawDefaultInspector(false);
        if (GUILayout.Button("Open Editor for Canceler BlendShape Names"))
        {
            var defaultOverride = new BlendShapeWeightSet(Component.AdvancedLipSyncSettings.CancelerBlendShapeNames.Select(x => new BlendShapeWeight(x, 0.0f)));
            CustomEditorUtility.OpenEditor(Component.gameObject, new AdvancedLipSyncTargeting(){ Target = Component }, defaultOverride);
        }
    }
}