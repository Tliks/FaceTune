using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Build.Animator;

internal class BlinkInstaller : InstallerBase
{
    private readonly string _forceDisableEyeBlinkParameter;
    private readonly Dictionary<string, string> _clonedShapesMapping = new();

    private const string DelayMultiplier = $"{FaceTuneConstants.ParameterPrefix}/Blink/DelayMultiplier";

    public BlinkInstaller(
        VirtualAnimatorController virtualController,
        AvatarContext avatarContext,
        bool useWriteDefaults,
        IAnimatorPlatformServices platformServices,
        string forceDisableEyeBlinkParameter) : base(virtualController, avatarContext, useWriteDefaults, platformServices)
    {
        _forceDisableEyeBlinkParameter = forceDisableEyeBlinkParameter;
        if (!string.IsNullOrWhiteSpace(_forceDisableEyeBlinkParameter))
        {
            _controller.EnsureBoolParameterExists(_forceDisableEyeBlinkParameter);
        }
    }

    public void AddEyeBlinkLayer(OutputUnit unit, RuntimeDomain<EyeBlinkRuntimeMode> domain)
    {
        var localEntries = domain.LocalEntries(unit).ToArray();
        if (localEntries.Length == 0) return;

        var advancedEntries = PrepareAdvancedSettings(localEntries);

        var layer = AddLayer($"{unit.Anchor.name} EyeBlink", LayerPriority);
        var position = EntryStatePosition;

        var foreign = AddState(layer, "Foreign/Inert", position);
        AsPassThrough(foreign);
        position.y += PositionYStep;

        var baseline = AddTrackingState(layer, "BaselineTracking", true, position);
        position.y += PositionYStep;
        var tracking = AddTrackingState(layer, "Tracking", true, position);
        position.y += PositionYStep;
        var disabled = AddTrackingState(layer, "Disabled", false, position);
        position.y += 2 * PositionYStep;

        var targets = new List<(VirtualState state, IEnumerable<AnimatorCondition> conditions)>
        {
            (baseline, VRCAAPHelper.IndexConditions(domain.ParameterName, true, domain.Baseline.Index))
        };
        targets.AddRange(domain.ForeignEntries(unit)
            .Select(entry => (foreign, VRCAAPHelper.IndexConditions(domain.ParameterName, true, entry.Index))));

        foreach (var entry in localEntries)
        {
            switch (entry.Mode.Kind)
            {
                case EyeBlinkRuntimeModeKind.Tracking:
                    targets.Add((tracking, VRCAAPHelper.IndexConditions(domain.ParameterName, true, entry.Index)));
                    break;
                case EyeBlinkRuntimeModeKind.Disabled:
                    targets.Add((disabled, VRCAAPHelper.IndexConditions(domain.ParameterName, true, entry.Index)));
                    break;
            }
        }

        var advancedGates = new List<VirtualState>();
        foreach (var (entry, settings) in advancedEntries)
        {
            var gate = AddAdvancedBlinkStates(layer, domain, entry, settings, disabled, position);
            advancedGates.Add(gate);
            targets.Add((gate, VRCAAPHelper.IndexConditions(domain.ParameterName, true, entry.Index)));
            position.y += 4 * PositionYStep;
        }

        ApplyForceDisableGuard(targets, disabled);

        AddEntryTransitions(layer, targets);
        var allStates = new[] { foreign, baseline, tracking, disabled }.Concat(advancedGates).ToArray();
        foreach (var state in allStates)
        {
            AddModeTransitions(state, targets.Where(target => target.state != state));
            AddForceDisableTransition(state, disabled);
        }
    }

    private VirtualState AddTrackingState(VirtualLayer layer, string name, bool tracking, Vector3 position)
    {
        var state = AddState(layer, name, position);
        state.Motion = _emptyClip;
        _platformServices.SetEyeBlinkTracking(state, tracking);
        return state;
    }

