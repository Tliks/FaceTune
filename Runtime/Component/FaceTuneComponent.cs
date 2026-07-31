
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPathPrefix + ComponentName)]
    internal class FaceTuneComponent : FaceTuneTagComponent, IHasObjectReferences, IExpressionDataSource, IHasConditions
    {
        internal const string ComponentName = FaceTuneConstants.Name;

        public bool ConditionEnabled = true;
        public Condition Condition = new(ConditionCase.From(new HandGestureCondition()));

        public bool DirectMenuEnabled = false;
        public DirectMenuSettings DirectMenuSettings = new()
        {
            Icon = new MenuIconSettings { Mode = MenuIconMode.ExpressionPreview }
        };

        public ExpressionSettings ExpressionSettings = new();
        public FacialSettings FacialSettings = new();
        
        public AvatarObjectReference DataReference = new();
        public ExpressionData Data = new();

        public bool EnableRealTimePreview = false;

        AvatarObjectReference IExpressionDataSource.DataReference => DataReference;
        ExpressionData IExpressionDataSource.Data => Data;
        IEnumerable<Condition> IHasConditions.Conditions => new[] { Condition };

        public void ResolveReferences()
        {
            DirectMenuSettings.ResolveReferences(this);
            DataReference.Get(this);
        }
    }
}