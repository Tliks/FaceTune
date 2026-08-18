namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(ExpressionDataComponent))]
internal sealed class ExpressionDataComponentEditor : FaceTuneSectionEditorBase<ExpressionDataComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateExpressionSection() };

    private FaceTuneSection CreateExpressionSection()
        => CreateSection(
            "expression.section.label",
            new FacialDataSectionDrawer(
                serializedObject,
                Component,
                targets.Length,
                nameof(ExpressionDataComponent.FacialBlendShapesReference),
                nameof(ExpressionDataComponent.FacialBlendShapes)),
            defaultExpanded: true);
}
