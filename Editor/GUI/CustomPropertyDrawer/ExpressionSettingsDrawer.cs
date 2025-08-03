namespace aoyon.facetune.gui;

internal static class ExpressionSettingsDrawer
{
    public static void Draw(SerializedProperty property)
    {
        var prop = property.FindPropertyRelative(ExpressionSettings.LoopTimePropName);
        EditorGUILayout.PropertyField(prop);
        if (!prop.boolValue)
        {
            DrawMotionTimeParameterName(property);
        }
    }

    public static void DrawMotionTimeParameterName(SerializedProperty property)
    {
        var prop = property.FindPropertyRelative(ExpressionSettings.MotionTimeParameterNamePropName);
        EditorGUILayout.PropertyField(prop);
    }
}