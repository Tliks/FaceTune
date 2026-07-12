namespace Aoyon.FaceTune.Gui;

internal static class FaceTuneDrawerUtility
{
    public static float Space => EditorGUIUtility.standardVerticalSpacing;
    public static float Line => EditorGUIUtility.singleLineHeight;

    public static void Draw(ref Rect position, SerializedProperty property, string key, bool children = true)
    {
        position.height = EditorGUI.GetPropertyHeight(property, children);
        using (new EditorGUI.PropertyScope(position, key.LG(), property))
            LocalizedUI.PropertyField(position, property, key, children);
        position.NewLine();
    }

    public static float Height(SerializedProperty property, bool children = true)
        => EditorGUI.GetPropertyHeight(property, children) + Space;

    public static void Enum(ref Rect position, SerializedProperty property, string labelKey, string typeName)
    {
        position.height = Line;
        Enum(position, property, labelKey, typeName);
        position.NewLine();
    }

    public static void Enum(Rect position, SerializedProperty property, string labelKey, string typeName)
    {
        var optionPrefix = char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
        var optionKeys = property.enumNames.Select(name => $"{optionPrefix}.option.{char.ToLowerInvariant(name[0]) + name.Substring(1)}");
        LocalizedUI.EnumPopup(position, property, labelKey, optionKeys);
    }
}
