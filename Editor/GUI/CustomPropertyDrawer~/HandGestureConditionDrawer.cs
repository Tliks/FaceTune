namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(HandGestureCondition))]
internal class HandGestureConditionDrawer : PropertyDrawer
{
    private readonly LocalizedPopup _handPopup;
    private readonly LocalizedPopup _handGesturePopup;
    private readonly LocalizedPopup _equalityComparisonPopup;

    public HandGestureConditionDrawer()
    {
        _handPopup = new LocalizedPopup(null, typeof(Hand).GetEnumNames().Select(k => $"Hand:enum:{k}"));
        _handGesturePopup = new LocalizedPopup(null, typeof(HandGesture).GetEnumNames().Select(k => $"HandGesture:enum:{k}"));
        _equalityComparisonPopup = new LocalizedPopup(null, typeof(EqualityComparison).GetEnumNames().Select(k => $"EqualityComparison:enum:{k}"));
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var handProp = property.FindPropertyRelative(HandGestureCondition.HandPropName);
        var handGestureProp = property.FindPropertyRelative(HandGestureCondition.HandGesturePropName);
        var equalityComparisonProp = property.FindPropertyRelative(HandGestureCondition.EqualityComparisonPropName);

        if (!Localization.TryGetLocalizedString("HandGestureCondition:label:Connector1", out var conn1))
        {
            conn1 = "";
        }
        if (!Localization.TryGetLocalizedString("HandGestureCondition:label:Connector2", out var conn2))
        {
            conn2 = "";
        }
        if (!Localization.TryGetLocalizedString("HandGestureCondition:label:Order", out var orderStr))
        {
            orderStr = "0,1,2,3,4";
        }

        var order = orderStr.Split(',').Select(s => s.Trim()).ToArray();

        float spacing = 2f;
        float c1W = string.IsNullOrEmpty(conn1) ? 0 : EditorStyles.label.CalcSize(new GUIContent(conn1)).x + 3f;
        float c2W = string.IsNullOrEmpty(conn2) ? 0 : EditorStyles.label.CalcSize(new GUIContent(conn2)).x + 3f;
        
        float totalFixed = c1W + c2W + (spacing * (order.Length - 1));
        float remaining = position.width - totalFixed;
        float fieldW = remaining / 3f;

        var r = new Rect(position.x, position.y, 0, position.height);

        foreach (var id in order)
        {
            switch (id)
            {
                case "0": r.width = fieldW * 0.8f; _handPopup.Field(r, handProp); break;
                case "1": if (c1W > 0) { r.width = c1W; EditorGUI.LabelField(r, conn1); } else continue; break;
                case "2": r.width = fieldW * 1.2f; _handGesturePopup.Field(r, handGestureProp); break;
                case "3": if (c2W > 0) { r.width = c2W; EditorGUI.LabelField(r, conn2); } else continue; break;
                case "4": r.width = fieldW; _equalityComparisonPopup.Field(r, equalityComparisonProp); break;
                default: continue;
            }
            r.x += r.width + spacing;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}
