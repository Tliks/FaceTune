namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(LockFacialComponent))]
internal sealed class LockFacialComponentEditor : FaceTuneSectionEditorBase<LockFacialComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateSettingsSection(), CreateConditionSection() };

    private FaceTuneSection CreateSettingsSection()
        => CreateSection(
            "lockFacial.section.label",
            new PropertiesSectionDrawer(),
            defaultExpanded: false);

    private FaceTuneSection CreateConditionSection()
        => CreateSection(
            "condition.section.label",
            new PropertiesSectionDrawer(
                new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(LockFacialComponent.LockWhen)),
                    LockFacialComponent.CreateDefaultLockWhen())),
            defaultExpanded: false);
}
