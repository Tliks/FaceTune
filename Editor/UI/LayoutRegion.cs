namespace Aoyon.FaceTune.Gui;

internal static class LayoutRegion
{
    public static LayoutRegionScope Begin(GUIStyle style) => new(style);
}

internal readonly ref struct LayoutRegionScope
{
    public LayoutRegionScope(GUIStyle style)
    {
        EditorGUILayout.BeginVertical(style);
    }

    public void Dispose()
    {
        EditorGUILayout.EndVertical();
    }
}
