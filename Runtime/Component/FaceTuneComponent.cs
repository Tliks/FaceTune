using nadena.dev.modular_avatar.core;

namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(BaseMenuPath  + "/" + ComponentName)]
    internal class FaceTuneComponent : FaceTuneTagComponent, IHasObjectReferences, IExpressionDataSource
    {
        internal const string ComponentName = FaceTuneConstants.Name;

        public Condition Condition = new();

        public ExpressionSettings ExpressionSettings = new();
        public FacialSettings FacialSettings = new();
        
        public ComponentReferenceMode DataReferenceMode = ComponentReferenceMode.Direct;
        public AvatarObjectReference DataReference = new();
        public ExpressionData Data = new();

        [Obsolete] public bool EnableRealTimePreview = false;

        ComponentReferenceMode IExpressionDataSource.DataReferenceMode => DataReferenceMode;
        AvatarObjectReference IExpressionDataSource.DataReference => DataReference;
        ExpressionData IExpressionDataSource.Data => Data;

        public void ResolveReferences() => DataReference.Get(this);
    }
}