namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(ConditionComponent))]
internal class ConditionEditor : FaceTuneIMGUIEditorBase<ConditionComponent>
{
    protected override void OnInnerInspectorGUI()
    {
        DrawDefaultInspector(true);
    }
}
