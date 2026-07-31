
namespace Aoyon.FaceTune;

internal interface IHasObjectReferences
{
    void ResolveReferences();
}

internal interface IHasConditions
{
    IEnumerable<Condition> Conditions { get; }
}

internal interface IHasExpressionData : IHasObjectReferences
{
    ExpressionData Data { get; }
}
