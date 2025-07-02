namespace aoyon.facetune.ui;

[CustomPropertyDrawer(typeof(ParameterCondition))]
internal class ParameterConditionDrawer : PropertyDrawer
{
    private static readonly string[] FloatComparisonOptions = { "Greater Than", "Less Than" };
    private static readonly int[] FloatComparisonValues = { (int)ComparisonType.GreaterThan, (int)ComparisonType.LessThan };
    
    private SerializedProperty? _comparisonTypeProp;
    private SerializedProperty? _floatValueProp;
    private SerializedProperty? _intValueProp;
    private SerializedProperty? _boolValueProp;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var parameterNameProp = property.FindPropertyRelative(ParameterCondition.ParameterNamePropName);
        var parameterTypeProp = property.FindPropertyRelative(ParameterCondition.ParameterTypePropName);
        
        Rect currentPosition = position;
        currentPosition.height = EditorGUIUtility.singleLineHeight;

        EditorGUI.PropertyField(currentPosition, parameterNameProp);
        currentPosition.y += EditorGUIUtility.singleLineHeight;

        EditorGUI.PropertyField(currentPosition, parameterTypeProp);
        currentPosition.y += EditorGUIUtility.singleLineHeight;

        ParameterType paramType = (ParameterType)parameterTypeProp.enumValueIndex;

        switch (paramType)
        {
            case ParameterType.Int:
                _comparisonTypeProp ??= property.FindPropertyRelative(ParameterCondition.ComparisonTypePropName);
                _intValueProp ??= property.FindPropertyRelative(ParameterCondition.IntValuePropName);
                EditorGUI.PropertyField(currentPosition, _comparisonTypeProp);
                currentPosition.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(currentPosition, _intValueProp);
                break;
            case ParameterType.Float:
                _comparisonTypeProp ??= property.FindPropertyRelative(ParameterCondition.ComparisonTypePropName);
                _floatValueProp ??= property.FindPropertyRelative(ParameterCondition.FloatValuePropName);
                
                // Floatの場合はGreaterThanとLessThanのみ選択可能にする
                ComparisonType currentComparison = (ComparisonType)_comparisonTypeProp.enumValueIndex;
                if (currentComparison != ComparisonType.GreaterThan && currentComparison != ComparisonType.LessThan)
                {
                    _comparisonTypeProp.enumValueIndex = (int)ComparisonType.GreaterThan;
                }
                
                int selectedIndex = Array.IndexOf(FloatComparisonValues, _comparisonTypeProp.enumValueIndex);
                if (selectedIndex == -1) selectedIndex = 0;
                
                int newIndex = EditorGUI.Popup(currentPosition, "Comparison Type", selectedIndex, FloatComparisonOptions);
                _comparisonTypeProp.enumValueIndex = FloatComparisonValues[newIndex];
                
                currentPosition.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(currentPosition, _floatValueProp);
                break;
            case ParameterType.Bool:
                _boolValueProp ??= property.FindPropertyRelative(ParameterCondition.BoolValuePropName);
                EditorGUI.PropertyField(currentPosition, _boolValueProp);
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
