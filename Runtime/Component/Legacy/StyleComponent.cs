
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(LegacyMenuPathPrefix + ComponentName)]
    internal class StyleComponent : FaceTuneTagComponent,
        IReferenceableExpressionSettings<FacialBlendShapeData>
    {
        internal const string ComponentName = ComponentNamePrefix + "Style";

        public FacialBlendShapeDataSource Data = new();

        [ToggleLeft]
        public bool ApplyToRenderer;

        [Obsolete] public List<BlendShapeWeightAnimation> BlendShapeAnimations = new();

        ISettingsSource<FacialBlendShapeData>? IReferenceableExpressionSettings<FacialBlendShapeData>.SettingsSource
            => Data;

    }
}