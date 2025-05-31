namespace com.aoyon.facetune;

internal interface IExpressionProvider
{
    Expression? ToExpression(FacialExpression defaultExpression, IOberveContext observeContext);
}

public abstract class ExpressionComponentBase : FaceTuneTagComponent // IExpressionProviderを実装
{
}

public abstract class FacialExpressionComponentBase : ExpressionComponentBase
{
    public TrackingPermission AllowEyeBlink = TrackingPermission.Disallow;
    public TrackingPermission AllowLipSync = TrackingPermission.Allow;
}