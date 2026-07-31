namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class TransitionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "Transition";

        public float DurationSeconds = 0.1f;
    }
}
