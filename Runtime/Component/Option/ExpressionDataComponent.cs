
namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class ExpressionDataComponent : FaceTuneTagComponent,
        IReferenceableExpressionSettings<FacialBlendShapeData>,
        IReferenceableExpressionSettings<NonFacialAnimationData>
    {
        internal const string ComponentName = ComponentNamePrefix + "Expression Data";

        // 親Expressionへ、hierarchyの並び順どおりに追加する。
        public SettingsReference FacialBlendShapesReference = new();
        public FacialBlendShapeData FacialBlendShapes = new();

        public SettingsReference NonFacialAnimationsReference = new();
        public NonFacialAnimationData NonFacialAnimations = new();

        ReferenceableExpressionSettings<FacialBlendShapeData> IReferenceableExpressionSettings<FacialBlendShapeData>.Settings
            => new(true, FacialBlendShapesReference.Mode, FacialBlendShapesReference.Source, FacialBlendShapes);

        ReferenceableExpressionSettings<NonFacialAnimationData> IReferenceableExpressionSettings<NonFacialAnimationData>.Settings
            => new(
                true,
                NonFacialAnimationsReference.Mode,
                NonFacialAnimationsReference.Source,
                NonFacialAnimations);
    }
}