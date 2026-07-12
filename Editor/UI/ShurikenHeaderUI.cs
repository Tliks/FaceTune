namespace Aoyon.FaceTune.Gui;

internal static class ShurikenHeaderUI
{
    private static GUIStyle? _style;
    private static GUIStyle Style => _style ??= new GUIStyle("ShurikenModuleTitle")
    {
        font = EditorStyles.label.font,
        border = new RectOffset(15, 7, 4, 4),
        fixedHeight = 22f,
        contentOffset = new Vector2(20f, -2f),
        fontSize = 12
    };

    public static bool DrawFoldoutLayout(bool expanded, GUIContent label)
    {
        var position = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, 20f, Style));
        GUI.Box(position, label, Style);
        return HandleFoldout(position, expanded);
    }

    public static bool DrawToggleFoldoutLayout(SerializedProperty enabled, bool expanded, GUIContent label)
    {
        var position = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, 20f, Style));
        GUI.Box(position, GUIContent.none, Style);

        var toggleRect = new Rect(
            position.x + EditorStyles.foldout.CalcSize(GUIContent.none).x + 4f,
            position.y,
            EditorStyles.toggle.CalcSize(GUIContent.none).x,
            position.height);
        var labelRect = new Rect(toggleRect.xMax, position.y, Mathf.Max(0f, position.xMax - toggleRect.xMax), position.height);

        using (new EditorGUI.PropertyScope(toggleRect, GUIContent.none, enabled))
        {
            var previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = enabled.hasMultipleDifferentValues;
            enabled.boolValue = EditorGUI.Toggle(toggleRect, enabled.boolValue);
            EditorGUI.showMixedValue = previousMixed;
        }
        EditorGUI.LabelField(labelRect, label);

        return HandleFoldout(position, expanded, toggleRect);
    }

    private static bool HandleFoldout(Rect position, bool expanded, Rect? excluded = null)
    {
        var arrow = new Rect(position.x + 4f, position.y + 2f, 13f, 13f);
        if (Event.current.type == EventType.Repaint)
            EditorStyles.foldout.Draw(arrow, false, false, expanded, false);

        var current = Event.current;
        if (current.type == EventType.MouseDown && current.button == 0
            && position.Contains(current.mousePosition)
            && excluded?.Contains(current.mousePosition) != true)
        {
            expanded = !expanded;
            current.Use();
        }
        return expanded;
    }
}
