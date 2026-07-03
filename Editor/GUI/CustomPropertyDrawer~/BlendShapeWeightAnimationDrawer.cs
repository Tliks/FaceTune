namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(BlendShapeWeightAnimation))]
internal class BlendShapeWeightAnimationDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var nameProp = property.FindPropertyRelative(BlendShapeWeightAnimation.NamePropName);
        var curveProp = property.FindPropertyRelative(BlendShapeWeightAnimation.CurvePropName);

        float totalWidth = position.width;
        float labelWidth = totalWidth * 0.3f;
        float fieldWidth = totalWidth * 0.7f;

        Rect nameRect = new Rect(position.x, position.y, labelWidth, position.height);
        Rect curveRect = new Rect(position.x + labelWidth, position.y, fieldWidth, position.height);

        EditorGUI.PropertyField(nameRect, nameProp, GUIContent.none);
        EditorGUI.PropertyField(curveRect, curveProp, GUIContent.none);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}