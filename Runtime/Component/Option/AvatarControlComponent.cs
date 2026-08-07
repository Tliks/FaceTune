namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class AvatarControlComponent : FaceTuneTagComponent, IHasConditions
    {
        internal const string ComponentName = ComponentNamePrefix + "Avatar Control";

        // Each list is an independent 0..1 slot. Different controls may coexist.
        public List<MmdSupportControl> MmdSupport = new();
        public List<DisableEyeBlinkControl> DisableEyeBlink = new();
        public List<DisableLipSyncControl> DisableLipSync = new();
        public List<LockFacialControl> LockFacial = new();

        IEnumerable<Condition> IHasConditions.Conditions
            => MmdSupport.Select(control => control.DisableWhen)
                .Concat(DisableEyeBlink.Select(control => control.DisableWhen))
                .Concat(DisableLipSync.Select(control => control.DisableWhen))
                .Concat(LockFacial.Select(control => control.LockWhen));
    }
}