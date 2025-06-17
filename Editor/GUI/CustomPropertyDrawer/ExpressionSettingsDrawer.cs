namespace com.aoyon.facetune.ui;

internal static class ExpressionSettingsDrawer
{
    private static readonly GUIContent LoopTimeContent = new("Loop");
    private static readonly GUIContent MotionTimeParameterNameContent = new("Motion Time Parameter Name");

    public static void Draw(SerializedProperty property)
    {
        var prop = property.FindPropertyRelative(nameof(ExpressionSettings.LoopTime));
        EditorGUILayout.PropertyField(prop, LoopTimeContent);
        if (!prop.boolValue)
        {
            DrawMotionTimeParameterName(property);
        }
    }

    public static void DrawMotionTimeParameterName(SerializedProperty property)
    {
        var prop = property.FindPropertyRelative(nameof(ExpressionSettings.MotionTimeParameterName));
        EditorGUILayout.PropertyField(prop, MotionTimeParameterNameContent);
    }
}