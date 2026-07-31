
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class StyleComponent : FaceTuneTagComponent, IHasObjectReferences, IExpressionDataSource
    {
        internal const string ComponentName = ComponentNamePrefix + "Style";

        public AvatarObjectReference DataReference = new();
        public ExpressionData Data = new();

        public bool ApplyToRenderer = false;

        [Obsolete] public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

        AvatarObjectReference IExpressionDataSource.DataReference => DataReference;
        ExpressionData IExpressionDataSource.Data => Data;

        public void ResolveReferences() => DataReference.Get(this);
    }
}