#pragma warning disable CS0618

namespace Aoyon.FaceTune;

[Obsolete("Legacy serialized data retained only for migration.")]
internal class LegacyConditionComponent : LegacyFaceTuneTagComponent
{
    public List<LegacyHandGestureCondition> HandGestureConditions = new();
    public List<LegacyParameterCondition> ParameterConditions = new();
}

#pragma warning restore CS0618
