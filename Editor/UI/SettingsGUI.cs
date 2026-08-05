namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(AvatarSettings))]
internal sealed class AvatarSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUIHelper.DrawProperty(ref position, property.FindPropertyRelative(nameof(AvatarSettings.FaceObjectReference)), "avatarSettings.faceObject.label");
        var blendShapes = property.FindPropertyRelative(nameof(AvatarSettings.ExcludedBlendShapeNames));
        position.height = GUIHelper.GetListHeight(blendShapes);
        GUIHelper.DrawList(position, blendShapes, "avatarSettings.excludedBlendShapes.label".LG());
        position.NewLine();
        GUIHelper.DrawProperty(ref position, property.FindPropertyRelative(nameof(AvatarSettings.ParmaterCompression)), "avatarSettings.parameterCompression.label");
        GUIHelper.DrawProperty(ref position, property.FindPropertyRelative(nameof(AvatarSettings.SupressTrackingControl)), "avatarSettings.suppressTrackingControl.label");
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => GUIHelper.PropertyHeight(property.FindPropertyRelative(nameof(AvatarSettings.FaceObjectReference)))
         + GUIHelper.GetListHeight(property.FindPropertyRelative(nameof(AvatarSettings.ExcludedBlendShapeNames)))
         + GUIHelper.VerticalSpacing
         + GUIHelper.PropertyHeight(property.FindPropertyRelative(nameof(AvatarSettings.ParmaterCompression)))
         + GUIHelper.PropertyHeight(property.FindPropertyRelative(nameof(AvatarSettings.SupressTrackingControl)));

}

[CustomPropertyDrawer(typeof(MmdSupportSettings))]
internal sealed class MmdSupportSettingsDrawer : PropertyDrawer
{
    private static readonly string[] BlendShapeModeKeys =
    {
        "mmdSupport.blendShapes.option.auto",
        "mmdSupport.blendShapes.option.specified"
    };
    private static readonly ReorderableListOptions BlendShapeListOptions = new(
        Header: ReorderableListOptions.HeaderMode.None,
        Controls: ReorderableListOptions.ControlsPlacement.Header,
        NestContent: false);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var disableMode = property.FindPropertyRelative(nameof(MmdSupportSettings.DisableMode));
        GUIHelper.DrawLocalizedEnum(ref position, disableMode, "mmdSupport.disableMode.label", nameof(MmdDisableMode));

        var blendShapes = property.FindPropertyRelative(nameof(MmdSupportSettings.ExplicitMmdBlendShapeNames));
        var selectedMode = blendShapes.arraySize == 0 ? 0 : 1;
        position.height = GUIHelper.LineHeight;
        var nextMode = GUIHelper.LocalizedPopup(
            position,
            selectedMode,
            "mmdSupport.blendShapes.label",
            BlendShapeModeKeys);
        if (nextMode != selectedMode)
        {
            if (nextMode == 0)
                blendShapes.ClearArray();
            else
            {
                blendShapes.InsertArrayElementAtIndex(0);
                blendShapes.GetArrayElementAtIndex(0).stringValue = string.Empty;
            }
        }
        position.NewLine();

        if (nextMode == 0) return;
        position.Indent();
        position.height = GUIHelper.GetListHeight(blendShapes, BlendShapeListOptions);
        GUIHelper.DrawList(position, blendShapes, GUIContent.none, BlendShapeListOptions);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var blendShapes = property.FindPropertyRelative(nameof(MmdSupportSettings.ExplicitMmdBlendShapeNames));
        return (GUIHelper.LineHeight + GUIHelper.VerticalSpacing) * 2f
             + (blendShapes.arraySize == 0 ? 0f : GUIHelper.GetListHeight(blendShapes, BlendShapeListOptions));
    }
}
