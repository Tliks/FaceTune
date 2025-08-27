namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(BlendShapeWeight))]
internal class BlendShapeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var propName = property.FindPropertyRelative(BlendShapeWeight.NamePropName);
        var propWeight = property.FindPropertyRelative(BlendShapeWeight.WeightPropName);

        position.height = EditorGUIUtility.singleLineHeight;

        LocalizedUI.PropertyField(position, propName, "BlendShapeWeight:Name");
        position.y += EditorGUIUtility.singleLineHeight;
        LocalizedUI.PropertyField(position, propWeight, "BlendShapeWeight:Weight");

        EditorGUI.EndProperty();
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 2;
    }   
}
