namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public sealed class ParameterConditionComponent : FaceTuneTagComponent, IConditionProvider
    {
        internal const string ComponentName = "FT Parameter Condition";
        internal const string MenuPath = FaceTuneTagComponent.FTName + "/" + ComponentName;

        public Parameter Parameter;

        Condition IConditionProvider.ToCondition()
        {
            return new Condition(Parameter);
        }
    }
}