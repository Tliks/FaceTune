namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(FloatRangeAttribute))]
internal sealed class FloatRangeDrawer : PropertyDrawer
{
    private const float SeparatorWidth = 16f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var value = label == GUIContent.none || string.IsNullOrEmpty(label.text)
            ? position
            : EditorGUI.PrefixLabel(position, label);
        var (minimum, remainder) = value.SplitRatio(.5f);
        var (separator, maximum) = remainder.SplitLeft(SeparatorWidth);
        var x = property.FindPropertyRelative("x");
        var y = property.FindPropertyRelative("y");

        x.floatValue = EditorGUI.FloatField(minimum, x.floatValue);
        EditorGUI.LabelField(separator, "~");
        y.floatValue = EditorGUI.FloatField(maximum, y.floatValue);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => GUIHelper.LineHeight;
}
