namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(ConditionComponent))]
internal sealed class ConditionComponentEditor : FaceTuneSectionEditorBase<ConditionComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateConditionSection() };

    private FaceTuneSection CreateConditionSection()
        => CreateSection(
            "condition.section.label",
            new PropertiesSectionDrawer(serializedObject.FindProperty(nameof(ConditionComponent.Condition))),
            defaultExpanded: true);
}
