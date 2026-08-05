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
                serializedObject.FindProperty(nameof(TransitionComponent.DurationSeconds)),
                "transition.duration.label"),
            defaultExpanded: false);
}
