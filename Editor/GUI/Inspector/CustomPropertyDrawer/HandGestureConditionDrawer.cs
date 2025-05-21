namespace com.aoyon.facetune.ui;

[CustomPropertyDrawer(typeof(HandGestureCondition))]
internal class HandGestureConditionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var handProp = property.FindPropertyRelative(nameof(HandGestureCondition.Hand));
        var comparisonTypeProp = property.FindPropertyRelative(nameof(HandGestureCondition.ComparisonType));
        var handGestureProp = property.FindPropertyRelative(nameof(HandGestureCondition.HandGesture));
    
        Rect currentPosition = position;
        currentPosition.height = EditorGUIUtility.singleLineHeight;

        EditorGUI.PropertyField(currentPosition, handProp, new GUIContent("Hand"));
        currentPosition.y += EditorGUIUtility.singleLineHeight;
        
        EditorGUI.PropertyField(currentPosition, comparisonTypeProp, new GUIContent("Comparison"));
        currentPosition.y += EditorGUIUtility.singleLineHeight;

        EditorGUI.PropertyField(currentPosition, handGestureProp, new GUIContent("Hand Gesture"));
        // currentPosition.y += EditorGUIUtility.singleLineHeight; // 最後の要素の場合は不要

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 3;
    }
}