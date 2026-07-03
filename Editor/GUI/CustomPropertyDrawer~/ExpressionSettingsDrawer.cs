namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(ExpressionSettings))]
internal class ExpressionSettingsDrawer : PropertyDrawer
{
    private const float HeightMargin = 2;

    private static readonly string[] _motionTimeParameterPresets = new string[]
    {
        "GestureLeftWeight",
        "GestureRightWeight",
        "Custom",
    };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var currentPosition = position;
        
        var loopTimeProp = property.FindPropertyRelative(ExpressionSettings.LoopTimePropName);
        LocalizedUI.PropertyField(currentPosition, loopTimeProp, "ExpressionSettings:prop:LoopTime");
        currentPosition.y += EditorGUIUtility.singleLineHeight + HeightMargin;

        var lineHeight = EditorGUIUtility.singleLineHeight;
        var spacing = 5f;

        var totalUsableWidth = position.width;
        var popupDesiredWidth = totalUsableWidth * 0.3f;
        var propertyDesiredWidth = totalUsableWidth - popupDesiredWidth - spacing;

        var motionTimeParameterNameRect = new Rect(currentPosition.x, currentPosition.y, propertyDesiredWidth, lineHeight);
        var motionTimeParameterNamePopupRect = new Rect(currentPosition.x + propertyDesiredWidth + spacing, currentPosition.y, popupDesiredWidth, lineHeight);

        GUI.enabled = !loopTimeProp.boolValue;
        var motionTimeParameterNameProp = property.FindPropertyRelative(ExpressionSettings.MotionTimeParameterNamePropName);

        LocalizedUI.PropertyField(motionTimeParameterNameRect, motionTimeParameterNameProp, "ExpressionSettings:prop:MotionTimeParameterName");

        // If the current value is not found in presets OR is empty, select "Custom"
        var currentMotionTimeParameterName = motionTimeParameterNameProp.stringValue;
        var initialPopupSelectedIndex = Array.IndexOf(_motionTimeParameterPresets, currentMotionTimeParameterName);
        if (initialPopupSelectedIndex == -1 || string.IsNullOrEmpty(currentMotionTimeParameterName))
        {
            initialPopupSelectedIndex = 2;
        }
        var newPopupSelectedIndex = EditorGUI.Popup(motionTimeParameterNamePopupRect, initialPopupSelectedIndex, _motionTimeParameterPresets);
        if (newPopupSelectedIndex != initialPopupSelectedIndex)
        {
            if (newPopupSelectedIndex == 2)
            {
                motionTimeParameterNameProp.stringValue = string.Empty;
            }
            else
            {
                motionTimeParameterNameProp.stringValue = _motionTimeParameterPresets[newPopupSelectedIndex];
            }
        }

        GUI.enabled = true;
        
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 2 + HeightMargin * 1;
    }
}