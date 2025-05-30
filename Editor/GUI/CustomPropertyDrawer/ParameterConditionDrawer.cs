namespace com.aoyon.facetune.ui;

[CustomPropertyDrawer(typeof(ParameterCondition))]
internal class ParameterConditionDrawer : PropertyDrawer
{
    private SerializedProperty? _floatComparisonTypeProp;
    private SerializedProperty? _intComparisonTypeProp;
    private SerializedProperty? _floatValueProp;
    private SerializedProperty? _intValueProp;
    private SerializedProperty? _boolValueProp;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var parameterNameProp = property.FindPropertyRelative(nameof(ParameterCondition.ParameterName));
        var parameterTypeProp = property.FindPropertyRelative(nameof(ParameterCondition.ParameterType));
        
        Rect currentPosition = position;
        currentPosition.height = EditorGUIUtility.singleLineHeight;

        EditorGUI.PropertyField(currentPosition, parameterNameProp, new GUIContent("Parameter Name"));
        currentPosition.y += EditorGUIUtility.singleLineHeight;

        EditorGUI.PropertyField(currentPosition, parameterTypeProp, new GUIContent("Parameter Type"));
        currentPosition.y += EditorGUIUtility.singleLineHeight;

        ParameterType paramType = (ParameterType)parameterTypeProp.enumValueIndex;

        switch (paramType)
        {
            case ParameterType.Int:
                _intComparisonTypeProp ??= property.FindPropertyRelative(nameof(ParameterCondition.IntComparisonType));
                _intValueProp ??= property.FindPropertyRelative(nameof(ParameterCondition.IntValue));
                EditorGUI.PropertyField(currentPosition, _intComparisonTypeProp, new GUIContent("Comparison"));
                currentPosition.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(currentPosition, _intValueProp, new GUIContent("Value"));
                break;
            case ParameterType.Float:
                _floatComparisonTypeProp ??= property.FindPropertyRelative(nameof(ParameterCondition.FloatComparisonType));
                _floatValueProp ??= property.FindPropertyRelative(nameof(ParameterCondition.FloatValue));
                EditorGUI.PropertyField(currentPosition, _floatComparisonTypeProp, new GUIContent("Comparison"));
                currentPosition.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(currentPosition, _floatValueProp, new GUIContent("Value"));
                break;
            case ParameterType.Bool:
                _boolValueProp ??= property.FindPropertyRelative(nameof(ParameterCondition.BoolValue));
                EditorGUI.PropertyField(currentPosition, _boolValueProp, new GUIContent("Value"));
                break;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var parameterTypeProp = property.FindPropertyRelative(nameof(ParameterCondition.ParameterType));
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
