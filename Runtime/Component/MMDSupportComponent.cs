namespace Aoyon.FaceTune
{
    [AddComponentMenu(BaseMenuPath + "/" + ComponentName)]
    internal class MMDSupportComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} MMD Support";

        public MmdSupportSettings Settings = new();
    }
}
