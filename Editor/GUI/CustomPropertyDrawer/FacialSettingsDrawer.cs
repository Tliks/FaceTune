namespace com.aoyon.facetune.ui;

[CustomPropertyDrawer(typeof(FacialSettings))]
internal class FacialSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var propAllowLipSync = property.FindPropertyRelative(nameof(FacialSettings.AllowLipSync));
        var propAllowEyeBlink = property.FindPropertyRelative(nameof(FacialSettings.AllowEyeBlink));
        var propBlendingPermission = property.FindPropertyRelative(nameof(FacialSettings.BlendingPermission));

        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, propAllowLipSync, new GUIContent("Allow Lip Sync"));
        position.y += EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, propAllowEyeBlink, new GUIContent("Allow Eye Blink"));
        position.y += EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, propBlendingPermission, new GUIContent("Blending Permission"));

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 3;
    }
}