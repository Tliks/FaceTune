using nadena.dev.modular_avatar.core;

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
    ComponentReferenceMode DataReferenceMode { get; }
    AvatarObjectReference DataReference { get; }
    ExpressionData Data { get; }
}
