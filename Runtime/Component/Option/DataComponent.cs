
namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class DataComponent : FaceTuneTagComponent, IHasExpressionData
    {
        internal const string ComponentName = ComponentNamePrefix + "Data";

        public ExpressionData Data = CreateDefaultData();

        // AnimationClip
        [Obsolete] public AnimationClip? Clip = null;
        [Obsolete] public ClipImportOption ClipOption = ClipImportOption.NonZero;

        // Manual
        [Obsolete] public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

        internal static ExpressionData CreateDefaultData() => new();

        ExpressionData IHasExpressionData.Data => Data;

    }
}