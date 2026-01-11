namespace Aoyon.FaceTune.Gui;

internal static class GUIStyleHelper
{
    private static GUIStyle? _boldFoldoutStyle;
    public static GUIStyle BoldFoldout =>
        _boldFoldoutStyle ??= new GUIStyle(EditorStyles.foldout)
        {
            fontStyle = FontStyle.Bold
        };
    
    private static GUIStyle? _IconLabelStyle;
    public static GUIStyle IconLabel =>
        _IconLabelStyle ??= new GUIStyle(EditorStyles.label)
        {
            margin = new RectOffset(0, 0, 2, 0),
            padding = new RectOffset(0, 0, 0, 0)
        };
}