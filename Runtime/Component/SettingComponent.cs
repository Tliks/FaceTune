using nadena.dev.modular_avatar.core;

namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(BaseMenuPath  + "/" + ComponentName)]
    internal class SettingsComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Settings";

        public bool OverrideFaceRenderer = false;
        public AvatarObjectReference FaceObjectReference = new();

        public bool OverrideDurationSeconds = false;
        public float DurationSeconds = 0.1f;

        public bool OverrideAllowTrackedBlendShapes = false;
        public bool AllowTrackedBlendShapes = true;

        public void ResolveReferences() => FaceObjectReference?.Get(this);
    }
}