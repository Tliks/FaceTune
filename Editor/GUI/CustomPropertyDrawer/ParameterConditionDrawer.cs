namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(ParameterCondition))]
internal class ParameterConditionDrawer : PropertyDrawer
{
    private readonly LocalizedPopup _parameterTypePopup;
    private readonly LocalizedPopup _comparisonTypePopup;

    public ParameterConditionDrawer()
    {
        _parameterTypePopup = new LocalizedPopup(typeof(ParameterType));
        _comparisonTypePopup = new LocalizedPopup(typeof(ComparisonType));
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var parameterNameProp = property.FindPropertyRelative(ParameterCondition.ParameterNamePropName);
        var parameterTypeProp = property.FindPropertyRelative(ParameterCondition.ParameterTypePropName);
        
        Rect currentPosition = position;
        currentPosition.height = EditorGUIUtility.singleLineHeight;

        LocalizedUI.PropertyField(currentPosition, parameterNameProp, "ParameterCondition:prop:ParameterName");
        currentPosition.y += EditorGUIUtility.singleLineHeight;

        _parameterTypePopup.Field(currentPosition, parameterTypeProp);
        currentPosition.y += EditorGUIUtility.singleLineHeight;

        ParameterType paramType = (ParameterType)parameterTypeProp.enumValueIndex;

        switch (paramType)
        {
            case ParameterType.Int:
            {
                var comparisonTypeProp = property.FindPropertyRelative(ParameterCondition.ComparisonTypePropName);
                var intValueProp = property.FindPropertyRelative(ParameterCondition.IntValuePropName);
                _comparisonTypePopup.Field(currentPosition, comparisonTypeProp);
                currentPosition.y += EditorGUIUtility.singleLineHeight;
                LocalizedUI.PropertyField(currentPosition, intValueProp, "ParameterCondition:prop:IntValue");
                break;
            }
            case ParameterType.Float:
            {
                var comparisonTypeProp = property.FindPropertyRelative(ParameterCondition.ComparisonTypePropName);
                var floatValueProp = property.FindPropertyRelative(ParameterCondition.FloatValuePropName);
                
                // Floatの場合はGreaterThanとLessThanのみ選択可能にする
                ComparisonType currentComparison = (ComparisonType)comparisonTypeProp.enumValueIndex;
                if (currentComparison != ComparisonType.GreaterThan && currentComparison != ComparisonType.LessThan)
                {
                    comparisonTypeProp.enumValueIndex = (int)ComparisonType.GreaterThan;
                }
                
                _comparisonTypePopup.Field(currentPosition, comparisonTypeProp);
                currentPosition.y += EditorGUIUtility.singleLineHeight;
                LocalizedUI.PropertyField(currentPosition, floatValueProp, "ParameterCondition:prop:FloatValue");
                break;
            }
            case ParameterType.Bool:
                var boolValueProp = property.FindPropertyRelative(ParameterCondition.BoolValuePropName);
                LocalizedUI.PropertyField(currentPosition, boolValueProp, "ParameterCondition:prop:BoolValue");
                break;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var parameterTypeProp = property.FindPropertyRelative(ParameterCondition.ParameterTypePropName);
        ParameterType paramType = (ParameterType)parameterTypeProp.enumValueIndex;

        // ParameterName, ParameterTypeは必ず表示
        int lineCount = 2;

        // Int, FloatはComparisonとValueで+2行、BoolはValueで+1行
        switch (paramType)
        {
            case ParameterType.Int:
            case ParameterType.Float:
                lineCount += 2;
                break;
            case ParameterType.Bool:
                lineCount += 1;
                break;
            default:
                break;
        }

        return EditorGUIUtility.singleLineHeight * lineCount;
    }
}
