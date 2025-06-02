namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class AnimationExpressionComponent : ExpressionComponentBase, IExpressionProvider
    {
        internal const string ComponentName = "FT AnimationExpression";
        internal const string MenuPath = FaceTune + "/" + Expression + "/" + ComponentName;

        public PathType PathType = PathType.Absolute;
        public AnimationClip? Clip = null;

        Expression? IExpressionProvider.ToExpression(FacialExpression defaultExpression, IObserveContext observeContext)
        {            
            var clip = observeContext.Observe(this, c => c.Clip, (a, b) => a == b);
            if (clip == null) return null;
            var pathType = observeContext.Observe(this, c => c.PathType, (a, b) => a == b);
            if (pathType == PathType.Absolute)
            {
                return new AnimationExpression(clip, TrackingPermission.Keep, TrackingPermission.Keep, name);
            }
            else if (pathType == PathType.Relative)
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new Exception();
            }
        }
    }
}