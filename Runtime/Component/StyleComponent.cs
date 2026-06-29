namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(BaseMenuPath  + "/" + ComponentName)]
    internal class StyleComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Style";

        public ExpressionData Data = new();

        public bool ApplyToRenderer = false;

        [Obsolete] public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

    }
}