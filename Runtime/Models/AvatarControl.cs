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
