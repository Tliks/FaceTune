namespace Aoyon.FaceTune;

[DisallowMultipleComponent]
internal class ExpressionConditionModifierComponent : FaceTuneTagComponent, IHasConditions
{
    public Condition OriginalGate = new() { Always = true };
    public Condition AdditionalActivation = new();

    IEnumerable<Condition> IHasConditions.Conditions => new[] { OriginalGate, AdditionalActivation };
}
