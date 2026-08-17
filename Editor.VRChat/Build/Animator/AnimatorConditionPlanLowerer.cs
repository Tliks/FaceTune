using Aoyon.FaceTune.Build;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal sealed class AnimatorConditionPlanLowerer
{
    private const string AlwaysParameterName = FaceTuneConstants.GeneratedParameterPrefix + "/Always";

    private readonly DnfCondition _alwaysCondition;

    public AnimatorConditionPlanLowerer(ParameterDomainRegistry parameterDomains)
    {
        _alwaysCondition = DnfCondition.Single(
            AnimatorConditionRule.FromParameterCondition(
                ParameterCondition.Bool(AlwaysParameterName, true)),
            parameterDomains);
    }

    public InitialLayerPlan Lower(InitialLayerPlan layer)
    {
        var requiresAlwaysParameter = false;
        DnfCondition LowerCondition(DnfCondition condition)
        {
            if (!condition.IsAlways) return condition;
            requiresAlwaysParameter = true;
            return _alwaysCondition;
        }

        return layer with
        {
            DefaultState = layer.DefaultState with { When = LowerCondition(layer.DefaultState.When) },
            MmdPlaybackState = layer.MmdPlaybackState == null
                ? null
                : layer.MmdPlaybackState with { When = LowerCondition(layer.MmdPlaybackState.When) },
            Parameters = AddAlwaysParameterIfRequired(layer.Parameters, requiresAlwaysParameter)
        };
    }

    public OutputUnitPlan Lower(OutputUnitPlan unit)
    {
        var requiresAlwaysParameter = false;
        DnfCondition LowerCondition(DnfCondition condition)
        {
            if (!condition.IsAlways) return condition;
            requiresAlwaysParameter = true;
            return _alwaysCondition;
        }

        DnfCondition? LowerOptionalCondition(DnfCondition? condition)
            => condition == null ? null : LowerCondition(condition);

        var layers = unit.ExpressionLayers.Select(layer => layer with
        {
            InitialExitWhen = LowerCondition(layer.InitialExitWhen),
            PassThroughWhen = LowerOptionalCondition(layer.PassThroughWhen),
            MmdPlaybackWhen = LowerOptionalCondition(layer.MmdPlaybackWhen),
            States = layer.States.Select(state => state with
            {
                EnterWhen = LowerCondition(state.EnterWhen),
                ExitWhen = LowerCondition(state.ExitWhen)
            }).ToArray()
        }).ToArray();

        return unit with
        {
            ExpressionLayers = layers,
            Parameters = AddAlwaysParameterIfRequired(unit.Parameters, requiresAlwaysParameter)
        };
    }

    public EyeBlinkLayerPlan? Lower(EyeBlinkLayerPlan? layer)
    {
        if (layer == null) return null;

        var requiresAlwaysParameter = false;
        DnfCondition LowerCondition(DnfCondition condition)
        {
            if (!condition.IsAlways) return condition;
            requiresAlwaysParameter = true;
            return _alwaysCondition;
        }

        return layer with
        {
            InitialExitWhen = LowerCondition(layer.InitialExitWhen),
            DisabledWhen = LowerCondition(layer.DisabledWhen),
            BuiltInWhen = LowerCondition(layer.BuiltInWhen),
            AnimationWhen = LowerCondition(layer.AnimationWhen),
            MmdPlaybackWhen = layer.MmdPlaybackWhen == null
                ? null
                : LowerCondition(layer.MmdPlaybackWhen),
            Animations = layer.Animations.Select(animation => animation with
            {
                When = LowerCondition(animation.When)
            }).ToArray(),
            Parameters = AddAlwaysParameterIfRequired(layer.Parameters, requiresAlwaysParameter)
        };
    }

    public LipSyncLayerPlan? Lower(LipSyncLayerPlan? layer)
    {
        if (layer == null) return null;

        var requiresAlwaysParameter = false;
        DnfCondition LowerCondition(DnfCondition condition)
        {
            if (!condition.IsAlways) return condition;
            requiresAlwaysParameter = true;
            return _alwaysCondition;
        }

        return layer with
        {
            InitialExitWhen = LowerCondition(layer.InitialExitWhen),
            DisabledWhen = LowerCondition(layer.DisabledWhen),
            BuiltInWhen = LowerCondition(layer.BuiltInWhen),
            VoiceActiveWhen = LowerCondition(layer.VoiceActiveWhen),
            VoiceInactiveWhen = LowerCondition(layer.VoiceInactiveWhen),
            MmdPlaybackWhen = layer.MmdPlaybackWhen == null
                ? null
                : LowerCondition(layer.MmdPlaybackWhen),
            Cancellers = layer.Cancellers.Select(canceller => canceller with
            {
                When = LowerCondition(canceller.When)
            }).ToArray(),
            Parameters = AddAlwaysParameterIfRequired(
                layer.Parameters,
                requiresAlwaysParameter)
        };
    }

    private static IReadOnlyList<PlanParameter> AddAlwaysParameterIfRequired(
        IReadOnlyList<PlanParameter> parameters,
        bool required)
    {
        if (!required) return parameters;
        if (parameters.Any(parameter => parameter.Name == AlwaysParameterName))
        {
            throw new InvalidOperationException(
                $"Animator parameter name is reserved by FaceTune: {AlwaysParameterName}");
        }

        return parameters.Append(new PlanParameter(
            AlwaysParameterName,
            AnimatorControllerParameterType.Bool,
            1f)).ToArray();
    }
}
