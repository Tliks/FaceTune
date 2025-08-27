namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(PatternComponent))]
internal class PatternEditor : FaceTuneIMGUIEditorBase<PatternComponent>
{
    protected override void OnInnerInspectorGUI()
    {
        DrawDefaultInspector(true);
        EditorGUILayout.HelpBox(Localization.S("PatternComponent:Description"), MessageType.Info);
    }
}
