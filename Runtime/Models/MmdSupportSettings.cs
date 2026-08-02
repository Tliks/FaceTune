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

    public SingleConditionBase DisableWhen = SingleConditionBase.Menu();
    public MmdDisableMode DisableMode = MmdDisableMode.Auto;
}
