namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class AnimationExpressionComponent : ExpressionComponentBase, IExpressionProvider
    {
        internal const string ComponentName = "FT AnimationExpression";
        internal const string MenuPath = FaceTuneTagComponent.FTName + "/" + ComponentName;

        public PathType PathType = PathType.Absolute;
        public AnimationClip? Clip = null;

        Expression? IExpressionProvider.ToExpression(SessionContext context)
        {
            if (Clip == null) return null;
            
            var pathType = PathType;
            if (pathType == PathType.Absolute)
            {
                return new AnimationExpression(Clip, name);
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