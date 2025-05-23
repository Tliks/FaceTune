namespace com.aoyon.facetune;

internal interface IExpressionProvider
{
    Expression? ToExpression(SessionContext context);
}

public abstract class ExpressionComponentBase : FaceTuneTagComponent
{
}

public abstract class FacialExpressionComponentBase : ExpressionComponentBase
{
    public bool EnableBlending = false;
    public TrackingPermission AllowEyeBlink = TrackingPermission.Disallow;
    public TrackingPermission AllowLipSync = TrackingPermission.Allow;
}