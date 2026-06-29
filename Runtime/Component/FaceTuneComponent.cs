namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(BaseMenuPath  + "/" + ComponentName)]
    internal class FaceTuneComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = FaceTuneConstants.Name;

        public Condition Condition = new();
        public ExpressionSettings ExpressionSettings = new();
        public FacialSettings FacialSettings = new();
        public ExpressionData Data = new();

        [Obsolete] public bool EnableRealTimePreview = false;

    }
}