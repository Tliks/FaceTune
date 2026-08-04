namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(ConditionComponent))]
internal sealed class ConditionComponentEditor : FaceTuneSectionEditor<ConditionComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreatePropertySection("condition.section.label".LG(), nameof(ConditionComponent.Condition)) };
}
