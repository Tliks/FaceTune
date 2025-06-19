namespace com.aoyon.facetune;

internal interface IExpressionProvider
{
    Expression ToExpression(SessionContext sessionContext, IObserveContext observeContext);
}

internal interface IHasBlendShapes
{
    internal void GetBlendShapes(ICollection<BlendShape> resultToAdd, BlendShapeSet defaultSet);
}

public abstract class ExpressionComponentBase : FaceTuneTagComponent // IExpressionProviderを実装
{
}