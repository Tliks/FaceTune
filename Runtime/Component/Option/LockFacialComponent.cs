namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class LockFacialComponent : FaceTuneTagComponent, IHasSingleConditions
    {
        internal const string ComponentName = ComponentNamePrefix + "Lock Facial";
        
        public SingleConditionBase LockWhen = SingleConditionBase.Menu();

        IEnumerable<SingleConditionBase> IHasSingleConditions.SingleConditions => new[] { LockWhen };
    }
}