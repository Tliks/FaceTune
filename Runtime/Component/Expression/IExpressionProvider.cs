namespace com.aoyon.facetune;

internal interface IExpressionProvider
{
    Expression ToExpression(SessionContext sessionContext, IObserveContext observeContext);
}

public abstract class ExpressionComponentBase : FaceTuneTagComponent // IExpressionProviderを実装
{
}