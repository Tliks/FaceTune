
namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class ExpressionDataComponent : FaceTuneTagComponent,
        IReferenceableExpressionSettings<FacialBlendShapeDataSource>
    {
        internal const string ComponentName = ComponentNamePrefix + "Expression Data";

        // 親Expressionへ、hierarchyの並び順どおりに追加する。
        public FacialBlendShapeDataSource FacialBlendShapes = new();

        FacialBlendShapeDataSource? IReferenceableExpressionSettings<FacialBlendShapeDataSource>.SettingsSource
            => FacialBlendShapes;
    }
}