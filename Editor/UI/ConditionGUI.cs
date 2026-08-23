namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(ConditionSelection))]
internal sealed class ConditionSelectionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var mode = property.FindPropertyRelative(nameof(ConditionSelection.Mode));
        var condition = property.FindPropertyRelative(nameof(ConditionSelection.Condition));
        position.SetSingleHeight();
        var showsCondition = mode.hasMultipleDifferentValues || mode.enumValueIndex == (int)ConditionSelection.Kind.Conditional;
        var modeRect = position;
        if (showsCondition)
        {
            var controlsRect = Rect.zero;
            (modeRect, controlsRect) = position.SplitRight(GUIHelper.ListControlsWidth);
            var cases = condition.FindPropertyRelative(nameof(Condition.Cases));
            GUIHelper.DrawListControls(controlsRect, cases, ConditionGUI.CasesOptions);
        }
        GUIHelper.LocalizedEnumPopup(
            modeRect,
            mode,
            "condition.mode.label",
            new[] { "condition.mode.always.label", "condition.mode.normal.label" });
        if (!showsCondition) return;

        position.NewLine();
        position.height = ConditionGUI.GetHeight(condition, false);
        ConditionGUI.Draw(position, condition, false);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var mode = property.FindPropertyRelative(nameof(ConditionSelection.Mode));
        if (!mode.hasMultipleDifferentValues && mode.enumValueIndex == (int)ConditionSelection.Kind.Always)
            return GUIHelper.LineHeight;
        return GUIHelper.LineHeight
             + GUIHelper.VerticalSpacing
             + ConditionGUI.GetHeight(property.FindPropertyRelative(nameof(ConditionSelection.Condition)), false);
    }
}

internal static class ConditionGUI
{
    private const int SeparatorFontSize = 8;
    private const float OrSeparatorLeftOffset = 20f;
    private const float EmptyMessageHeight = 30f;
    private static GUIStyle? _separatorStyle;
    private static GUIStyle SeparatorStyle => _separatorStyle ??= new GUIStyle(EditorStyles.miniLabel)
    {
        alignment = TextAnchor.MiddleCenter,
        fontSize = SeparatorFontSize
    };
    internal static readonly ReorderableListOptions CasesOptions = new(
        Header: ReorderableListOptions.HeaderMode.None,
        MaxVisibleHeight: 260f,
        NestContent: false,
        EmptyContentHeight: EmptyMessageHeight,
        DrawEmptyOverride: DrawEmptyMessage,
        InitializeElement: element => element.CopyFrom(new ConditionCase()),
        DrawElementSeparator: rect => DrawSeparatorAt(
            rect.x - OrSeparatorLeftOffset,
            rect.y,
            "condition.or.label".LG()),
        Controls: ReorderableListOptions.ControlsPlacement.Manual);

    internal static float GetHeight(SerializedProperty condition, bool drawControls)
    {
        var listHeight = GUIHelper.GetListHeight(condition.FindPropertyRelative(nameof(Condition.Cases)), CasesOptions);
        return drawControls ? GUIHelper.LineHeight + GUIHelper.VerticalSpacing + listHeight : listHeight;
    }

    internal static void Draw(Rect position, SerializedProperty condition, bool drawControls)
    {
        var cases = condition.FindPropertyRelative(nameof(Condition.Cases));
        if (drawControls)
        {
            position.SetSingleHeight();
            var (labelRect, controlsRect) = position.SplitRight(GUIHelper.ListControlsWidth);
            EditorGUI.LabelField(labelRect, "condition.conditions.label".LG());
            GUIHelper.DrawListControls(controlsRect, cases, CasesOptions);
            position.NewLine();
        }
        position.height = GUIHelper.GetListHeight(cases, CasesOptions);
        GUIHelper.DrawList(position, cases, GUIContent.none, CasesOptions);
    }

    internal static float GetSeparatorWidth(GUIContent label)
        => SeparatorStyle.CalcSize(label).x;

    internal static void DrawSeparatorAt(float x, float y, GUIContent label)
        => DrawSeparator(x, y, GetSeparatorWidth(label), label);

    private static void DrawSeparator(float x, float y, float width, GUIContent label)
    {
        var position = new Rect(x, y - GUIHelper.LineHeight * .5f, width, GUIHelper.LineHeight);
        EditorGUI.LabelField(position, label, SeparatorStyle);
    }

    internal static void DrawEmptyMessage(Rect position, SerializedProperty _)
        => EditorGUI.HelpBox(position, "condition.emptyCase.message".LS(), MessageType.Warning);
}

