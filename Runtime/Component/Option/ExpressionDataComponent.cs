
namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class ExpressionDataComponent : FaceTuneTagComponent,
        IReferenceableExpressionSettings<FacialBlendShapeData>
    {
        internal const string ComponentName = ComponentNamePrefix + "Expression Data";

        // 親Expressionへ、hierarchyの並び順どおりに追加する。
        public FacialBlendShapeDataSource FacialBlendShapes = new();

        ISettingsSource<FacialBlendShapeData>? IReferenceableExpressionSettings<FacialBlendShapeData>.SettingsSource
            => FacialBlendShapes;
    }
}