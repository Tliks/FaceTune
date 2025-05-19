namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public sealed class HandGestureConditionComponent : FaceTuneTagComponent, IConditionProvider
    {
        internal const string ComponentName = "FT Hand Gesture Condition";
        internal const string MenuPath = FaceTune + "/" + Condition + "/" + ComponentName;

        public Hand Hand = Hand.Left;
        public BoolComparisonType ComparisonType = BoolComparisonType.Equal;
        public HandGesture HandGesture = HandGesture.Fist;

        Condition? IConditionProvider.ToCondition()
        {
            return new HandGestureCondition(Hand, ComparisonType, HandGesture);
        }
    }
}
