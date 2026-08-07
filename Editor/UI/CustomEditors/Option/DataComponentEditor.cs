namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(ExpressionDataComponent))]
internal sealed class DataComponentEditor : FaceTuneSectionEditorBase<ExpressionDataComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateExpressionSection() };

    private FaceTuneSection CreateExpressionSection()
        => CreateSection(
            "expression.section.label",
            new ExpressionSectionDrawer(
                serializedObject,
                Component,
                targets.Length),
            defaultExpanded: true);
}
