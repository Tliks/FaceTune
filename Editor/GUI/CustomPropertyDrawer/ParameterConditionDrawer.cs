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

    private SerializedProperty? _parameterNameProp;
    private SerializedProperty? _parameterTypeProp;
    private SerializedProperty? _comparisonTypeProp;
    private SerializedProperty? _floatValueProp;
    private SerializedProperty? _intValueProp;
    private SerializedProperty? _boolValueProp;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        _parameterNameProp ??= property.FindPropertyRelative(ParameterCondition.ParameterNamePropName);
        _parameterTypeProp ??= property.FindPropertyRelative(ParameterCondition.ParameterTypePropName);
        
        Rect currentPosition = position;
        currentPosition.height = EditorGUIUtility.singleLineHeight;

        LocalizedUI.PropertyField(currentPosition, _parameterNameProp, "ParameterCondition:ParameterName");
        currentPosition.y += EditorGUIUtility.singleLineHeight;

        _parameterTypePopup.Field(currentPosition, _parameterTypeProp);
        currentPosition.y += EditorGUIUtility.singleLineHeight;

        ParameterType paramType = (ParameterType)_parameterTypeProp.enumValueIndex;

        switch (paramType)
        {
            case ParameterType.Int:
                _comparisonTypeProp ??= property.FindPropertyRelative(ParameterCondition.ComparisonTypePropName);
                _intValueProp ??= property.FindPropertyRelative(ParameterCondition.IntValuePropName);
                _comparisonTypePopup.Field(currentPosition, _comparisonTypeProp);
                currentPosition.y += EditorGUIUtility.singleLineHeight;
                LocalizedUI.PropertyField(currentPosition, _intValueProp, "ParameterCondition:IntValue");
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
                
                _comparisonTypePopup.Field(currentPosition, _comparisonTypeProp);
                currentPosition.y += EditorGUIUtility.singleLineHeight;
                LocalizedUI.PropertyField(currentPosition, _floatValueProp, "ParameterCondition:FloatValue");
                break;
            case ParameterType.Bool:
                _boolValueProp ??= property.FindPropertyRelative(ParameterCondition.BoolValuePropName);
                LocalizedUI.PropertyField(currentPosition, _boolValueProp, "ParameterCondition:BoolValue");
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
