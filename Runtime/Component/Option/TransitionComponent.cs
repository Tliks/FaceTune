namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class TransitionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "Transition";

        public float DurationSeconds = DefaultDurationSeconds;

        public const float DefaultDurationSeconds = 0.1f;
    }
}
