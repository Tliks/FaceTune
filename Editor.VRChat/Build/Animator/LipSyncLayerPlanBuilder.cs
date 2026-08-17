using Aoyon.FaceTune.Build;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal sealed class LipSyncLayerPlanBuilder
{
    private const string VoiceParameterName = "Voice";
    private const float VoiceThreshold = 0.01f;

    private readonly AapProtocol _aap;
    private readonly DnfCondition? _disableWhen;
    private readonly bool _useTrackingControl;

    public LipSyncLayerPlanBuilder(
        AapProtocol aap,
        DnfCondition? disableWhen,
        bool useTrackingControl)
    {
        _aap = aap;
        _disableWhen = useTrackingControl ? disableWhen : null;
        _useTrackingControl = useTrackingControl;
    }

    public LipSyncLayerPlan? Build(DnfCondition? mmdPlaybackWhen)
    {
        if (!_aap.ControlsLipSync && _disableWhen == null)
        {
            return null;
        }

        var enabled = _aap.ControlsLipSync
            ? _aap.LipSyncEnabledWhen
            : DnfCondition.Always;
        if (_disableWhen != null)
        {
            enabled = enabled.And(_disableWhen.Complement());
        }

        var cancellers = _aap.LipSyncCancellerModes
            .Select((entry, index) => new LipSyncCancellerPlan(
                $"Canceller {index + 1}",
                enabled.And(_aap.LipSyncModeIs(entry.Mode)),
                new BlendShapeWeightSet(entry.Settings.CancellerBlendShapes).ToArray()))
            .ToArray();
        var parameters = new Dictionary<string, PlanParameter>();
        _aap.CollectLipSyncParameters(parameters);
        if (cancellers.Length > 0)
        {
            parameters.TryAdd(
                VoiceParameterName,
                new PlanParameter(
                    VoiceParameterName,
                    AnimatorControllerParameterType.Float,
                    0f));
        }
        if (_disableWhen != null)
        {
            AnimatorHelper.CollectConditionParameters(parameters, _disableWhen);
        }
        if (mmdPlaybackWhen != null)
        {
            AnimatorHelper.CollectConditionParameters(parameters, mmdPlaybackWhen);
        }

        var voiceActiveWhen = VoiceActiveWhen();
        return new LipSyncLayerPlan(
            "Lip Sync",
            _useTrackingControl,
            DnfCondition.Always,
            enabled.Complement(),
            enabled,
            voiceActiveWhen,
            voiceActiveWhen.Complement(),
            mmdPlaybackWhen,
            cancellers,
            parameters.Values.ToArray());
    }

    private static DnfCondition VoiceActiveWhen()
    {
        var condition = ParameterCondition.Float(
            VoiceParameterName,
            ComparisonType.GreaterThan,
            VoiceThreshold);
        return DnfCondition.Single(
            AnimatorConditionRule.FromParameterCondition(condition),
            ParameterDomainRegistry.Empty);
    }
}
