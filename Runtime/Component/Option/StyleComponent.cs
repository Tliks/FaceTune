
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class StyleComponent : FaceTuneTagComponent, IHasExpressionData
    {
        internal const string ComponentName = ComponentNamePrefix + "Style";

        public ExpressionData Data = new();

        public bool ApplyToRenderer = false;

        [Obsolete] public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

        ExpressionData IHasExpressionData.Data => Data;

        public void ResolveReferences() => Data.ResolveReferences(this);
    }
}