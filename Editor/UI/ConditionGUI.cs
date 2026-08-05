namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(Condition))]
internal sealed class ConditionDrawer : PropertyDrawer
{
    internal static float GetConditionBaseHeight(SerializedProperty condition)
        => GUIHelper.LineHeight
            + GUIHelper.VerticalSpacing
            + Mathf.Max(GUIHelper.LineHeight, EditorGUI.GetPropertyHeight(condition, GUIContent.none, true));

    internal static void DrawConditionBase(Rect position, SerializedProperty condition)
    {
        var types = new[] { typeof(HandGestureCondition), typeof(MenuCondition), typeof(ParameterCondition) };
        var labels = new[]
        {
            "condition.kind.none.label".LG(),
            "condition.kind.hand.label".LG(),
            "condition.kind.menu.label".LG(),
            "condition.kind.parameter.label".LG()
        };
        var currentType = condition.managedReferenceValue?.GetType();
        var currentIndex = Array.IndexOf(types, currentType) + 1;

        position.SetSingleHeight();
        var modeRect = EditorGUI.PrefixLabel(position, "common.mode.label".LG());
        var nextIndex = EditorGUI.Popup(modeRect, currentIndex, labels);
        if (nextIndex != currentIndex)
        {
            condition.managedReferenceValue = nextIndex == 0
                ? null
                : Activator.CreateInstance(types[nextIndex - 1]);
            return;
        }

        position.NewLine();
        position.height = GetConditionBaseHeight(condition) - GUIHelper.LineHeight - GUIHelper.VerticalSpacing;
        var conditionRect = EditorGUI.PrefixLabel(position, "condition.section.label".LG());
        EditorGUI.PropertyField(conditionRect, condition, GUIContent.none, true);
    }

    private const int SeparatorFontSize = 8;
    private const float EmptyMessageHeight = 30f;
    private static readonly GUIStyle SeparatorStyle = new(EditorStyles.miniLabel)
    {
        alignment = TextAnchor.MiddleCenter,
        fontSize = SeparatorFontSize
    };
    private static readonly ReorderableListOptions CasesOptions = new(
        Header: ReorderableListOptions.HeaderMode.None,
        MaxVisibleHeight: 260f,
        EmptyContentHeight: EmptyMessageHeight,
        DrawEmptyOverride: DrawEmptyMessage,
        InitializeElement: element => element.FindPropertyRelative("Conditions").arraySize = 0,
        DrawElementSeparator: rect => DrawSeparator(rect, "condition.or.label".LG()),
        Controls: ReorderableListOptions.ControlsPlacement.Manual);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var always = property.FindPropertyRelative("Always");
        position.SetSingleHeight();
        var cases = property.FindPropertyRelative("Cases");
        var showCases = !always.boolValue || always.hasMultipleDifferentValues;
        var modeRect = position;
        var controlsRect = Rect.zero;
        if (showCases) (modeRect, controlsRect) = position.SplitRight(GUIHelper.ListControlsWidth);
        var mode = always.boolValue ? 1 : 0;
        var popupRect = EditorGUI.PrefixLabel(modeRect, "condition.mode.label".LG());
        var nextMode = GUIHelper.LocalizedPopup(
            popupRect,
            mode,
            null,
            new[] { "condition.mode.normal.label", "condition.mode.always.label" });
        if (nextMode != mode) always.boolValue = nextMode == 1;
        if (showCases) GUIHelper.DrawListControls(controlsRect, cases, CasesOptions);
        position.NewLine();
        if (always.boolValue && !always.hasMultipleDifferentValues) return;

