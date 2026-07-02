using nadena.dev.modular_avatar.core;

namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(BaseMenuPath  + "/" + ComponentName)]
    internal class LipSyncComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} LipSync";

        public ComponentReferenceMode ReferenceMode = ComponentReferenceMode.Direct;
        public AvatarObjectReference Reference = new();
        public AdvancedLipSyncSettings AdvancedLipSyncSettings = new();

        public void ResolveReferences() => Reference.Get(this);
    }  
}