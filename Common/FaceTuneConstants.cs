namespace Aoyon.FaceTune;

internal static class FaceTuneConstants
{
    public const string Name = "FaceTune";
    public const string QualifiedName = "aoyon.facetune";

    public const string ComponentPrefix = Name;
    public const string ParameterPrefix = Name;

    public const string AnimatedBlendShapePrefix = "blendShape.";

    // Public Parameters
    public const string ForceDisableEyeBlinkParameter = $"{ParameterPrefix}/ForceDisableEyeBlink"; // bool
    public const string ForceDisableLipSyncParameter = $"{ParameterPrefix}/ForceDisableLipSync"; // bool

    // Internal Parameters
    public const string TrueParameterName = $"{FaceTuneConstants.ParameterPrefix}/True";
    public const string FalseParameterName = $"{FaceTuneConstants.ParameterPrefix}/False";
}