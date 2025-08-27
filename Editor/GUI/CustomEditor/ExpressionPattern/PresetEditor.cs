namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(PresetComponent))]
internal class PresetEditor : FaceTuneIMGUIEditorBase<PresetComponent>
{
    protected override void OnInnerInspectorGUI()
    {
        DrawDefaultInspector(true);
    }
}