    private IReadOnlyList<(ModeEntry<EyeBlinkRuntimeMode> entry, AdvancedEyeBlinkSettings settings)> PrepareAdvancedSettings(
        IEnumerable<ModeEntry<EyeBlinkRuntimeMode>> localEntries)
    {
        var originalSettings = localEntries
            .Where(entry => entry.Mode.Kind == EyeBlinkRuntimeModeKind.Advanced)
            .Select(entry => entry.Mode.AdvancedSettings!)
            .ToArray();
        if (originalSettings.Length == 0) return Array.Empty<(ModeEntry<EyeBlinkRuntimeMode>, AdvancedEyeBlinkSettings)>();

        var blinkShapeNames = originalSettings.SelectMany(settings => settings.BlinkBlendShapeNames).ToHashSet();
        Action<Mesh, Mesh> onClone = (original, clone) => ObjectRegistry.RegisterReplacedObject(original, clone);
        Action<string> onNotFound = name => LocalizedLog.Error("BuildLog:error:CloneShapes:ShapeNotFound", name);
        var cloned = Utils.CloneShapes(_avatarContext.FaceRenderer, blinkShapeNames, onClone, onNotFound, "_clone.blink");
        foreach (var (source, clone) in cloned)
        {
            _clonedShapesMapping[source] = clone;
        }

        return localEntries
            .Where(entry => entry.Mode.Kind == EyeBlinkRuntimeModeKind.Advanced)
            .Select(entry => (entry, entry.Mode.AdvancedSettings!.GetRenamed(_clonedShapesMapping)))
            .ToArray();
    }

    private VirtualState AddAdvancedBlinkStates(
        VirtualLayer layer,
        RuntimeDomain<EyeBlinkRuntimeMode> domain,
        ModeEntry<EyeBlinkRuntimeMode> entry,
        AdvancedEyeBlinkSettings settings,
        VirtualState disabled,
        Vector3 position)
    {
        _controller.EnsureFloatParameterExists(DelayMultiplier);

        var stare = AddTrackingState(layer, $"AdvancedBlink {entry.Index} Stare", false, position);
        var entryPassThrough = AddTrackingState(layer, $"AdvancedBlink {entry.Index} Entry", false, position + new Vector3(PositionXStep, 0, 0));
        var blink = AddTrackingState(layer, $"AdvancedBlink {entry.Index} Blink", false, position + new Vector3(PositionXStep, PositionYStep, 0));
        var exitPassThrough = AddTrackingState(layer, $"AdvancedBlink {entry.Index} Exit", false, position + new Vector3(0, PositionYStep, 0));

        if (!settings.UseRandomInterval)
        {
            stare.Motion = AnimatorHelper.CreateDelayClip(settings.IntervalSeconds, $"BlinkDelay {entry.Index}");
        }
        else
        {
            stare.Motion = AnimatorHelper.CreateDelayClip(settings.RandomIntervalMinSeconds, $"BlinkDelay {entry.Index}");
            var minMultiplier = settings.RandomIntervalMinSeconds / settings.RandomIntervalMaxSeconds;
            _platformServices.AddRandomDriver(stare, DelayMultiplier, minMultiplier, 1f);
            stare.SpeedParameter = DelayMultiplier;
        }

        AsPassThrough(entryPassThrough);
        AsPassThrough(exitPassThrough);
        AddBlendShapeAnimationsToState(blink, CreateBlinkAnimations(settings));

        var modeConditions = VRCAAPHelper.IndexConditions(domain.ParameterName, true, entry.Index).ToImmutableList();

        AddTransition(stare, entryPassThrough, AnimatorHelper.CreateTransitionWithExitTime(), modeConditions);
        AddTransition(entryPassThrough, blink, AnimatorHelper.CreateTransitionWithDurationSeconds(settings.ClosingDurationSeconds), modeConditions);
        AddTransition(blink, exitPassThrough, AnimatorHelper.CreateTransitionWithExitTime(1f, settings.OpeningDurationSeconds), modeConditions);
        AddTransition(exitPassThrough, stare, AnimatorHelper.CreateTransitionWithDurationSeconds(0f), modeConditions);

        foreach (var state in new[] { entryPassThrough, blink, exitPassThrough })
        {
            AddTransition(state, stare, AnimatorHelper.CreateTransitionWithDurationSeconds(0f), modeConditions);
            var modeMismatch = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
            modeMismatch.SetDestination(stare);
            state.Transitions = state.Transitions.AddRange(AnimatorHelper.SetORConditions(
                modeMismatch,
                VRCAAPHelper.IndexConditions(domain.ParameterName, false, entry.Index)));
            AddForceDisableTransition(state, disabled);
        }

        return stare;
    }

