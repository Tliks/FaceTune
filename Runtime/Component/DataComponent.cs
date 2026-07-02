using nadena.dev.modular_avatar.core;

namespace Aoyon.FaceTune
{
    [AddComponentMenu(BaseMenuPath + "/" + ComponentName)]
    internal class DataComponent : FaceTuneTagComponent, IHasObjectReferences, IExpressionDataSource
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Data";

        public ComponentReferenceMode DataReferenceMode = ComponentReferenceMode.Direct;
        public AvatarObjectReference DataReference = new();
        public ExpressionData Data = new();

        // AnimationClip
        [Obsolete] public AnimationClip? Clip = null;
        [Obsolete] public ClipImportOption ClipOption = ClipImportOption.NonZero;

        // Manual
        [Obsolete] public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

        [Obsolete] public bool AllBlendShapeAnimationAsFacial = false;

        ComponentReferenceMode IExpressionDataSource.DataReferenceMode => DataReferenceMode;
        AvatarObjectReference IExpressionDataSource.DataReference => DataReference;
        ExpressionData IExpressionDataSource.Data => Data;

        public void ResolveReferences() => DataReference.Get(this);
    }
}