namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(BaseMenuPath  + "/" + ComponentName)]
    internal class SettingsComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Settings";

        public AvatarSettings Settings = AvatarSettings.Default;

        public void ResolveReferences() => Settings.FaceObjectReference.Get(this);
    }
}