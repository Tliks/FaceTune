using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Build.Animator;

internal sealed record class AnimatorBuildPlan(
    InitialLayerPlan InitialLayer,
    IReadOnlyList<OutputUnitPlan> Units,
    TrackingControlLayerPlan? TrackingControlLayer,
    FxControlPlan? FxControl,
    float ExpressionTransitionDurationSeconds)
{
    private const string EyeBlinkControlAap = $"{FaceTuneConstants.ParameterPrefix}/Blink/Control";
    private const string LipSyncControlAap = $"{FaceTuneConstants.ParameterPrefix}/LipSync/Control";
    private const string EyeBlinkAdvancedSelectorAap = $"{FaceTuneConstants.ParameterPrefix}/Blink/Advanced";
    private const string LipSyncAdvancedSelectorAap = $"{FaceTuneConstants.ParameterPrefix}/LipSync/Advanced";

    private const int ControlTrackingIndex = 0;
    private const int ControlAnimationIndex = 1;
    private const int AdvancedNoneIndex = 0;
    private const int AdvancedEnabledIndex = 1;

    public static AnimatorBuildPlan From(
        ExpressionProgram program,
        BuildSettings settings,
        IAnimatorPlatformServices platformServices,
        VirtualControllerContext controllerContext)
    {
        var layerForceInactiveWhen = CreateLayerForceInactiveWhen(settings);
        var units = BuildUnits(program, settings, platformServices, controllerContext, layerForceInactiveWhen);
        return new AnimatorBuildPlan(
            BuildInitialLayer(settings),
            units,
            BuildTrackingControlLayer(settings, units, layerForceInactiveWhen),
            BuildFxControl(settings),
            settings.DurationSeconds);
    }

    private static InitialLayerPlan BuildInitialLayer(BuildSettings settings)
    {
        var avatarContext = settings.AvatarContext;
        var blendShapes = avatarContext.FaceRenderer
            .GetBlendShapeWeights(avatarContext.FaceMesh)
            .Where(shape => !settings.ExcludedBlendShapeNames.Contains(shape.Name))
            .ToArray();
        return new InitialLayerPlan(blendShapes);
    }

    private static IReadOnlyList<OutputUnitPlan> BuildUnits(
        ExpressionProgram program,
        BuildSettings settings,
        IAnimatorPlatformServices platformServices,
        VirtualControllerContext controllerContext,
        DnfCondition? layerForceInactiveWhen)
    {
        var avatarContext = settings.AvatarContext;
        var managedBlendShapeNames = avatarContext.FaceMesh.GetBlendShapeNames()
            .Where(name => !settings.ExcludedBlendShapeNames.Contains(name))
            .ToHashSet();

        var splitIndices = FindExternalOverlapSplitIndices(
            program.Items,
            avatarContext.Root.transform,
            platformServices,
            controllerContext,
            managedBlendShapeNames);

        var units = new List<OutputUnitPlan>();
        var start = 0;
        foreach (var splitIndex in splitIndices.Append(program.Items.Count))
        {
            var expressions = program.Items.Skip(start).Take(splitIndex - start).ToArray();
            if (expressions.Length == 0)
            {
                start = splitIndex;
                continue;
            }

            units.Add(new OutputUnitPlan(
                units.Count,
                expressions[0].SourceTransform,
                BuildExpressionLayers(expressions, settings, layerForceInactiveWhen),
                BuildAdvancedEyeBlinkLayer(expressions, layerForceInactiveWhen),
                BuildAdvancedLipSyncLayer(expressions, layerForceInactiveWhen)));
            start = splitIndex;
        }

        return units;
    }

    private static IReadOnlyList<ExpressionLayerPlan> BuildExpressionLayers(
        IReadOnlyList<ExpressionItem> expressions,
        BuildSettings settings,
        DnfCondition? forceInactiveWhen)
    {
        var layers = new List<ExpressionLayerPlan>();
        for (var index = 0; index < expressions.Count;)
        {
            var expression = expressions[index];
            if (expression.WriteMode == ExpressionWriteMode.Replace)
            {
                var run = new List<ExpressionItem>();
                while (index < expressions.Count && expressions[index].WriteMode == ExpressionWriteMode.Replace)
                {
                    run.Add(expressions[index]);
                    index++;
                }

                layers.Add(BuildExpressionLayer(PackedLayerName(PackedLayerKind.ReplaceRun, run), PackedLayerKind.ReplaceRun, run, settings, forceInactiveWhen));
                continue;
            }

            layers.Add(BuildExpressionLayer(expression.Name, PackedLayerKind.Blend, new[] { expression }, settings, forceInactiveWhen));
            index++;
        }

        return layers;
    }

    private static ExpressionLayerPlan BuildExpressionLayer(
        string name,
        PackedLayerKind kind,
        IReadOnlyList<ExpressionItem> expressions,
        BuildSettings settings,
        DnfCondition? forceInactiveWhen)
    {
        var states = expressions.Select((expression, index) =>
        {
            var enterWhen = StateEnterWhen(kind, expressions, index);
            return new ExpressionStatePlan(
                expression.Name,
                enterWhen,
                BuildExpressionExitWhen(enterWhen, settings),
                expression.AnimationSet,
                expression.ExpressionSettings,
                BuildAapWrites(expression.FacialSettings));
        }).ToArray();

        return new ExpressionLayerPlan(name, forceInactiveWhen, states);
    }

    private static string PackedLayerName(PackedLayerKind kind, IReadOnlyList<ExpressionItem> expressions)
    {
        return kind == PackedLayerKind.ReplaceRun && expressions.Count > 1
            ? $"ReplaceRun {expressions[0].Name}..{expressions[^1].Name}"
            : expressions[0].Name;
    }

    private static DnfCondition StateEnterWhen(PackedLayerKind kind, IReadOnlyList<ExpressionItem> expressions, int index)
    {
        var expression = expressions[index];
        if (kind != PackedLayerKind.ReplaceRun) return expression.RawWhen;

        var higherReplaceWhen = DnfCondition.Any(expressions
            .Skip(index + 1)
            .Select(item => item.RawWhen));
        return expression.RawWhen.Except(higherReplaceWhen);
    }

    private static DnfCondition BuildExpressionExitWhen(DnfCondition enterWhen, BuildSettings settings)
    {
        var exitWhen = enterWhen.Not();
        if (string.IsNullOrWhiteSpace(settings.LockFacialParameterName)) return exitWhen;

        return exitWhen.And(ParameterBool(settings.LockFacialParameterName, false));
    }

    private static IReadOnlyList<AapWrite> BuildAapWrites(FacialSettings settings)
    {
        var writes = new List<AapWrite>();

        if (settings.AllowEyeBlink != TrackingPermission.Keep)
        {
            writes.Add(new AapWrite(
                EyeBlinkControlAap,
                VRCAAPHelper.IndexToValue(settings.AllowEyeBlink == TrackingPermission.Allow
                    ? ControlTrackingIndex
                    : ControlAnimationIndex)));
        }

        if (settings.AdvancedEyBlinkSettings.IsAnimationEnabled())
        {
            writes.Add(new AapWrite(EyeBlinkAdvancedSelectorAap, VRCAAPHelper.IndexToValue(AdvancedEnabledIndex)));
        }
        else if (settings.AllowEyeBlink != TrackingPermission.Keep)
        {
            writes.Add(new AapWrite(EyeBlinkAdvancedSelectorAap, VRCAAPHelper.IndexToValue(AdvancedNoneIndex)));
        }

        if (settings.AllowLipSync != TrackingPermission.Keep)
        {
            writes.Add(new AapWrite(
                LipSyncControlAap,
                VRCAAPHelper.IndexToValue(settings.AllowLipSync == TrackingPermission.Allow
                    ? ControlTrackingIndex
                    : ControlAnimationIndex)));
        }

        if (settings.AdvancedLipSyncSettings.IsCancelerEnabled())
        {
            writes.Add(new AapWrite(LipSyncAdvancedSelectorAap, VRCAAPHelper.IndexToValue(AdvancedEnabledIndex)));
        }
        else if (settings.AllowLipSync != TrackingPermission.Keep)
        {
            writes.Add(new AapWrite(LipSyncAdvancedSelectorAap, VRCAAPHelper.IndexToValue(AdvancedNoneIndex)));
        }

        return writes;
    }

    private static AdvancedEyeBlinkLayerPlan? BuildAdvancedEyeBlinkLayer(
        IReadOnlyList<ExpressionItem> expressions,
        DnfCondition? forceInactiveWhen)
    {
        return expressions.Any(expression => expression.FacialSettings.AdvancedEyBlinkSettings.IsAnimationEnabled())
            ? new AdvancedEyeBlinkLayerPlan("Advanced EyeBlink", forceInactiveWhen)
            : null;
    }

    private static AdvancedLipSyncLayerPlan? BuildAdvancedLipSyncLayer(
        IReadOnlyList<ExpressionItem> expressions,
        DnfCondition? forceInactiveWhen)
    {
        return expressions.Any(expression => expression.FacialSettings.AdvancedLipSyncSettings.IsCancelerEnabled())
            ? new AdvancedLipSyncLayerPlan("Advanced LipSync", forceInactiveWhen)
            : null;
    }

    private static TrackingControlLayerPlan? BuildTrackingControlLayer(
        BuildSettings settings,
        IReadOnlyList<OutputUnitPlan> units,
        DnfCondition? forceInactiveWhen)
    {
        if (settings.SupressTrackingControl) return null;

        var controlsEyeBlink = !string.IsNullOrWhiteSpace(settings.DisableEyeBlinkParameterName)
            || HasAapWrite(units, EyeBlinkControlAap);
        var controlsLipSync = !string.IsNullOrWhiteSpace(settings.DisableLipSyncParameterName)
            || HasAapWrite(units, LipSyncControlAap);

        if (!controlsEyeBlink && !controlsLipSync) return null;

        var hasEyeBlinkControlAap = HasAapWrite(units, EyeBlinkControlAap);
        var hasLipSyncControlAap = HasAapWrite(units, LipSyncControlAap);

        var eyeBlinkTrackingWhen = controlsEyeBlink ? TrackingControlWhen(EyeBlinkControlAap, hasEyeBlinkControlAap, settings.DisableEyeBlinkParameterName, true) : null;
        var eyeBlinkAnimationWhen = controlsEyeBlink ? TrackingControlWhen(EyeBlinkControlAap, hasEyeBlinkControlAap, settings.DisableEyeBlinkParameterName, false) : null;
        var lipSyncTrackingWhen = controlsLipSync ? TrackingControlWhen(LipSyncControlAap, hasLipSyncControlAap, settings.DisableLipSyncParameterName, true) : null;
        var lipSyncAnimationWhen = controlsLipSync ? TrackingControlWhen(LipSyncControlAap, hasLipSyncControlAap, settings.DisableLipSyncParameterName, false) : null;

        var states = new List<TrackingControlStatePlan>();
        foreach (var eyeBlinkState in TrackingDomainStates(controlsEyeBlink, eyeBlinkTrackingWhen, eyeBlinkAnimationWhen))
        {
            foreach (var lipSyncState in TrackingDomainStates(controlsLipSync, lipSyncTrackingWhen, lipSyncAnimationWhen))
            {
                states.Add(new TrackingControlStatePlan(
                    TrackingStateName(eyeBlinkState.Tracking, lipSyncState.Tracking),
                    DnfCondition.All(new[] { eyeBlinkState.When, lipSyncState.When }.OfType<DnfCondition>()),
                    eyeBlinkState.Tracking,
                    lipSyncState.Tracking));
            }
        }

        return new TrackingControlLayerPlan("Tracking Control", forceInactiveWhen, states);
    }

    private static bool HasAapWrite(IReadOnlyList<OutputUnitPlan> units, string parameterName)
    {
        return units.SelectMany(unit => unit.ExpressionLayers)
            .SelectMany(layer => layer.States)
            .SelectMany(state => state.AapWrites)
            .Any(write => write.ParameterName == parameterName);
    }

    private static IEnumerable<(bool? Tracking, DnfCondition? When)> TrackingDomainStates(
        bool enabled,
        DnfCondition? trackingWhen,
        DnfCondition? animationWhen)
    {
        if (!enabled)
        {
            yield return (null, null);
            yield break;
        }

        yield return (true, trackingWhen!);
        yield return (false, animationWhen!);
    }

    private static string TrackingStateName(bool? eyeBlinkTracking, bool? lipSyncTracking)
    {
        var eye = eyeBlinkTracking switch
        {
            true => "EyeBlinkTracking",
            false => "EyeBlinkAnimation",
            null => "EyeBlinkKeep"
        };
        var lip = lipSyncTracking switch
        {
            true => "LipSyncTracking",
            false => "LipSyncAnimation",
            null => "LipSyncKeep"
        };
        return $"{eye} {lip}";
    }

    private static DnfCondition TrackingControlWhen(
        string controlAapName,
        bool hasControlAap,
        string forceDisableParameterName,
        bool tracking)
    {
        var forceDisable = string.IsNullOrWhiteSpace(forceDisableParameterName)
            ? null
            : ParameterBool(forceDisableParameterName, true);

        var aapCondition = hasControlAap
            ? AapIndexCondition(controlAapName, tracking ? ControlTrackingIndex : ControlAnimationIndex)
            : tracking ? DnfCondition.Always : DnfCondition.Never;

        if (forceDisable == null) return aapCondition;

        return tracking
            ? aapCondition.And(forceDisable.Not())
            : aapCondition.Or(forceDisable);
    }

    private static FxControlPlan? BuildFxControl(BuildSettings settings)
    {
        if (!settings.MmdPlayback.Enabled || settings.MmdPlayback.DisableMode != MmdDisableMode.DisableFx) return null;
        if (string.IsNullOrWhiteSpace(settings.MmdPlayback.DisableParameterName)) return null;
        return new FxControlPlan("MMD FX Control", ParameterBool(settings.MmdPlayback.DisableParameterName, true));
    }

    private static DnfCondition? CreateLayerForceInactiveWhen(BuildSettings settings)
    {
        if (!settings.MmdPlayback.Enabled || settings.MmdPlayback.DisableMode == MmdDisableMode.DisableFx) return null;
        if (string.IsNullOrWhiteSpace(settings.MmdPlayback.DisableParameterName)) return null;
        return ParameterBool(settings.MmdPlayback.DisableParameterName, true);
    }

    private static DnfCondition ParameterBool(string parameterName, bool value)
    {
        return DnfCondition.Single(AnimatorConditionRule.FromParameterCondition(ParameterCondition.Bool(parameterName, value)));
    }

    private static DnfCondition AapIndexCondition(string parameterName, int index)
    {
        return DnfCondition.All(VRCAAPHelper.IndexConditions(parameterName, true, index)
            .Select(condition => DnfCondition.Single(new AnimatorConditionRule(condition, AnimatorControllerParameterType.Float))));
    }

    private static IEnumerable<int> FindExternalOverlapSplitIndices(
        IReadOnlyList<ExpressionItem> items,
        Transform root,
        IAnimatorPlatformServices platformServices,
        VirtualControllerContext controllerContext,
        ISet<string> managedBlendShapeNames)
    {
        if (items.Count < 2 || managedBlendShapeNames.Count == 0) yield break;

        var expressionIndexByTransform = items
            .Select((item, index) => (item.SourceTransform, index))
            .ToDictionary(entry => entry.SourceTransform, entry => entry.index);

        var hasExpressionAbove = false;
        var hasBoundarySinceLastExpression = false;

        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (expressionIndexByTransform.TryGetValue(transform, out var expressionIndex))
            {
                if (hasExpressionAbove && hasBoundarySinceLastExpression)
                {
                    yield return expressionIndex;
                }

                hasExpressionAbove = true;
                hasBoundarySinceLastExpression = false;
                continue;
            }

            if (!hasExpressionAbove || hasBoundarySinceLastExpression) continue;

            hasBoundarySinceLastExpression = platformServices.IsUnitBoundaryTransform(
                transform,
                controllerContext,
                managedBlendShapeNames);
        }
    }
}