        position.height = GUIHelper.GetListHeight(cases, CasesOptions);
        GUIHelper.DrawList(position, cases, "condition.conditions.label".LG(), CasesOptions);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var always = property.FindPropertyRelative("Always");
        if (always.boolValue && !always.hasMultipleDifferentValues) return GUIHelper.LineHeight;
        var cases = property.FindPropertyRelative("Cases");
        return GUIHelper.LineHeight + GUIHelper.GetListHeight(cases, CasesOptions);
    }

    internal static void DrawSeparator(Rect boundary, GUIContent label)
        => DrawSeparator(boundary.x, boundary.y, boundary.width, label);

    internal static void DrawSeparatorAt(float x, float y, GUIContent label)
        => DrawSeparator(x, y, SeparatorStyle.CalcSize(label).x, label);

    private static void DrawSeparator(float x, float y, float width, GUIContent label)
    {
        var position = new Rect(
            x,
            y - GUIHelper.LineHeight * .5f,
            width,
            GUIHelper.LineHeight);
        EditorGUI.LabelField(position, label, SeparatorStyle);
    }

    internal static void DrawEmptyMessage(Rect position, SerializedProperty _)
        => EditorGUI.HelpBox(position, "condition.emptyCase.message".LG().text, MessageType.Warning);
}

[CustomPropertyDrawer(typeof(ConditionCase))]
internal sealed class ConditionCaseDrawer : PropertyDrawer
{
    private const float EmptyMessageHeight = 30f;
    private static readonly ReorderableListOptions ConditionsOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        AddElementOverride: ShowAddMenu,
        DrawElementSeparator: DrawAndSeparator,
        DrawElementOverride: DrawCondition,
        EmptyContentHeight: EmptyMessageHeight,
        DrawEmptyOverride: ConditionDrawer.DrawEmptyMessage,
        NestContent: false,
        Controls: ReorderableListOptions.ControlsPlacement.Header);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var conditions = property.FindPropertyRelative("Conditions");
        GUIHelper.DrawList(position, conditions, GetCaseLabel(property), ConditionsOptions);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var conditions = property.FindPropertyRelative("Conditions");
        return GUIHelper.GetListHeight(conditions, ConditionsOptions);
    }

    private static void DrawCondition(Rect position, SerializedProperty condition)
    {
        position.Indent();
        EditorGUI.PropertyField(position, condition, GUIContent.none, true);
    }

    private static void DrawAndSeparator(Rect boundary)
        => ConditionDrawer.DrawSeparatorAt(boundary.x, boundary.y, "condition.and.label".LG());

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
        GUIHelper.LocalizedEnumPopup(position, property, string.Empty, keys);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => GUIHelper.LineHeight;
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
        GUIHelper.DrawLocalizedEnum(modeRect, mode, string.Empty, nameof(MenuConditionMode));
        if (mode.enumValueIndex >= 2)
            EditorGUI.PropertyField(thresholdRect, property.FindPropertyRelative("Threshold"), GUIContent.none);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => GUIHelper.LineHeight;
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
            GUIHelper.DrawLocalizedEnum(typeRect, type, string.Empty, nameof(ParameterType));
            var boolOptions = new[] { "common.false.label".LG(), "common.true.label".LG() };
            value.boolValue = EditorGUI.Popup(boolValueRect, value.boolValue ? 1 : 0, boolOptions) == 1;
            return;
        }

        var (parameterRect, parameterTypeRect) = position.SplitRatio(.65f);
        EditorGUI.PropertyField(parameterRect, name, GUIContent.none);
        GUIHelper.DrawLocalizedEnum(parameterTypeRect, type, string.Empty, nameof(ParameterType));
        position.NewLine();

        if (type.enumValueIndex == (int)ParameterType.Float
            && comparison.enumValueIndex != (int)ComparisonType.GreaterThan
            && comparison.enumValueIndex != (int)ComparisonType.LessThan)
            comparison.enumValueIndex = (int)ComparisonType.GreaterThan;

        var (comparisonRect, valueRect) = position.SplitRatio(.5f);
        GUIHelper.DrawLocalizedEnum(comparisonRect, comparison, string.Empty, nameof(ComparisonType));
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
            ? GUIHelper.LineHeight
            : GUIHelper.LineHeight * 2f + GUIHelper.VerticalSpacing;
}
