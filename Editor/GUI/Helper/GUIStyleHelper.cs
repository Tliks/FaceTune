namespace Aoyon.FaceTune.Gui;

internal static class GUIStyleHelper
{
    private static GUIStyle? _boldFoldoutStyle;
    public static GUIStyle BoldFoldout =>
        _boldFoldoutStyle ??= new GUIStyle(EditorStyles.foldout)
        {
            fontStyle = FontStyle.Bold
        };
}