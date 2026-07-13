
namespace Aoyon.FaceTune;

internal interface IHasObjectReferences
{
    void ResolveReferences();
}

internal interface IHasConditions
{
    IEnumerable<Condition> Conditions { get; }
}

internal interface IExpressionDataSource
{
    AvatarObjectReference DataReference { get; }
    ExpressionData Data { get; }
}
