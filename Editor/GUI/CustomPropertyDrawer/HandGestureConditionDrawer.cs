namespace com.aoyon.facetune.ui;

[CustomPropertyDrawer(typeof(HandGestureCondition))]
internal class HandGestureConditionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var handProp = property.FindPropertyRelative(HandGestureCondition.HandPropName);
        var isEqualProp = property.FindPropertyRelative(HandGestureCondition.IsEqualPropName);
        var handGestureProp = property.FindPropertyRelative(HandGestureCondition.HandGesturePropName);
    
        Rect currentPosition = position;
        currentPosition.height = EditorGUIUtility.singleLineHeight;

        EditorGUI.PropertyField(currentPosition, handProp);
        currentPosition.y += EditorGUIUtility.singleLineHeight;
        
        EditorGUI.PropertyField(currentPosition, isEqualProp);
        currentPosition.y += EditorGUIUtility.singleLineHeight;

        EditorGUI.PropertyField(currentPosition, handGestureProp);
        // currentPosition.y += EditorGUIUtility.singleLineHeight; // 最後の要素の場合は不要

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 3;
    }
}