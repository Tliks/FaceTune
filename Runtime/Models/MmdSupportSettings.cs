namespace Aoyon.FaceTune;

internal enum MmdDisableMode
{
    Auto,
    DisableFx,
    DisableLayer
}

[Serializable]
internal class MmdSupportSettings
{
    public List<string> ExplicitMmdBlendShapeNames = new();

    public string DisableParameterName = string.Empty;
    public MmdDisableMode DisableMode = MmdDisableMode.Auto;
}
