
namespace Aoyon.FaceTune
{
    [AddComponentMenu(BaseMenuPath + "/" + ComponentName)]
    internal class DataComponent : FaceTuneTagComponent, IHasObjectReferences, IExpressionDataSource
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Data";

        public AvatarObjectReference DataReference = new();
        public ExpressionData Data = new();

        // AnimationClip
        [Obsolete] public AnimationClip? Clip = null;
        [Obsolete] public ClipImportOption ClipOption = ClipImportOption.NonZero;

        // Manual
        [Obsolete] public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

        AvatarObjectReference IExpressionDataSource.DataReference => DataReference;
        ExpressionData IExpressionDataSource.Data => Data;

        public void ResolveReferences() => DataReference.Get(this);
    }
}