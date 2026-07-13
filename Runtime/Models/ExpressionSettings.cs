namespace Aoyon.FaceTune;

internal enum MultiFrameMode
{
    Default,
    Loop,
    Trigger,
    Parameter
}

[Serializable]
internal record class ExpressionSettings
{
    [SerializeField] private MultiFrameMode multiFrameMode;
    [SerializeField] private Hand triggerHand = Hand.Left;
    [SerializeField] private string parameterName = string.Empty;

    // Kept only for migration from the former representation.
    [Obsolete, SerializeField] private bool loopTime;
    [Obsolete, SerializeField] private string motionTimeParameterName = string.Empty;

    public MultiFrameMode MultiFrameMode { get => multiFrameMode; init => multiFrameMode = value; }
    public Hand TriggerHand { get => triggerHand; init => triggerHand = value; }
    public string ParameterName { get => parameterName; init => parameterName = value; }

    public bool LoopTime => multiFrameMode == MultiFrameMode.Loop;
    public string MotionTimeParameterName => multiFrameMode == MultiFrameMode.Parameter ? parameterName : string.Empty;

    public const string MultiFrameModePropName = nameof(multiFrameMode);
    public const string TriggerHandPropName = nameof(triggerHand);
    public const string ParameterNamePropName = nameof(parameterName);

}