[CustomPropertyDrawer(typeof(Condition))]
internal sealed class ConditionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        ConditionGUI.Draw(position, property, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => ConditionGUI.GetHeight(property, true);
}

[CustomPropertyDrawer(typeof(ConditionCase))]
internal sealed class ConditionCaseDrawer : PropertyDrawer
{
    private const float EmptyMessageHeight = 30f;
    private const float MaxVisibleHeight = 126f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var rows = GetRows(property);
        GUIHelper.DrawVirtualList(
            position,
            property,
            rows,
            GetCaseLabel(property),
            index => EditorGUI.GetPropertyHeight(GetElement(property, rows[index]), GUIContent.none, true),
            (rect, index) => DrawCondition(rect, property, rows, index),
            () => ShowAddMenu(property),
            index => Remove(property, rows, index),
            rect => ConditionGUI.DrawEmptyMessage(rect, property),
            EmptyMessageHeight,
            MaxVisibleHeight);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var rows = GetRows(property);
        return GUIHelper.GetVirtualListHeight(
            rows,
            index => EditorGUI.GetPropertyHeight(GetElement(property, rows[index]), GUIContent.none, true),
            EmptyMessageHeight,
            MaxVisibleHeight);
    }

    private static void DrawCondition(Rect rect, SerializedProperty property, List<Row> rows, int index)
    {
        if (index < 0 || index >= rows.Count) return;
        if (index > 0) ConditionGUI.DrawSeparatorAt(rect.x, rect.y, "condition.and.label".LG());
        var separatorWidth = ConditionGUI.GetSeparatorWidth("condition.and.label".LG());
        var indent = Mathf.Max(GUIHelper.IndentWidth, separatorWidth);
        rect.x += indent;
        rect.width = Mathf.Max(0f, rect.width - indent);
        var element = GetElement(property, rows[index]);
        rect.height = EditorGUI.GetPropertyHeight(element, GUIContent.none, true);
        EditorGUI.PropertyField(rect, element, GUIContent.none, true);
    }

    private static List<Row> GetRows(SerializedProperty property)
    {
        var rows = new List<Row>();
        AddRows(rows, property.FindPropertyRelative(nameof(ConditionCase.HandGestureConditions)), Kind.HandGesture);
        AddRows(rows, property.FindPropertyRelative(nameof(ConditionCase.MenuConditions)), Kind.Menu);
        AddRows(rows, property.FindPropertyRelative(nameof(ConditionCase.ParameterConditions)), Kind.Parameter);
        return rows;
    }

    private static void AddRows(List<Row> rows, SerializedProperty array, Kind kind)
    {
        for (var index = 0; index < array.arraySize; index++)
            rows.Add(new Row(kind, index));
    }

    private static void ShowAddMenu(SerializedProperty property)
    {
        var serializedObject = property.serializedObject;
        var propertyPath = property.propertyPath;
        var menu = new GenericMenu();
        Add<HandGestureCondition>(menu, "condition.kind.hand.label", serializedObject, propertyPath, Kind.HandGesture);
        Add<MenuCondition>(menu, "condition.kind.menu.label", serializedObject, propertyPath, Kind.Menu);
        Add<ParameterCondition>(menu, "condition.kind.parameter.label", serializedObject, propertyPath, Kind.Parameter);
        menu.ShowAsContext();
    }

    private static void Add<T>(GenericMenu menu, string labelKey, SerializedObject serializedObject, string propertyPath, Kind kind)
        where T : new()
        => menu.AddItem(labelKey.LG(), false, () =>
        {
            serializedObject.Update();
            var property = serializedObject.FindProperty(propertyPath);
            var array = GetArray(property, kind);
            var index = array.arraySize;
            array.InsertArrayElementAtIndex(index);
            array.GetArrayElementAtIndex(index).CopyFrom(new T());
            GUIHelper.SetVirtualListIndex(property, GetGlobalIndex(property, kind, index));
            serializedObject.ApplyModifiedProperties();
        });

    private static void Remove(SerializedProperty property, List<Row> rows, int index)
    {
        var row = rows[index];
        GetArray(property, row.Kind).DeleteArrayElementAtIndex(row.LocalIndex);
    }

    private static SerializedProperty GetElement(SerializedProperty property, Row row)
        => GetArray(property, row.Kind).GetArrayElementAtIndex(row.LocalIndex);

    private static SerializedProperty GetArray(SerializedProperty property, Kind kind) => kind switch
    {
        Kind.HandGesture => property.FindPropertyRelative(nameof(ConditionCase.HandGestureConditions)),
        Kind.Menu => property.FindPropertyRelative(nameof(ConditionCase.MenuConditions)),
        Kind.Parameter => property.FindPropertyRelative(nameof(ConditionCase.ParameterConditions)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static int GetGlobalIndex(SerializedProperty property, Kind kind, int localIndex)
        => kind switch
        {
            Kind.HandGesture => localIndex,
            Kind.Menu => property.FindPropertyRelative(nameof(ConditionCase.HandGestureConditions)).arraySize + localIndex,
            Kind.Parameter => property.FindPropertyRelative(nameof(ConditionCase.HandGestureConditions)).arraySize
                            + property.FindPropertyRelative(nameof(ConditionCase.MenuConditions)).arraySize
                            + localIndex,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static GUIContent GetCaseLabel(SerializedProperty property)
    {
        var match = System.Text.RegularExpressions.Regex.Match(property.propertyPath, @"Array\.data\[(\d+)\]$");
        var number = match.Success && int.TryParse(match.Groups[1].Value, out var index) ? index + 1 : 1;
        return new GUIContent($"{"condition.case.label".LG().text} {number}");
    }

    private enum Kind { HandGesture, Menu, Parameter }
    private sealed record Row(Kind Kind, int LocalIndex);
}

[CustomPropertyDrawer(typeof(HandGestureCondition))]
internal sealed class HandGestureConditionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        position.SetSingleHeight();
        var hand = property.FindPropertyRelative(nameof(HandGestureCondition.Hand));
        var gesture = property.FindPropertyRelative(nameof(HandGestureCondition.Gesture));
        var matches = property.FindPropertyRelative(nameof(HandGestureCondition.Matches));
        var matchOptions = new[] { "condition.hand.matches.label".LG(), "condition.hand.doesNotMatch.label".LG() };
        var fields = position.FlexHorizontal(1f, 1f, 1f);
        var handRect = fields[0];
        var gestureRect = fields[1];
        var matchesRect = fields[2];
        GUIHelper.DrawLocalizedEnum(handRect, hand, string.Empty, nameof(HandGestureHand));
        GUIHelper.DrawLocalizedEnum(gestureRect, gesture, string.Empty, nameof(HandGesture));
        matches.boolValue = EditorGUI.Popup(matchesRect, matches.boolValue ? 0 : 1, matchOptions) == 0;
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
        var modeWidth = GUIHelper.LocalizedEnumPopupWidth(mode, nameof(MenuConditionMode));
        var (beforeMode, modeRect) = position.SplitRight(modeWidth);
        var sourceRect = beforeMode;
        var thresholdRect = Rect.zero;
        if (mode.enumValueIndex >= 2)
            (sourceRect, thresholdRect) = beforeMode.SplitRight(GUIHelper.PopupWidth(new[] { new GUIContent("0.00") }));
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
            var boolOptions = new[] { "condition.bool.enabled.label".LG(), "condition.bool.disabled.label".LG() };
            var (beforeBoolValue, boolValueRect) = position.SplitRight(GUIHelper.PopupWidth(boolOptions));
            var (nameRect, typeRect) = beforeBoolValue.SplitRight(
                GUIHelper.LocalizedEnumPopupWidth(type, nameof(ParameterType)));
            EditorGUI.PropertyField(nameRect, name, GUIContent.none);
            GUIHelper.DrawLocalizedEnum(typeRect, type, string.Empty, nameof(ParameterType));
            value.boolValue = EditorGUI.Popup(boolValueRect, value.boolValue ? 0 : 1, boolOptions) == 0;
            return;
        }

        var (parameterRect, parameterTypeRect) = position.SplitRight(
            GUIHelper.LocalizedEnumPopupWidth(type, nameof(ParameterType)));
        EditorGUI.PropertyField(parameterRect, name, GUIContent.none);
        GUIHelper.DrawLocalizedEnum(parameterTypeRect, type, string.Empty, nameof(ParameterType));
        position.NewLine();

        if (type.enumValueIndex == (int)ParameterType.Float
            && comparison.enumValueIndex != (int)ComparisonType.GreaterThan
            && comparison.enumValueIndex != (int)ComparisonType.LessThan)
            comparison.enumValueIndex = (int)ComparisonType.GreaterThan;

        var (valueRect, comparisonRect) = position.SplitRight(
            GUIHelper.LocalizedEnumPopupWidth(comparison, nameof(ComparisonType)));
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
