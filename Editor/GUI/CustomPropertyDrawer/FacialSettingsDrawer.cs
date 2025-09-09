namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(FacialSettings))]
internal class FacialSettingsDrawer : PropertyDrawer
{
    private const float HeightMargin = 2;

    private readonly LocalizedPopup _allowLipSyncPopup;
    private readonly LocalizedPopup _allowEyeBlinkPopup;

    public FacialSettingsDrawer()
    {
        _allowLipSyncPopup = new LocalizedPopup("FacialSettings:prop:AllowLipSync", typeof(TrackingPermission));
        _allowEyeBlinkPopup = new LocalizedPopup("FacialSettings:prop:AllowEyeBlink", typeof(TrackingPermission));
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var propAllowLipSync = property.FindPropertyRelative(FacialSettings.AllowLipSyncPropName);
        var propAllowEyeBlink = property.FindPropertyRelative(FacialSettings.AllowEyeBlinkPropName);
        var propEnableBlending = property.FindPropertyRelative(FacialSettings.EnableBlendingPropName);

        position.height = EditorGUIUtility.singleLineHeight;
        _allowLipSyncPopup.Field(position, propAllowLipSync);
        position.y += EditorGUIUtility.singleLineHeight + HeightMargin;
        _allowEyeBlinkPopup.Field(position, propAllowEyeBlink);
        position.y += EditorGUIUtility.singleLineHeight + HeightMargin;
        LocalizedUI.PropertyField(position, propEnableBlending, "FacialSettings:prop:EnableBlending");

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 3 + HeightMargin * 2;
    }
}