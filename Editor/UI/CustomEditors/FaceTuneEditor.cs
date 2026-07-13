namespace Aoyon.FaceTune.Gui;

internal abstract class FaceTuneEditor<T> : Editor where T : FaceTuneTagComponent
{
    protected T Component => (T)target;
    protected virtual bool ShowLanguageSwitcher => false;

    public sealed override void OnInspectorGUI()
    {
        serializedObject.UpdateIfRequiredOrScript();

        var height = GetInspectorHeight();
        var position = EditorGUILayout.GetControlRect(false, height, GUIStyle.none);
        DrawInspector(position);

        serializedObject.ApplyModifiedProperties();
        if (ShowLanguageSwitcher)
        {
            EditorGUILayout.Space();
            Localization.DrawLanguageSwitcher();
        }
    }

    protected virtual float GetInspectorHeight()
    {
        var height = 0f;
        var iterator = serializedObject.GetIterator();
        var enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyPath == "m_Script") continue;
            height += GUIHelper.PropertyHeight(iterator);
        }
        return Mathf.Max(0f, height - GUIHelper.VerticalSpacing);
    }

    protected virtual void DrawInspector(Rect position)
    {
        var iterator = serializedObject.GetIterator();
        var enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyPath == "m_Script") continue;
            var property = iterator.Copy();
            position.height = EditorGUI.GetPropertyHeight(property, true);
            EditorGUI.PropertyField(position, property, true);
            position.NewLine();
        }
    }

    protected float GetPropertyHeight(string propertyName, bool includeChildren = true)
    {
        var property = serializedObject.FindProperty(propertyName);
        return property == null ? 0f : GUIHelper.PropertyHeight(property, includeChildren);
    }

    protected void DrawProperty(ref Rect position, string propertyName, bool includeChildren = true)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property == null) return;

        position.height = EditorGUI.GetPropertyHeight(property, includeChildren);
        EditorGUI.PropertyField(position, property, includeChildren);
        position.NewLine();
    }

    protected static bool IsMode(SerializedProperty mode, int value)
        => !mode.hasMultipleDifferentValues && mode.enumValueIndex == value;
}
