using nadena.dev.modular_avatar.core;

namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(BaseMenuPath  + "/" + ComponentName)]
    internal class StyleComponent : FaceTuneTagComponent, IHasObjectReferences, IExpressionDataSource
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Style";

        public ComponentReferenceMode DataReferenceMode = ComponentReferenceMode.Direct;
        public AvatarObjectReference DataReference = new();
        public ExpressionData Data = new();

        public bool ApplyToRenderer = false;

        [Obsolete] public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

        ComponentReferenceMode IExpressionDataSource.DataReferenceMode => DataReferenceMode;
        AvatarObjectReference IExpressionDataSource.DataReference => DataReference;
        ExpressionData IExpressionDataSource.Data => Data;

        public void ResolveReferences() => DataReference.Get(this);
    }
}