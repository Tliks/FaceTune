namespace com.aoyon.facetune.ui;

[CustomPropertyDrawer(typeof(FacialSettings))]
internal class FacialSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var propAllowLipSync = property.FindPropertyRelative(FacialSettings.AllowLipSyncPropName);
        var propAllowEyeBlink = property.FindPropertyRelative(FacialSettings.AllowEyeBlinkPropName);
        var propBlendingPermission = property.FindPropertyRelative(FacialSettings.BlendingPermissionPropName);

        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, propAllowLipSync);
        position.y += EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, propAllowEyeBlink);
        position.y += EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, propBlendingPermission);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 3;
    }
}