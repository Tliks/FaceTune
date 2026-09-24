using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>Virtual Animatorの配置規則とtransition生成を一箇所にまとめる。</summary>
internal sealed class AnimatorGraph
{
    private const string AlwaysParameterName =
        FaceTuneConstants.InternalParameterPrefix + "/Always";

    public const float PositionXStep = 250f;
    public const float PositionYStep = 50f;
    private const float InitialEvaluationDelaySeconds = 0.5f;

    private readonly bool _useWriteDefaults;
    private readonly CloneContext _cloneContext;
    private readonly VirtualClip _emptyClip;
    private readonly VirtualClip _defaultDelayClip;

    public AnimatorGraph(bool useWriteDefaults, CloneContext cloneContext)
    {
        _useWriteDefaults = useWriteDefaults;
        _cloneContext = cloneContext;
        _emptyClip = AnimatorHelper.CreateCustomEmptyClip();
        _defaultDelayClip = AnimatorHelper.CreateDelayClip(
            InitialEvaluationDelaySeconds,
            "Initial Delay");
    }

    public VirtualLayer AddLayer(VirtualAnimatorController controller, string name, int priority)
        => controller.AddLayer(new LayerPriority(priority), $"{FaceTuneConstants.Name}: {name}");

    public VirtualState AddState(VirtualLayer layer, string name, Vector3 position)
        => AddState(layer.StateMachine!, name, position);

    public VirtualState AddState(VirtualStateMachine stateMachine, string name, Vector3 position)
    {
        var state = stateMachine.AddState(name, position: position);
        state.WriteDefaultValues = _useWriteDefaults;
        return state;
    }

    public VirtualStateMachine AddStateMachine(
        VirtualStateMachine parent,
        string name,
        Vector3 position)
    {
        var child = VirtualStateMachine.Create(_cloneContext, name);
        parent.StateMachines = parent.StateMachines.Add(
            new VirtualStateMachine.VirtualChildStateMachine
            {
                StateMachine = child,
                Position = position
            });
        return child;
    }

    public void AsPassThrough(VirtualState state)
    {
        state.Motion = _useWriteDefaults ? null : _emptyClip;
    }

    public VirtualState AddInitialDelayState(VirtualLayer layer, Vector3 position)
    {
        var state = AddState(layer, "Initial Delay", position);
        state.Motion = _defaultDelayClip;
        layer.StateMachine!.DefaultState = state;

        return state;
    }

    public void AddExitTimeExitTransition(VirtualState state)
    {
        var transition = CreateTransitionWithExitTime();
        transition.SetExitDestination();
        state.Transitions = state.Transitions.Add(transition);
    }

    public static void EnsureAlwaysParameter(VirtualAnimatorController controller)
        => controller.EnsureBoolParameterExists(AlwaysParameterName, true);

    public static void EnsureConditionParameters(
        VirtualAnimatorController controller,
        params DnfCondition?[] conditions)
    {
        foreach (var condition in conditions)
        {
            if (condition == null) continue;
            foreach (var rule in condition.Cases
                         .SelectMany(conditionCase => conditionCase.Rules)
                         .OfType<AnimatorConditionRule>())
            {
                controller.EnsureParameterExists(
                    rule.ParameterType,
                    rule.ParameterName,
                    0f);
            }
        }
    }

    public void AddStateTransition(
        VirtualState source,
        VirtualState destination,
        DnfCondition when,
        float duration)
    {
        var transitions = CreateStateTransitions(destination, when, duration);
        source.Transitions = source.Transitions.AddRange(transitions);
    }

    public void AddStateTransition(
        VirtualState source,
        VirtualStateMachine destination,
        DnfCondition when,
        float duration)
    {
        var transitions = TransitionCases(when).Select(conditionCase =>
        {
            var transition = CreateTransitionWithDurationSeconds(duration);
            transition.SetDestination(destination);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            return transition;
        });
        source.Transitions = source.Transitions.AddRange(transitions);
    }

