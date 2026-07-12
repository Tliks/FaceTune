namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(Condition))]
internal sealed class ConditionDrawer : PropertyDrawer
{
    private static readonly ReorderableListOptions CasesOptions = new(Foldout: false, MaxVisibleHeight: 260f);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var always = property.FindPropertyRelative("Always");
        FaceTuneDrawerUtility.Draw(ref position, always, "condition.always.label");
        position = EditorGUI.IndentedRect(position);
        var cases = property.FindPropertyRelative("Cases");
        position.height = ReorderableListUI.GetHeight(cases, CasesOptions);
        using (new EditorGUI.DisabledScope(always.boolValue && !always.hasMultipleDifferentValues))
            ReorderableListUI.Draw(position, cases, "condition.conditions.label".LG(), CasesOptions);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => FaceTuneDrawerUtility.Height(property.FindPropertyRelative("Always"))
         + ReorderableListUI.GetHeight(property.FindPropertyRelative("Cases"), CasesOptions);
}

[CustomPropertyDrawer(typeof(ConditionCase))]
internal sealed class ConditionCaseDrawer : PropertyDrawer
{
    private static readonly ReorderableListOptions ListOptions = new(Foldout: false, MaxVisibleHeight: 120f);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        DrawList(ref position, property, "HandGestureConditions", "conditionCase.handGestureConditions.label");
        DrawList(ref position, property, "MenuConditions", "conditionCase.menuConditions.label");
        DrawList(ref position, property, "ParameterConditions", "conditionCase.parameterConditions.label");
    }

    private static void DrawList(ref Rect position, SerializedProperty property, string name, string key)
    {
        var list = property.FindPropertyRelative(name);
        var height = ReorderableListUI.GetHeight(list, ListOptions);
        position.height = height;
        ReorderableListUI.Draw(position, list, key.LG(), ListOptions);
        position.y += height;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => Height(property, "HandGestureConditions") + Height(property, "MenuConditions") + Height(property, "ParameterConditions");

    private static float Height(SerializedProperty property, string name)
        => ReorderableListUI.GetHeight(property.FindPropertyRelative(name), ListOptions);
}

[CustomPropertyDrawer(typeof(HandGestureCondition))]
internal sealed class HandGestureConditionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        position.SetSingleHeight();
        var (matchRect, gestureRect) = position.SplitRatio(.5f);
        DrawEnum(matchRect, property.FindPropertyRelative("Match"), nameof(HandGestureMatch));
        DrawEnum(gestureRect, property.FindPropertyRelative("HandGesture"), nameof(HandGesture));
    }

    private static void DrawEnum(Rect position, SerializedProperty property, string typeName)
    {
        var prefix = char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
        var keys = property.enumNames.Select(name => $"{prefix}.option.{char.ToLowerInvariant(name[0]) + name.Substring(1)}");
        LocalizedUI.EnumPopup(position, property, string.Empty, keys);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => FaceTuneDrawerUtility.Line;
}

[CustomPropertyDrawer(typeof(MenuCondition))]
internal sealed class MenuConditionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        position.SetSingleHeight();
        var mode = property.FindPropertyRelative("Mode");
        var (sourceRect, rest) = position.SplitRatio(.5f);
        var (modeRect, thresholdRect) = rest.SplitRatio(mode.enumValueIndex >= 2 ? .55f : 1f);
        EditorGUI.PropertyField(sourceRect, property.FindPropertyRelative("MenuSource"), GUIContent.none);
        FaceTuneDrawerUtility.Enum(modeRect, mode, string.Empty, nameof(MenuConditionMode));
        if (mode.enumValueIndex >= 2)
            EditorGUI.PropertyField(thresholdRect, property.FindPropertyRelative("Threshold"), GUIContent.none);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => FaceTuneDrawerUtility.Line;
}

[CustomPropertyDrawer(typeof(ParameterCondition))]
internal sealed class ParameterConditionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var name = property.FindPropertyRelative("ParameterName");
        var type = property.FindPropertyRelative("ParameterType");
        var comparison = property.FindPropertyRelative("ComparisonType");
        var value = GetValue(property, type);

        position.SetSingleHeight();
        if (type.enumValueIndex == (int)ParameterType.Bool)
        {
            var (nameRect, remainder) = position.SplitRatio(.55f);
            var (typeRect, boolValueRect) = remainder.SplitRatio(.6f);
            EditorGUI.PropertyField(nameRect, name, GUIContent.none);
            FaceTuneDrawerUtility.Enum(typeRect, type, string.Empty, nameof(ParameterType));
            var boolOptions = new[] { "common.false.label".LG(), "common.true.label".LG() };
            value.boolValue = EditorGUI.Popup(boolValueRect, value.boolValue ? 1 : 0, boolOptions) == 1;
            return;
        }

        var (parameterRect, parameterTypeRect) = position.SplitRatio(.65f);
        EditorGUI.PropertyField(parameterRect, name, GUIContent.none);
        FaceTuneDrawerUtility.Enum(parameterTypeRect, type, string.Empty, nameof(ParameterType));
        position.NewLine();

        if (type.enumValueIndex == (int)ParameterType.Float
            && comparison.enumValueIndex != (int)ComparisonType.GreaterThan
            && comparison.enumValueIndex != (int)ComparisonType.LessThan)
            comparison.enumValueIndex = (int)ComparisonType.GreaterThan;

        var (comparisonRect, valueRect) = position.SplitRatio(.5f);
        FaceTuneDrawerUtility.Enum(comparisonRect, comparison, string.Empty, nameof(ComparisonType));
        EditorGUI.PropertyField(valueRect, value, GUIContent.none);
    }

    private static SerializedProperty GetValue(SerializedProperty property, SerializedProperty type)
        => type.enumValueIndex switch
        {
            (int)ParameterType.Float => property.FindPropertyRelative("FloatValue"),
            (int)ParameterType.Bool => property.FindPropertyRelative("BoolValue"),
            _ => property.FindPropertyRelative("IntValue")
        };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => property.FindPropertyRelative("ParameterType").enumValueIndex == (int)ParameterType.Bool
            ? FaceTuneDrawerUtility.Line
            : FaceTuneDrawerUtility.Line * 2f + FaceTuneDrawerUtility.Space;
}
