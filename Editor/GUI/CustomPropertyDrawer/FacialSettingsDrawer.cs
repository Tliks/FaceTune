namespace com.aoyon.facetune.ui;

[CustomPropertyDrawer(typeof(FacialSettings))]
internal class FacialSettingsDrawer : PropertyDrawer
{
    private const string AllowLipSyncPropName = "_allowLipSync";
    private const string AllowEyeBlinkPropName = "_allowEyeBlink";
    private const string BlendingPermissionPropName = "_blendingPermission";

    private static readonly GUIContent AllowLipSyncContent = new("Allow Lip Sync");
    private static readonly GUIContent AllowEyeBlinkContent = new("Allow Eye Blink");
    private static readonly GUIContent BlendingPermissionContent = new("Blending Permission");

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var propAllowLipSync = property.FindPropertyRelative(AllowLipSyncPropName);
        var propAllowEyeBlink = property.FindPropertyRelative(AllowEyeBlinkPropName);
        var propBlendingPermission = property.FindPropertyRelative(BlendingPermissionPropName);

        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, propAllowLipSync, AllowLipSyncContent);
        position.y += EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, propAllowEyeBlink, AllowEyeBlinkContent);
        position.y += EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, propBlendingPermission, BlendingPermissionContent);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 3;
    }
}