namespace Aoyon.FaceTune;

/// <summary>MMD再生中に競合する出力を止める設定。</summary>
[Serializable]
internal class MMDSupportSettings
{
    public enum Mode
    {
        Auto = 0,
        DisableFXlayer = 10,
        DisableLayers = 20
    }

    public List<string> ExplicitBlendShapeNames = new();
    public Mode SupportMode = Mode.Auto;
}

/// <summary>AFK再生中に競合する出力を止める設定。</summary>
[Serializable]
internal class AFKSupportSettings
{
    public enum Mode
    {
        DisableFaceTune = 0,
        DisableFXlayer = 10
    }

    public Mode SupportMode = Mode.DisableFaceTune;
}
