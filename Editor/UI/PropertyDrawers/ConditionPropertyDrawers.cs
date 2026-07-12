namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(Condition))]
internal sealed class ConditionDrawer : PropertyDrawer
{
    private const int SeparatorFontSize = 8;
    private static readonly GUIStyle SeparatorStyle = new(EditorStyles.miniLabel)
    {
        alignment = TextAnchor.MiddleCenter,
        fontSize = SeparatorFontSize
    };
    private static readonly ReorderableListOptions CasesOptions = new(
        Foldout: false,
        MaxVisibleHeight: 260f,
        InitializeElement: element => element.FindPropertyRelative("Conditions").arraySize = 0,
        DrawElementSeparator: rect => DrawSeparator(rect, "condition.or.label".LG()));

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var always = property.FindPropertyRelative("Always");
        position.SetSingleHeight();
        var mode = always.boolValue ? 1 : 0;
        var nextMode = GUI.Toolbar(position, mode, new[]
        {
            "condition.mode.normal.label".LG(),
            "condition.mode.always.label".LG()
        });
        if (nextMode != mode) always.boolValue = nextMode == 1;
        position.NewLine();
        var cases = property.FindPropertyRelative("Cases");
        position.height = ReorderableListUI.GetHeight(cases, CasesOptions);
        using (new EditorGUI.DisabledScope(always.boolValue && !always.hasMultipleDifferentValues))
            ReorderableListUI.Draw(position, cases, "condition.conditions.label".LG(), CasesOptions);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => FaceTuneDrawerUtility.Line
         + ReorderableListUI.GetHeight(property.FindPropertyRelative("Cases"), CasesOptions);

    internal static void DrawSeparator(Rect boundary, GUIContent label)
    {
        var position = new Rect(
            boundary.x,
            boundary.y - EditorGUIUtility.singleLineHeight * .5f,
            boundary.width,
            EditorGUIUtility.singleLineHeight);
        EditorGUI.LabelField(position, label, SeparatorStyle);
    }
}

[CustomPropertyDrawer(typeof(ConditionCase))]
internal sealed class ConditionCaseDrawer : PropertyDrawer
{
    private static readonly ReorderableListOptions ConditionsOptions = new(
        Foldout: false,
        AddElement: ShowAddMenu);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var conditions = property.FindPropertyRelative("Conditions");
        ReorderableListUI.Draw(position, conditions, GetCaseLabel(property), ConditionsOptions);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => ReorderableListUI.GetHeight(property.FindPropertyRelative("Conditions"), ConditionsOptions);

    private static GUIContent GetCaseLabel(SerializedProperty property)
    {
        var match = System.Text.RegularExpressions.Regex.Match(property.propertyPath, @"Array\.data\[(\d+)\]$");
        var number = match.Success && int.TryParse(match.Groups[1].Value, out var index) ? index + 1 : 1;
        return new GUIContent($"{"condition.case.label".LG().text} {number}");
    }

    private static void ShowAddMenu(SerializedProperty conditions)
    {
        var serializedObject = conditions.serializedObject;
        var propertyPath = conditions.propertyPath;
        var menu = new GenericMenu();
        Add<HandGestureCondition>(menu, "condition.kind.hand.label", serializedObject, propertyPath);
        Add<MenuCondition>(menu, "condition.kind.menu.label", serializedObject, propertyPath);
        Add<ParameterCondition>(menu, "condition.kind.parameter.label", serializedObject, propertyPath);
        menu.ShowAsContext();
    }

    private static void Add<T>(GenericMenu menu, string labelKey, SerializedObject serializedObject, string propertyPath)
        where T : ConditionBase, new()
    {
        menu.AddItem(labelKey.LG(), false, () =>
        {
            serializedObject.Update();
            var conditions = serializedObject.FindProperty(propertyPath);
            var index = conditions.arraySize;
            conditions.InsertArrayElementAtIndex(index);
            conditions.GetArrayElementAtIndex(index).managedReferenceValue = new T();
            serializedObject.ApplyModifiedProperties();
        });
    }
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
