namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class AvatarControlComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "Avatar Control";

        public enum Kind
        {
            LockFacial = 0,
            DisableEyeBlink = 10,
            DisableLipSync = 20,
            SupportMMD = 30
        }

        public Kind ControlKind;

        // SupportMMD
        public MMDSupportSettings MMD = new();

        // for All kind
        public ConditionSelection Condition = new();

#region Defaults

        private void Reset()
        {
            Condition = CreateDefaultCondition();
        }

        internal static ConditionSelection CreateDefaultCondition()
            => new()
            {
                Condition = new Condition(
                    ConditionCase.From(new MenuCondition()))
            };

#endregion
    }
}