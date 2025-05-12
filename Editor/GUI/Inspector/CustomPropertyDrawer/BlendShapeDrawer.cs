namespace com.aoyon.facetune.ui;

[CustomPropertyDrawer(typeof(BlendShape))]
internal class BlendShapeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var propName = property.FindPropertyRelative(nameof(BlendShape.Name));
        var propWeight = property.FindPropertyRelative(nameof(BlendShape.Weight));

        if (propName == null || propWeight == null) return;
        
        position.height = EditorGUIUtility.singleLineHeight;

        EditorGUI.PropertyField(position, propName, new GUIContent("Name"));
        position.y += EditorGUIUtility.singleLineHeight;
        EditorGUI.Slider(position, propWeight, 0f, 100f, new GUIContent("Weight"));
        position.y += EditorGUIUtility.singleLineHeight;

        EditorGUI.EndProperty();
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 2;
    }   
}
