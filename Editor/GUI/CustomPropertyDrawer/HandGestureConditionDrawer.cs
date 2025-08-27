namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(HandGestureCondition))]
internal class HandGestureConditionDrawer : PropertyDrawer
{
    private readonly LocalizedPopup _handPopup;
    private readonly LocalizedPopup _handGesturePopup;
    private readonly LocalizedPopup _equalityComparisonPopup;

    public HandGestureConditionDrawer()
    {
        _handPopup = new LocalizedPopup(typeof(Hand));
        _handGesturePopup = new LocalizedPopup(typeof(HandGesture));
        _equalityComparisonPopup = new LocalizedPopup( // オーバーライド
            "HandGestureCondition:EqualityComparison",
            new[]
            {
                "HandGestureCondition:EqualityComparison:Equal",
                "HandGestureCondition:EqualityComparison:NotEqual"
            }
        );
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var handProp = property.FindPropertyRelative(HandGestureCondition.HandPropName);
        var handGestureProp = property.FindPropertyRelative(HandGestureCondition.HandGesturePropName);
        var equalityComparisonProp = property.FindPropertyRelative(HandGestureCondition.EqualityComparisonPropName);

        var currentPosition = position;
        currentPosition.height = EditorGUIUtility.singleLineHeight;

        _handPopup.Field(currentPosition, handProp);
        currentPosition.y += EditorGUIUtility.singleLineHeight;
        _handGesturePopup.Field(currentPosition, handGestureProp);
        currentPosition.y += EditorGUIUtility.singleLineHeight;
        _equalityComparisonPopup.Field(currentPosition, equalityComparisonProp); 

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 3;
    }
}