#pragma warning disable CS0618

namespace Aoyon.FaceTune;

[DisallowMultipleComponent]
[Obsolete("Legacy serialized data retained only for migration.")]
internal class LegacyAdvancedLipSyncComponent : LegacyFaceTuneTagComponent
{
    public AdvancedLipSyncSettings AdvancedLipSyncSettings = new();
}

#pragma warning restore CS0618
