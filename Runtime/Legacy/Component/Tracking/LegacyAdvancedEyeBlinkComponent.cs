#pragma warning disable CS0618

namespace Aoyon.FaceTune;

[DisallowMultipleComponent]
[Obsolete("Legacy serialized data retained only for migration.")]
internal class LegacyAdvancedEyeBlinkComponent : LegacyFaceTuneTagComponent
{
    public AdvancedEyeBlinkSettings AdvancedEyeBlinkSettings = new();
}

#pragma warning restore CS0618
