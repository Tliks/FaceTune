using nadena.dev.modular_avatar.core;

namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(BaseMenuPath  + "/" + ComponentName)]
    internal class FaceTuneComponent : FaceTuneTagComponent, IHasObjectReferences, IExpressionDataSource, IHasConditions
    {
        internal const string ComponentName = FaceTuneConstants.Name;

        public bool ConditionEnabled = false;
        public Condition Condition = new(){ Always = false, Cases = new List<ConditionCase>() { new() } };

        public bool DirectMenuEnabled = false;
        public DirectMenuSettings DirectMenuSettings = new();

        public ExpressionSettings ExpressionSettings = new();
        public FacialSettings FacialSettings = new();
        
        public ComponentReferenceMode DataReferenceMode = ComponentReferenceMode.Direct;
        public AvatarObjectReference DataReference = new();
        public ExpressionData Data = new();

        [Obsolete] public bool EnableRealTimePreview = false;

        ComponentReferenceMode IExpressionDataSource.DataReferenceMode => DataReferenceMode;
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