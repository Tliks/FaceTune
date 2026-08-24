namespace Aoyon.FaceTune.Gui;

internal static partial class GUIHelper
{
    private const float HelpBoxWidthOffset = 24f;
    private static readonly Dictionary<int, GUIStyle> HelpBoxStyles = new();

    internal static Rect HelpBox(Rect position, string text, MessageType messageType, int? fontSize = null)
    {
        var content = CreateHelpBoxContent(text, messageType);
        var style = GetHelpBoxStyle(fontSize);
        position.height = style.CalcHeight(content, position.width);
        GUI.Label(position, content, style);
        position.NewLine();
        position.SetSingleHeight();
        return position;
    }

    internal static float GetHelpBoxHeight(string text, MessageType messageType, int? fontSize = null)
        => GetHelpBoxHeight(text, Mathf.Max(0f, EditorGUIUtility.currentViewWidth - HelpBoxWidthOffset), messageType, fontSize);

    internal static float GetHelpBoxHeight(string text, float width, MessageType messageType, int? fontSize = null)
        => GetHelpBoxStyle(fontSize).CalcHeight(CreateHelpBoxContent(text, messageType), width);

    private static GUIStyle GetHelpBoxStyle(int? fontSize)
    {
        var size = fontSize ?? EditorStyles.helpBox.fontSize;
        if (!HelpBoxStyles.TryGetValue(size, out var style))
        {
            style = new GUIStyle(EditorStyles.helpBox) { fontSize = size };
            HelpBoxStyles[size] = style;
        }
        return style;
    }

    private static GUIContent CreateHelpBoxContent(string text, MessageType messageType)
    {
        var icon = messageType switch
        {
            MessageType.None => null,
            MessageType.Info => EditorGUIUtility.IconContent("console.infoicon").image,
            MessageType.Warning => EditorGUIUtility.IconContent("console.warnicon").image,
            MessageType.Error => EditorGUIUtility.IconContent("console.erroricon").image,
            _ => null,
        };

        return icon == null ? new GUIContent(text) : new GUIContent(text, icon, text);
    }
}
