namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class AvatarControlComponent : FaceTuneTagComponent, IHasConditions
    {
        internal const string ComponentName = ComponentNamePrefix + "Avatar Control";

        public enum Kind
        {
            LockFacial = 0,
            DisableEyeBlink = 10,
            DisableLipSync = 20,
            SupportMMD = 30
        }

        public Kind ControlKind = Kind.LockFacial;

        // SupportMMD
        public MMDSupportSettings MMD = new();

        // for All kind
        public ConditionSelection Condition = CreateDefaultCondition();

        internal static ConditionSelection CreateDefaultCondition()
            => new()
            {
                Condition = new Condition(
                    ConditionCase.From(new MenuCondition()))
            };

        IEnumerable<Condition> IHasConditions.Conditions
            => Condition.Mode == ConditionSelection.Kind.Conditional
                ? new[] { Condition.Condition }
                : Array.Empty<Condition>();
    }
}