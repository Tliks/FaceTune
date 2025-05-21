namespace com.aoyon.facetune.ui;

[CustomPropertyDrawer(typeof(ParameterCondition))]
internal class ParameterConditionDrawer : PropertyDrawer
{
    private SerializedProperty? _comparisonTypeProp;
    private SerializedProperty? _boolComparisonTypeProp;
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
                _comparisonTypeProp ??= property.FindPropertyRelative(nameof(ParameterCondition.ComparisonType));
                _intValueProp ??= property.FindPropertyRelative(nameof(ParameterCondition.IntValue));
                EditorGUI.PropertyField(currentPosition, _comparisonTypeProp, new GUIContent("Comparison"));
                currentPosition.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(currentPosition, _intValueProp, new GUIContent("Value"));
                break;
            case ParameterType.Float:
                _comparisonTypeProp ??= property.FindPropertyRelative(nameof(ParameterCondition.ComparisonType));
                _floatValueProp ??= property.FindPropertyRelative(nameof(ParameterCondition.FloatValue));
                EditorGUI.PropertyField(currentPosition, _comparisonTypeProp, new GUIContent("Comparison"));
                currentPosition.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(currentPosition, _floatValueProp, new GUIContent("Value"));
                break;
            case ParameterType.Bool:
                _boolComparisonTypeProp ??= property.FindPropertyRelative(nameof(ParameterCondition.BoolComparisonType));
                _boolValueProp ??= property.FindPropertyRelative(nameof(ParameterCondition.BoolValue));
                EditorGUI.PropertyField(currentPosition, _boolComparisonTypeProp, new GUIContent("Comparison"));
                currentPosition.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(currentPosition, _boolValueProp, new GUIContent("Value"));
                break;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // ParameterName, ParameterType, Comparison, Value の4行分
        return EditorGUIUtility.singleLineHeight * 4;
    }
}
