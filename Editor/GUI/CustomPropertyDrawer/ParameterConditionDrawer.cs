namespace com.aoyon.facetune.ui;

[CustomPropertyDrawer(typeof(ParameterCondition))]
internal class ParameterConditionDrawer : PropertyDrawer
{
    private const string ParameterNamePropName = "_parameterName";
    private const string ParameterTypePropName = "_parameterType";
    private const string FloatComparisonTypePropName = "_floatComparisonType";
    private const string IntComparisonTypePropName = "_intComparisonType";
    private const string FloatValuePropName = "_floatValue";
    private const string IntValuePropName = "_intValue";
    private const string BoolValuePropName = "_boolValue";

    private static readonly GUIContent ParameterNameContent = new("Parameter Name");
    private static readonly GUIContent ParameterTypeContent = new("Parameter Type");
    private static readonly GUIContent FloatComparisonTypeContent = new("Comparison");
    private static readonly GUIContent IntComparisonTypeContent = new("Comparison");
    private static readonly GUIContent FloatValueContent = new("Value");
    private static readonly GUIContent IntValueContent = new("Value");
    private static readonly GUIContent BoolValueContent = new("Value");

    private SerializedProperty? _floatComparisonTypeProp;
    private SerializedProperty? _intComparisonTypeProp;
    private SerializedProperty? _floatValueProp;
    private SerializedProperty? _intValueProp;
    private SerializedProperty? _boolValueProp;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var parameterNameProp = property.FindPropertyRelative(ParameterNamePropName);
        var parameterTypeProp = property.FindPropertyRelative(ParameterTypePropName);
        
        Rect currentPosition = position;
        currentPosition.height = EditorGUIUtility.singleLineHeight;

        EditorGUI.PropertyField(currentPosition, parameterNameProp, ParameterNameContent);
        currentPosition.y += EditorGUIUtility.singleLineHeight;

        EditorGUI.PropertyField(currentPosition, parameterTypeProp, ParameterTypeContent);
        currentPosition.y += EditorGUIUtility.singleLineHeight;

        ParameterType paramType = (ParameterType)parameterTypeProp.enumValueIndex;

        switch (paramType)
        {
            case ParameterType.Int:
                _intComparisonTypeProp ??= property.FindPropertyRelative(IntComparisonTypePropName);
                _intValueProp ??= property.FindPropertyRelative(IntValuePropName);
                EditorGUI.PropertyField(currentPosition, _intComparisonTypeProp, IntComparisonTypeContent);
                currentPosition.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(currentPosition, _intValueProp, IntValueContent);
                break;
            case ParameterType.Float:
                _floatComparisonTypeProp ??= property.FindPropertyRelative(FloatComparisonTypePropName);
                _floatValueProp ??= property.FindPropertyRelative(FloatValuePropName);
                EditorGUI.PropertyField(currentPosition, _floatComparisonTypeProp, FloatComparisonTypeContent);
                currentPosition.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(currentPosition, _floatValueProp, FloatValueContent);
                break;
            case ParameterType.Bool:
                _boolValueProp ??= property.FindPropertyRelative(nameof(ParameterCondition.BoolValue));
                EditorGUI.PropertyField(currentPosition, _boolValueProp, BoolValueContent);
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
