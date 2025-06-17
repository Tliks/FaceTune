namespace com.aoyon.facetune.ui;

internal static class ExpressionSettingsDrawer
{
    private static readonly GUIContent IsLoopContent = new("Is Loop");
    private static readonly GUIContent MotionTimeParameterNameContent = new("Motion Time Parameter Name");

    public static void Draw(SerializedProperty property)
    {
        var prop = property.FindPropertyRelative(nameof(ExpressionSettings.IsLoop));
        EditorGUILayout.PropertyField(prop, IsLoopContent);
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