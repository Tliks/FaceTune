namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(FloatRange))]
internal sealed class FloatRangeDrawer : PropertyDrawer
{
    private const float SeparatorWidth = 16f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var value = EditorGUI.PrefixLabel(position, label);
        var (min, remainder) = value.SplitRatio(.5f);
        var (separator, max) = remainder.SplitLeft(SeparatorWidth);
        var minProperty = property.FindPropertyRelative("min");
        var maxProperty = property.FindPropertyRelative("max");

        minProperty.floatValue = EditorGUI.FloatField(min, minProperty.floatValue);
        EditorGUI.LabelField(separator, "~");
        maxProperty.floatValue = EditorGUI.FloatField(max, maxProperty.floatValue);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => GUIHelper.LineHeight;
}
