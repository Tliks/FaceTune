
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class EyeBlinkComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = ComponentNamePrefix + "EyeBlink";

        public ComponentReferenceMode ReferenceMode = ComponentReferenceMode.Direct;
        public AvatarObjectReference Reference = new();
        public AdvancedEyeBlinkSettings AdvancedEyeBlinkSettings = new();

        void IHasObjectReferences.ResolveReferences() => Reference.Get(this);
    }
}