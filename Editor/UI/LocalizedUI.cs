namespace Aoyon.FaceTune.Gui;

/// <summary>Stateless localized wrappers around Unity's IMGUI API.</summary>
internal static class LocalizedUI
{
    public static void PropertyField(SerializedProperty property, string key, bool includeChildren = true)
        => EditorGUILayout.PropertyField(property, key.LG(), includeChildren);

    public static void PropertyField(Rect position, SerializedProperty property, string key, bool includeChildren = true)
        => EditorGUI.PropertyField(position, property, key.LG(), includeChildren);

    public static int Popup(Rect position, int selectedIndex, string? labelKey, IEnumerable<string> optionKeys)
        => EditorGUI.Popup(
            position,
            labelKey == null ? GUIContent.none : labelKey.LG(),
            selectedIndex,
            optionKeys.Select(key => key.LG()).ToArray());

    public static int Popup(int selectedIndex, string? labelKey, IEnumerable<string> optionKeys, params GUILayoutOption[] options)
        => EditorGUILayout.Popup(
            labelKey == null ? GUIContent.none : labelKey.LG(),
            selectedIndex,
            optionKeys.Select(key => key.LG()).ToArray(),
            options);

    public static void EnumPopup(
        Rect position,
        SerializedProperty property,
        string labelKey,
        IEnumerable<string> optionKeys)
    {
        var label = string.IsNullOrEmpty(labelKey) ? GUIContent.none : labelKey.LG();
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var previousMixedValue = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        var next = Popup(position, property.enumValueIndex, string.IsNullOrEmpty(labelKey) ? null : labelKey, optionKeys);
        if (next != property.enumValueIndex) property.enumValueIndex = next;
        EditorGUI.showMixedValue = previousMixedValue;
    }
}
