namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(AvatarControlComponent))]
internal sealed class AvatarControlComponentEditor : FaceTuneSectionEditorBase<AvatarControlComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateControlSection(), CreateConditionSection() };

    private FaceTuneSection CreateControlSection()
        => CreateSection("avatarControl.section.label", new AvatarControlSectionDrawer(serializedObject), true);

    private FaceTuneSection CreateConditionSection()
        => CreateSection("expression.condition.section.label", new PropertiesSectionDrawer(
            new PropertiesSectionDrawer.Entry(
                serializedObject.FindProperty(nameof(AvatarControlComponent.Condition)),
                null,
                AvatarControlComponent.CreateDefaultCondition)), false);
}

internal sealed class AvatarControlSectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _kind;
    private readonly SerializedProperty _mmd;

    public AvatarControlSectionDrawer(SerializedObject serializedObject)
    {
        _kind = serializedObject.FindProperty(nameof(AvatarControlComponent.ControlKind));
        _mmd = serializedObject.FindProperty(nameof(AvatarControlComponent.MMD));
        Actions = new SectionActionSet(
            serializedObject,
            new[]
            {
                SectionActionField.From(_kind, () => AvatarControlComponent.DefaultControlKind),
                SectionActionField.From(_mmd, () => new MMDSupportSettings())
            });
    }

    public SectionActionSet Actions { get; }

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

    private bool ShowsMmd => _kind.hasMultipleDifferentValues
                          || _kind.intValue == (int)AvatarControlComponent.Kind.SupportMMD;
}