internal sealed record class InitialLayerPlan(IReadOnlyList<BlendShapeWeight> BlendShapes);

internal sealed record class OutputUnitPlan(
    int Id,
    Transform Anchor,
    IReadOnlyList<ExpressionLayerPlan> ExpressionLayers,
    AdvancedEyeBlinkLayerPlan? AdvancedEyeBlink,
    AdvancedLipSyncLayerPlan? AdvancedLipSync);

internal sealed record class ExpressionLayerPlan(
    string Name,
    DnfCondition? ForceInactiveWhen,
    IReadOnlyList<ExpressionStatePlan> States);

internal sealed record class ExpressionStatePlan(
    string Name,
    DnfCondition EnterWhen,
    DnfCondition ExitWhen,
    BlendShapeWeightAnimationSet Animations,
    ExpressionSettings Settings,
    IReadOnlyList<AapWrite> AapWrites);

internal readonly record struct AapWrite(string ParameterName, float Value);

internal sealed record class TrackingControlLayerPlan(
    string Name,
    DnfCondition? ForceInactiveWhen,
    IReadOnlyList<TrackingControlStatePlan> States);

internal sealed record class TrackingControlStatePlan(
    string Name,
    DnfCondition When,
    bool? EyeBlinkTracking,
    bool? LipSyncTracking);

internal sealed record class AdvancedEyeBlinkLayerPlan(string Name, DnfCondition? ForceInactiveWhen);

internal sealed record class AdvancedLipSyncLayerPlan(string Name, DnfCondition? ForceInactiveWhen);

internal sealed record class FxControlPlan(string Name, DnfCondition DisableFxWhen);

internal enum PackedLayerKind
{
    ReplaceRun,
    Blend
}
