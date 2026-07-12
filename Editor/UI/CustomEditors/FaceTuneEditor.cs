namespace Aoyon.FaceTune.Gui;

/// <summary>Minimal base for FaceTune inspectors.</summary>
internal abstract class FaceTuneEditor<T> : Editor where T : FaceTuneTagComponent
{
    protected T Component => (T)target;

    public sealed override void OnInspectorGUI()
    {
        Localization.DrawLanguageSwitcher();
        EditorGUILayout.Space();
        serializedObject.UpdateIfRequiredOrScript();
        DrawInspector();
        serializedObject.ApplyModifiedProperties();
    }

    protected virtual void DrawInspector() => DrawDefaultInspector();

    protected void DrawProperty(string propertyName, bool includeChildren = true)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null) EditorGUILayout.PropertyField(property, includeChildren);
    }

    protected static bool IsMode(SerializedProperty mode, int value)
        => !mode.hasMultipleDifferentValues && mode.enumValueIndex == value;
}
