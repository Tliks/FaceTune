using nadena.dev.modular_avatar.core;

namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(BaseMenuPath  + "/" + ComponentName)]
    internal class EyeBlinkComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} EyeBlink";

        public ComponentReferenceMode ReferenceMode = ComponentReferenceMode.Direct;
        public AvatarObjectReference Reference = new();
        public AdvancedEyeBlinkSettings AdvancedEyeBlinkSettings = new();

        public void ResolveReferences() => Reference.Get(this);
    }
}