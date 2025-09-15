namespace Aoyon.FaceTune
{
    [AddComponentMenu(MenuPath)]
    public class ConditionComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Condition";
        internal const string MenuPath = BasePath + "/" + ExpressionPattern + "/" + ComponentName;

        public bool AsTrueCondition = false;
        public List<HandGestureCondition> HandGestureConditions = new();
        public List<ParameterCondition> ParameterConditions = new();

        internal ICondition ToCondition(AvatarContext avatarContext)
        {
            if (AsTrueCondition)
            {
                return new TrueCondition();
            }
            var andConditions = new List<ICondition>();
            foreach (var handGestureCondition in HandGestureConditions)
            {
                andConditions.Add((handGestureCondition as ISerializableCondition)!.ToCondition(avatarContext));
            }
            foreach (var parameterCondition in ParameterConditions)
            {
                andConditions.Add((parameterCondition as ISerializableCondition)!.ToCondition(avatarContext));
            }
            return new AndCondition(andConditions);
        }
    }
}