namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class LockFacialComponent : FaceTuneTagComponent, IHasConditions
    {
        internal const string ComponentName = ComponentNamePrefix + "Lock Facial";
        
        public Condition LockWhen = CreateDefaultLockWhen();

        internal static Condition CreateDefaultLockWhen() => new(ConditionCase.From(new MenuCondition()));

        IEnumerable<Condition> IHasConditions.Conditions => new[] { LockWhen };
    }
}