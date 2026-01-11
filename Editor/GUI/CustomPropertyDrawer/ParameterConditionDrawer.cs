namespace Aoyon.FaceTune.Gui;

/*
using Components;

[CustomPropertyDrawer(typeof(ParameterCondition))]
internal class ParameterConditionDrawer : PropertyDrawer
{
    private readonly LocalizedPopup _parameterTypePopup;
    private readonly LocalizedPopup _comparisonTypePopup;
    private readonly LocalizedPopup _floatComparisonTypePopup;

    public ParameterConditionDrawer()
    {
        _parameterTypePopup = new LocalizedPopup(null, typeof(ParameterType).GetEnumNames().Select(k => $"ParameterType:enum:{k}"));
        _comparisonTypePopup = new LocalizedPopup(null, typeof(ComparisonType).GetEnumNames().Select(k => $"ComparisonType:enum:{k}"));
        _floatComparisonTypePopup = new LocalizedPopup(null, new[] { 
            $"ComparisonType:enum:{nameof(ComparisonType.GreaterThan)}", 
            $"ComparisonType:enum:{nameof(ComparisonType.LessThan)}" 
        });
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var nameProp = property.FindPropertyRelative(ParameterCondition.ParameterNamePropName);
        var typeProp = property.FindPropertyRelative(ParameterCondition.ParameterTypePropName);
        var compProp = property.FindPropertyRelative(ParameterCondition.ComparisonTypePropName);
        
        var paramType = (ParameterType)typeProp.enumValueIndex;
        var valProp = paramType switch {
            ParameterType.Int => property.FindPropertyRelative(ParameterCondition.IntValuePropName),
            ParameterType.Float => property.FindPropertyRelative(ParameterCondition.FloatValuePropName),
            ParameterType.Bool => property.FindPropertyRelative(ParameterCondition.BoolValuePropName),
            _ => null
        };

        // Floatの制約
        if (paramType == ParameterType.Float && (ComparisonType)compProp.enumValueIndex is not (ComparisonType.GreaterThan or ComparisonType.LessThan)) {
            compProp.enumValueIndex = (int)ComparisonType.GreaterThan;
        }

        // 翻訳取得
        if (!Localization.TryGetLocalizedString("ParameterCondition:label:Connector1", out var c1)) c1 = "";
        if (!Localization.TryGetLocalizedString("ParameterCondition:label:Connector2", out var c2)) c2 = "";
        if (!Localization.TryGetLocalizedString("ParameterCondition:label:Order", out var orderStr)) orderStr = "0,1,4,3,2";

        var order = orderStr.Split(',').Select(s => s.Trim()).ToArray();
        float spacing = 2f;
        float c1W = string.IsNullOrEmpty(c1) ? 0 : EditorStyles.label.CalcSize(new GUIContent(c1)).x + 3f;
        float c2W = string.IsNullOrEmpty(c2) ? 0 : EditorStyles.label.CalcSize(new GUIContent(c2)).x + 3f;
        
        // 要素数（Boolの場合は1つ減る）
        bool hasComp = paramType != ParameterType.Bool;
        float fieldCount = hasComp ? 4f : 3f;
        float remaining = position.width - (c1W + c2W + spacing * (order.Length - 1));
        float fw = remaining / fieldCount;

        var r = new Rect(position.x, position.y, 0, position.height);

        foreach (var id in order) {
            switch (id) {
                case "0": 
                    r.width = fw * 1.2f; 
                    PlaceholderTextField.TextField(r, nameProp, "ParameterCondition:prop:ParameterName:placeholder".LS());
                    break;
                case "1": r.width = fw * 0.7f; _parameterTypePopup.Field(r, typeProp); break;
                case "2": 
                    if (hasComp) { 
                        r.width = fw * 1.1f; 
                        if (paramType == ParameterType.Float) {
                            int popupIdx = compProp.enumValueIndex == (int)ComparisonType.LessThan ? 1 : 0;
                            int nextIdx = _floatComparisonTypePopup.Draw(r, popupIdx);
                            if (nextIdx != popupIdx) {
                                compProp.enumValueIndex = nextIdx == 1 ? (int)ComparisonType.LessThan : (int)ComparisonType.GreaterThan;
                            }
                        } else {
                            _comparisonTypePopup.Field(r, compProp); 
                        }
                    } else continue; 
                    break;
                case "3": 
                    r.width = fw; 
                    if (paramType == ParameterType.Bool) {
                        int currentIdx = valProp.boolValue ? 0 : 1;
                        int nextIdx = EditorGUI.Popup(r, currentIdx, new[] { "True", "False" });
                        if (nextIdx != currentIdx) valProp.boolValue = nextIdx == 0;
                    } else {
                        EditorGUI.PropertyField(r, valProp, GUIContent.none); 
                    }
                    break;
                case "4": if (c1W > 0) { r.width = c1W; EditorGUI.LabelField(r, c1); } else continue; break;
                case "5": if (c2W > 0) { r.width = c2W; EditorGUI.LabelField(r, c2); } else continue; break;
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

*/