    public void AddExitTimeTransition(
        VirtualState source,
        VirtualState destination,
        float exitTime = 1f,
        float duration = 0f)
    {
        var transition = CreateTransitionWithExitTime(exitTime, duration);
        transition.SetDestination(destination);
        source.Transitions = source.Transitions.Add(transition);
    }

    public void AddExitTimeTransition(
        VirtualState source,
        VirtualState destination,
        DnfCondition when,
        float exitTime,
        float duration)
    {
        var transitions = TransitionCases(when).Select(conditionCase =>
        {
            var transition = CreateTransitionWithExitTime(exitTime, duration);
            transition.SetDestination(destination);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            return transition;
        });
        source.Transitions = source.Transitions.AddRange(transitions);
    }

    public void SetExitTransitions(VirtualState state, DnfCondition when, float duration)
    {
        state.Transitions = ImmutableList<VirtualStateTransition>.Empty;
        AddExitTransitions(state, when, duration);
    }

    public void AddExitTransitions(VirtualState state, DnfCondition when, float duration)
    {
        var transitions = TransitionCases(when).Select(conditionCase =>
        {
            var transition = CreateExitTransition(duration);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            return transition;
        });
        // 解除条件は時間遷移より先に評価する。
        state.Transitions = transitions.Concat(state.Transitions).ToImmutableList();
    }

    public void SetAnyStateTransition(
        VirtualLayer layer,
        VirtualState destination,
        DnfCondition when,
        float duration)
    {
        var transitions = TransitionCases(when).Select(conditionCase =>
        {
            var transition = CreateAnyStateTransition(destination, duration);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            return transition;
        });
        layer.StateMachine!.AnyStateTransitions = transitions.ToImmutableList();
    }

    public void AddEntryTransition(VirtualLayer layer, VirtualState destination, DnfCondition when)
        => AddEntryTransition(layer.StateMachine!, destination, when);

    public void AddEntryTransition(
        VirtualLayer layer,
        VirtualStateMachine destination,
        DnfCondition when)
        => AddEntryTransitions(layer.StateMachine!, when, transition =>
            transition.SetDestination(destination));

    public void AddEntryTransition(
        VirtualStateMachine stateMachine,
        VirtualState destination,
        DnfCondition? when = null)
    {
        if (when != null)
        {
            AddEntryTransitions(stateMachine, when, transition =>
                transition.SetDestination(destination));
            return;
        }

        stateMachine.EntryTransitions = stateMachine.EntryTransitions.Add(
            CreateTransition(transition => transition.SetDestination(destination)));
    }

    private static void AddEntryTransitions(
        VirtualStateMachine stateMachine,
        DnfCondition when,
        Action<VirtualTransition> setDestination)
    {
        stateMachine.EntryTransitions = stateMachine.EntryTransitions.AddRange(
            CreateTransitions(when, setDestination));
    }

    public void AddStateMachineTransition(
        VirtualStateMachine parent,
        VirtualStateMachine source,
        VirtualStateMachine destination,
        DnfCondition when)
        => AddStateMachineTransition(parent, source, when, transition =>
            transition.SetDestination(destination));

    public void AddStateMachineTransition(
        VirtualStateMachine parent,
        VirtualStateMachine source,
        VirtualState destination)
        => AddStateMachineTransition(parent, source, transition =>
            transition.SetDestination(destination));

    public void AddStateMachineExitTransition(
        VirtualStateMachine parent,
        VirtualStateMachine source)
        => AddStateMachineTransition(parent, source, transition =>
            transition.SetExitDestination());

    public void AddStateMachineTransition(
        VirtualStateMachine parent,
        VirtualStateMachine source,
        VirtualState destination,
        DnfCondition when)
        => AddStateMachineTransition(parent, source, when, transition =>
            transition.SetDestination(destination));

