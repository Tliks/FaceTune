namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public sealed class HandGestureConditionComponent : FaceTuneTagComponent, IConditionProvider
    {
        internal const string ComponentName = "FT Hand Gesture Condition";
        internal const string MenuPath = FaceTuneTagComponent.FTName + "/" + ComponentName;

        public Hand Hand = default;
        public HandGesture HandGesture = default;

        Condition IConditionProvider.ToCondition()
        {
            return new Condition(Hand, HandGesture);
        }

    }
}
