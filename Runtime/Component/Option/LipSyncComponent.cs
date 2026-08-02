
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class LipSyncComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = ComponentNamePrefix + "LipSync";

        public ComponentReferenceMode ReferenceMode = ComponentReferenceMode.Direct;
        public AvatarObjectReference Reference = new();
        public AdvancedLipSyncSettings AdvancedLipSyncSettings = new();

        void IHasObjectReferences.ResolveReferences() => Reference.Get(this);
    }  
}