    private static IEnumerable<BlendShapeWeightAnimation> CreateBlinkAnimations(AdvancedEyeBlinkSettings settings)
    {
        var animations = new List<BlendShapeWeightAnimation>();
        var holdDuration = Math.Max(settings.HoldDurationSeconds, 0.01f);
        foreach (var name in settings.BlinkBlendShapeNames)
        {
            var curve = new AnimationCurve();
            curve.AddKey(0f, 100f);
            curve.AddKey(holdDuration, 100f);
            animations.Add(new BlendShapeWeightAnimation(name, curve));
        }
        foreach (var name in settings.CancelerBlendShapeNames)
        {
            var curve = new AnimationCurve();
            curve.AddKey(0f, 0f);
            curve.AddKey(holdDuration, 0f);
            animations.Add(new BlendShapeWeightAnimation(name, curve));
        }
        return animations;
    }

    private static void AddTransition(
        VirtualState source,
        VirtualState target,
        VirtualStateTransition transition,
        IEnumerable<AnimatorCondition> conditions)
    {
        transition.SetDestination(target);
        transition.Conditions = conditions.ToImmutableList();
        source.Transitions = source.Transitions.Add(transition);
    }

    private static void AddEntryTransitions(
        VirtualLayer layer,
        IEnumerable<(VirtualState state, IEnumerable<AnimatorCondition> conditions)> targets)
    {
        var transitions = targets.Select(target =>
        {
            var transition = VirtualTransition.Create();
            transition.SetDestination(target.state);
            transition.Conditions = target.conditions.ToImmutableList();
            return transition;
        });
        layer.StateMachine!.EntryTransitions = layer.StateMachine!.EntryTransitions.AddRange(transitions);
    }

    private static void AddModeTransitions(
        VirtualState source,
        IEnumerable<(VirtualState state, IEnumerable<AnimatorCondition> conditions)> targets)
    {
        foreach (var target in targets)
        {
            AddTransition(source, target.state, AnimatorHelper.CreateTransitionWithDurationSeconds(0f), target.conditions);
        }
    }

    private void ApplyForceDisableGuard(List<(VirtualState state, IEnumerable<AnimatorCondition> conditions)> targets, VirtualState disabled)
    {
        if (string.IsNullOrWhiteSpace(_forceDisableEyeBlinkParameter)) return;

        for (var index = 0; index < targets.Count; index++)
        {
            var target = targets[index];
            if (target.state == disabled) continue;
            targets[index] = (target.state, target.conditions.Append(new AnimatorCondition
            {
                parameter = _forceDisableEyeBlinkParameter,
                mode = AnimatorConditionMode.IfNot
            }).ToArray());
        }

        targets.Add((disabled, new[]
        {
            new AnimatorCondition { parameter = _forceDisableEyeBlinkParameter, mode = AnimatorConditionMode.If }
        }));
    }

    private void AddForceDisableTransition(VirtualState source, VirtualState disabled)
    {
        if (string.IsNullOrWhiteSpace(_forceDisableEyeBlinkParameter) || source == disabled) return;
        AddTransition(source, disabled, AnimatorHelper.CreateTransitionWithDurationSeconds(0f), new[]
        {
            new AnimatorCondition { parameter = _forceDisableEyeBlinkParameter, mode = AnimatorConditionMode.If }
        });
    }

    public IEnumerable<BlendShapeWeight> ShapesToInitialize => _clonedShapesMapping.Values.Select(name => new BlendShapeWeight(name, 0f));
}
