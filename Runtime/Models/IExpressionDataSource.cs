using nadena.dev.modular_avatar.core;

namespace Aoyon.FaceTune;

internal interface IExpressionDataSource
{
    ComponentReferenceMode DataReferenceMode { get; }
    AvatarObjectReference DataReference { get; }
    ExpressionData Data { get; }
}
