namespace Aoyon.FaceTune;

[Serializable]
internal sealed class DisableEyeBlinkControl
{
    public Condition DisableWhen = new();
}

[Serializable]
internal sealed class DisableLipSyncControl
{
    public Condition DisableWhen = new();
}

[Serializable]
internal sealed class LockFacialControl
{
    public Condition LockWhen = new();
}

[Serializable]
internal sealed class MmdSupportControl
{
    public MmdSupportSettings Settings = new();
    public Condition DisableWhen = new();
}

internal enum MmdDisableMode { Auto, DisableFx, DisableLayer }

[Serializable]
internal class MmdSupportSettings
{
    public List<string> ExplicitMmdBlendShapeNames = new();
    public MmdDisableMode DisableMode = MmdDisableMode.Auto;
}
