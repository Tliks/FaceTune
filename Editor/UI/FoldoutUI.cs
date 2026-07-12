namespace Aoyon.FaceTune.Gui;

/// <summary>Foldouts whose drawing and hit area stay inside the supplied rectangle.</summary>
internal static class FoldoutUI
{
    public static bool Draw(Rect position, bool expanded, GUIContent label, bool toggleOnLabelClick = true)
    {
        if (Event.current.type == EventType.Repaint)
            EditorStyles.foldout.Draw(position, label, false, false, expanded, false);

        var arrowWidth = EditorStyles.foldout.CalcSize(GUIContent.none).x;
        var hitRect = position;
        if (!toggleOnLabelClick) hitRect.width = arrowWidth;

        var current = Event.current;
        if (current.type == EventType.MouseDown && current.button == 0 && hitRect.Contains(current.mousePosition))
        {
            expanded = !expanded;
            current.Use();
            GUI.changed = true;
        }

        return expanded;
    }

    public static bool Draw(Rect position, SerializedProperty property, GUIContent label, bool toggleOnLabelClick = true)
    {
        property.isExpanded = Draw(position, property.isExpanded, label, toggleOnLabelClick);
        return property.isExpanded;
    }
}
