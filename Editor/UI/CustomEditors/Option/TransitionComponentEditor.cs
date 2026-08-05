namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(TransitionComponent))]
internal sealed class TransitionComponentEditor : FaceTuneSectionEditorBase<TransitionComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateTransitionSection() };

    private FaceTuneSection CreateTransitionSection()
        => CreateSection(
            "transition.section.label",
            new PropertiesSectionDrawer(
                new PropertiesSectionDrawer.Entry(
                    serializedObject.FindProperty(nameof(TransitionComponent.DurationSeconds)),
                    TransitionComponent.DefaultDurationSeconds,
                    "transition.duration.label")),
            defaultExpanded: false);
}
