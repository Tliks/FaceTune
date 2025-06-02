namespace com.aoyon.facetune;

internal interface IExpressionProvider
{
    Expression? ToExpression(FacialExpression defaultExpression, IObserveContext observeContext);
}

public abstract class ExpressionComponentBase : FaceTuneTagComponent // IExpressionProviderを実装
{
}