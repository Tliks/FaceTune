namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class PatternComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Pattern";
        internal const string MenuPath = BasePath + "/" + Legacy + "/" + ComponentName;
        
        internal ExpressionPattern? GetPattern(AvatarContext avatarContext)
        {
            var expressionWithConditions = gameObject.GetComponentsInChildren<FaceTuneComponent>(true)
                .SelectMany(c => c.GetExpressionWithConditions(avatarContext))
                .ToList();
            if (expressionWithConditions.Count == 0) return null;
            return new ExpressionPattern(expressionWithConditions);
        }
    }
}
