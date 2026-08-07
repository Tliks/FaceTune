
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class StyleComponent : FaceTuneTagComponent, IHasExpressionData
    {
        internal const string ComponentName = ComponentNamePrefix + "Style";

        public ExpressionData Data = CreateDefaultData();

        [ToggleLeft]
        public bool ApplyToRenderer = DefaultApplyToRenderer;

        [Obsolete] public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

        public const bool DefaultApplyToRenderer = false;
        internal static ExpressionData CreateDefaultData() => new();

        ExpressionData IHasExpressionData.Data => Data;

    }
}