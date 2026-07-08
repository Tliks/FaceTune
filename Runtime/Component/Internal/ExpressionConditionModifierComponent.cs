namespace Aoyon.FaceTune;

[DisallowMultipleComponent]
internal class ExpressionConditionModifierComponent : FaceTuneTagComponent, IHasConditions
{
    [NonSerialized] public Condition? OriginalGate = new() { Always = true };
    [NonSerialized] public Condition? AdditionalActivation = new();

    IEnumerable<Condition> IHasConditions.Conditions => new[] { OriginalGate, AdditionalActivation }.OfType<Condition>();
}
