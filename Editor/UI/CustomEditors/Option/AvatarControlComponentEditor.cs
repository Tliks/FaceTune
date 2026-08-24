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
            new PropertiesSectionDrawer.Entry(serializedObject.FindProperty(nameof(AvatarControlComponent.Condition)), null)), false);
}