    private static void AddStateMachineTransition(
        VirtualStateMachine parent,
        VirtualStateMachine source,
        Action<VirtualTransition> setDestination)
        => AddStateMachineTransitions(parent, source, new[] { CreateTransition(setDestination) });

    private static void AddStateMachineTransition(
        VirtualStateMachine parent,
        VirtualStateMachine source,
        DnfCondition when,
        Action<VirtualTransition> setDestination)
        => AddStateMachineTransitions(parent, source, CreateTransitions(when, setDestination));

    private static IEnumerable<VirtualTransition> CreateTransitions(
        DnfCondition when,
        Action<VirtualTransition> setDestination)
        => TransitionCases(when).Select(conditionCase =>
        {
            var transition = CreateTransition(setDestination);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            return transition;
        });

    private static VirtualTransition CreateTransition(Action<VirtualTransition> setDestination)
    {
        var transition = VirtualTransition.Create();
        setDestination(transition);
        return transition;
    }

    private static void AddStateMachineTransitions(
        VirtualStateMachine parent,
        VirtualStateMachine source,
        IEnumerable<VirtualTransition> transitions)
    {
        var current = parent.StateMachineTransitions.TryGetValue(source, out var existing)
            ? existing
            : ImmutableList<VirtualTransition>.Empty;
        parent.StateMachineTransitions = parent.StateMachineTransitions.SetItem(
            source,
            current.AddRange(transitions));
    }

    private static IEnumerable<VirtualStateTransition> CreateStateTransitions(
        VirtualState destination,
        DnfCondition when,
        float duration)
        => TransitionCases(when).Select(conditionCase =>
        {
            var transition = CreateStateTransition(destination, duration);
            transition.Conditions = ToAnimatorConditions(conditionCase).ToImmutableList();
            return transition;
        });

    private static IEnumerable<DnfCase> TransitionCases(DnfCondition condition)
    {
        if (condition.IsNever) yield break;
        foreach (var conditionCase in condition.Cases) yield return conditionCase;
    }

    private static VirtualStateTransition CreateStateTransition(
        VirtualState destination,
        float duration)
    {
        var transition = CreateTransitionWithDurationSeconds(duration);
        transition.SetDestination(destination);
        return transition;
    }

    private static VirtualStateTransition CreateExitTransition(float duration)
    {
        var transition = CreateTransitionWithDurationSeconds(duration);
        transition.SetExitDestination();
        return transition;
    }

    private static VirtualStateTransition CreateAnyStateTransition(
        VirtualState destination,
        float duration)
    {
        var transition = CreateStateTransition(destination, duration);
        transition.CanTransitionToSelf = false;
        return transition;
    }

    private static VirtualStateTransition CreateTransitionWithDurationSeconds(float duration)
    {
        var transition = VirtualStateTransition.Create();
        transition.ExitTime = null;
        transition.HasFixedDuration = true;
        transition.Duration = duration;
        return transition;
    }

    private static VirtualStateTransition CreateTransitionWithExitTime(
        float exitTime = 1f,
        float duration = 0f)
    {
        var transition = VirtualStateTransition.Create();
        transition.ExitTime = exitTime;
        transition.HasFixedDuration = true;
        transition.Duration = duration;
        return transition;
    }

    private static IEnumerable<AnimatorCondition> ToAnimatorConditions(DnfCase conditionCase)
    {
        if (conditionCase.IsAlways)
        {
            return new[]
            {
                new AnimatorCondition
                {
                    mode = AnimatorConditionMode.If,
                    parameter = AlwaysParameterName
                }
            };
        }

        return conditionCase.Rules
            .Cast<AnimatorConditionRule>()
            .OrderBy(rule => rule.ParameterName, StringComparer.Ordinal)
            .ThenBy(rule => rule.ParameterType)
            .ThenBy(rule => rule.Condition.mode)
            .ThenBy(rule => rule.Condition.threshold)
            .Select(rule => rule.Condition);
    }
}
