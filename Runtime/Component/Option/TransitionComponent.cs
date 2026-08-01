namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class TransitionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "Transition";

        public const float DefaultDurationSeconds = 0.1f;

        public float DurationSeconds = DefaultDurationSeconds;
    }
}
