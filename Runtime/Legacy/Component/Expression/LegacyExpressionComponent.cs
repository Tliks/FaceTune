#pragma warning disable CS0618

namespace Aoyon.FaceTune;

[DisallowMultipleComponent]
[Obsolete("Legacy serialized data retained only for migration.")]
internal class LegacyExpressionComponent : FaceTuneTagComponent
{
    public ExpressionSettings ExpressionSettings = new();
    public FacialSettings FacialSettings = new();
    public bool EnableRealTimePreview;
}

#pragma warning restore CS0618
