namespace com.aoyon.facetune.ui;

[CustomPropertyDrawer(typeof(HandGestureCondition))]
internal class HandGestureConditionDrawer : PropertyDrawer
{
    private const string HandPropName = "_hand";
    private const string ComparisonTypePropName = "_comparisonType";
    private const string HandGesturePropName = "_handGesture";

    private static readonly GUIContent HandContent = new("Hand");
    private static readonly GUIContent ComparisonContent = new("Comparison");
    private static readonly GUIContent HandGestureContent = new("Hand Gesture");

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var handProp = property.FindPropertyRelative(HandPropName);
        var comparisonTypeProp = property.FindPropertyRelative(ComparisonTypePropName);
        var handGestureProp = property.FindPropertyRelative(HandGesturePropName);
    
        Rect currentPosition = position;
        currentPosition.height = EditorGUIUtility.singleLineHeight;

        EditorGUI.PropertyField(currentPosition, handProp, HandContent);
        currentPosition.y += EditorGUIUtility.singleLineHeight;
        
        EditorGUI.PropertyField(currentPosition, comparisonTypeProp, ComparisonContent);
        currentPosition.y += EditorGUIUtility.singleLineHeight;

        EditorGUI.PropertyField(currentPosition, handGestureProp, HandGestureContent);
        // currentPosition.y += EditorGUIUtility.singleLineHeight; // 最後の要素の場合は不要

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 3;
    }
}