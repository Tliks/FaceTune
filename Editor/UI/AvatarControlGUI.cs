namespace Aoyon.FaceTune.Gui;

internal sealed class AvatarControlSectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _kind;
    private readonly SerializedProperty _mmd;

    public AvatarControlSectionDrawer(SerializedObject serializedObject)
    {
        _kind = serializedObject.FindProperty(nameof(AvatarControlComponent.ControlKind));
        _mmd = serializedObject.FindProperty(nameof(AvatarControlComponent.MMD));
    }

    public float GetHeight()
        => GUIHelper.LineHeight
         + (ShowsMmd ? GUIHelper.VerticalSpacing + EditorGUI.GetPropertyHeight(_mmd, GUIContent.none, true) : 0f);

    public void Draw(Rect position)
    {
        position.SetSingleHeight();
        GUIHelper.LocalizedEnumPopup(position, _kind, "avatarControl.kind.label", new[]
        {
            "avatarControl.kind.lockFacial.label",
            "avatarControl.kind.disableEyeBlink.label",
            "avatarControl.kind.disableLipSync.label",
            "avatarControl.kind.supportMmd.label"
        });
        if (!ShowsMmd) return;
        position.NewLine();
        position.height = EditorGUI.GetPropertyHeight(_mmd, GUIContent.none, true);
        EditorGUI.PropertyField(position, _mmd, GUIContent.none, true);
    }

    private bool ShowsMmd => _kind.hasMultipleDifferentValues || _kind.intValue == (int)AvatarControlComponent.Kind.SupportMMD;
}
