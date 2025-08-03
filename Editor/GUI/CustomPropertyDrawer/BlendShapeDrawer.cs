namespace aoyon.facetune.gui;

[CustomPropertyDrawer(typeof(BlendShape))]
internal class BlendShapeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var propName = property.FindPropertyRelative(BlendShape.NamePropName);
        var propWeight = property.FindPropertyRelative(BlendShape.WeightPropName);

        if (propName == null || propWeight == null) return;
        
        position.height = EditorGUIUtility.singleLineHeight;

        EditorGUI.PropertyField(position, propName);
        position.y += EditorGUIUtility.singleLineHeight;
        EditorGUI.Slider(position, propWeight, 0f, 100f);

        EditorGUI.EndProperty();
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 2;
    }   
}
