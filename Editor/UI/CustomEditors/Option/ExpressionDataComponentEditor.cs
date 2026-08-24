namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(ExpressionDataComponent))]
internal sealed class ExpressionDataComponentEditor : FaceTuneSectionEditorBase<ExpressionDataComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateExpressionSection(), CreateAdditionalAnimationsSection() };

    private FaceTuneSection CreateExpressionSection()
        => CreateSection(
            "expression.section.label",
            new FacialDataSectionDrawer(
                serializedObject,
                nameof(ExpressionDataComponent.FacialBlendShapesReference),
                nameof(ExpressionDataComponent.FacialBlendShapes)),
            defaultExpanded: true);

    private FaceTuneSection CreateAdditionalAnimationsSection()
        => CreateSection(
            "expression.additionalAnimations.section.label",
            new NonFacialAnimationDataSectionDrawer(
                serializedObject,
                nameof(ExpressionDataComponent.NonFacialAnimationsReference),
                nameof(ExpressionDataComponent.NonFacialAnimations)),
            defaultExpanded: false);
}
