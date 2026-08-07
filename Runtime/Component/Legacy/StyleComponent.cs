
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(LegacyMenuPathPrefix + ComponentName)]
    internal class StyleComponent : FaceTuneTagComponent, IHasExpressionData
    {
        internal const string ComponentName = ComponentNamePrefix + "Style";

        public ExpressionData Data = new();

        [ToggleLeft]
        public bool ApplyToRenderer;

        [Obsolete] public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

        ExpressionData IHasExpressionData.Data => Data;

    }
}