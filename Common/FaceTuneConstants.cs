namespace Aoyon.FaceTune;

internal static class FaceTuneConstants
{
    public const string Name = "FaceTune";
    public const string QualifiedName = "aoyon.facetune";

    public const string ComponentPrefix = Name;
    public const string ParameterPrefix = Name;

    public const string AnimatedBlendShapePrefix = "blendShape.";

    // Public parameters
    public const string ForceDisableEyeBlinkParameter = $"{ParameterPrefix}/ForceDisableEyeBlink"; // bool
    public const string ForceDisableLipSyncParameter = $"{ParameterPrefix}/ForceDisableLipSync"; // bool
    public const string FixFacialParameter = $"{ParameterPrefix}/FixFacial"; // bool
}