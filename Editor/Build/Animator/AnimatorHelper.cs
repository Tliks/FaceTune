using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Build.Animator;

internal static class AnimatorHelper
{
    internal static bool? AnalyzeLayerWriteDefaults(VirtualAnimatorController controller)
    {
        bool? writeDefaults = null;

        var wdStateCounter = controller.Layers
            .Where(l => !IsWriteDefaultsSafeLayer(l))
            .Where(l => l.StateMachine != null)
            .SelectMany(l => l.StateMachine!.AllStates())
            .Select(s => s.WriteDefaultValues)
            .GroupBy(b => b)
            .ToDictionary(g => g.Key, g => g.Count());

        if (wdStateCounter.Count == 1) writeDefaults = wdStateCounter.First().Key;
        return writeDefaults;
    }

    private static bool IsWriteDefaultsSafeLayer(VirtualLayer virtualLayer)
    {
        if (virtualLayer.BlendingMode == AnimatorLayerBlendingMode.Additive) return true;
        var sm = virtualLayer.StateMachine;

        if (sm == null) return false;
        if (sm.StateMachines.Count != 0) return false;
        return sm.States.Count == 1 && sm.AnyStateTransitions.Count == 0 &&
                sm.DefaultState?.Transitions.Count == 0 && sm.DefaultState.Motion is VirtualBlendTree;
    }

    public static TBehavior EnsureBehavior<TBehavior>(this VirtualStateMachine stateMachine) where TBehavior : StateMachineBehaviour
    {
        var behavior = stateMachine.Behaviours.OfType<TBehavior>().FirstOrDefault();
        if (behavior == null)
        {
            behavior = ScriptableObject.CreateInstance<TBehavior>();
            stateMachine.Behaviours = stateMachine.Behaviours.Add(behavior);
        }
        return behavior;
    }

    public static TBehavior EnsureBehavior<TBehavior>(this VirtualState state) where TBehavior : StateMachineBehaviour
    {
        var behavior = state.Behaviours.OfType<TBehavior>().FirstOrDefault();
        if (behavior == null)
        {
            behavior = ScriptableObject.CreateInstance<TBehavior>();
            state.Behaviours = state.Behaviours.Add(behavior);
        }
        return behavior;
    }

    public static VirtualStateTransition CreateTransitionWithDurationSeconds(float duration)
    {
        var transition = VirtualStateTransition.Create();
        transition.ExitTime = null; 
        transition.HasFixedDuration = true;
        transition.Duration = duration;
        return transition;
    }

    public static VirtualStateTransition CreateTransitionWithExitTime(float exitTime = 1f, float duration = 0f)
    {
        var transition = VirtualStateTransition.Create();
        transition.ExitTime = exitTime; 
        transition.HasFixedDuration = true;
        transition.Duration = duration;
        return transition;
    }

    public static void EnsureBoolParameterExists(this VirtualAnimatorController controller, string parameter, bool defaultValue = false)
    {
        if (!controller.Parameters.ContainsKey(parameter))
        {
            var param = new AnimatorControllerParameter
            {
                name = parameter,
                type = AnimatorControllerParameterType.Bool,
                defaultBool = defaultValue
            };
            controller.Parameters = controller.Parameters.Add(parameter, param);
        }
    }

    public static void EnsureIntParameterExists(this VirtualAnimatorController controller, string parameter, int defaultValue = 0)
    {
        if (!controller.Parameters.ContainsKey(parameter))
        {
            var param = new AnimatorControllerParameter
            {
                name = parameter,
                type = AnimatorControllerParameterType.Int,
                defaultInt = defaultValue
            };
            controller.Parameters = controller.Parameters.Add(parameter, param);
        }
    }

    public static void EnsureFloatParameterExists(this VirtualAnimatorController controller, string parameter, float defaultValue = 0f)
    {
        if (!controller.Parameters.ContainsKey(parameter))
        {
            var param = new AnimatorControllerParameter
            {
                name = parameter,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = defaultValue
            };
            controller.Parameters = controller.Parameters.Add(parameter, param);
        }
    }

    public static void EnsureParameterExists(this VirtualAnimatorController controller, AnimatorControllerParameterType type, string parameter)
    {
        switch (type)
        {
            case AnimatorControllerParameterType.Bool:
            case AnimatorControllerParameterType.Trigger:
                EnsureBoolParameterExists(controller, parameter);
                break;
            case AnimatorControllerParameterType.Int:
                EnsureIntParameterExists(controller, parameter);
                break;
            case AnimatorControllerParameterType.Float:
                EnsureFloatParameterExists(controller, parameter);
                break;
            default:
                throw new ArgumentException($"Invalid parameter type: {type}");
        }
    }

    public static void EnsureParameterExists(this VirtualAnimatorController controller, AnimatorControllerParameterType type, string parameter, float defaultValue)
    {
        switch (type)
        {
            case AnimatorControllerParameterType.Bool:
                EnsureBoolParameterExists(controller, parameter, defaultValue != 0f);
                break;
            case AnimatorControllerParameterType.Int:
                EnsureIntParameterExists(controller, parameter, (int)defaultValue);
                break;
            case AnimatorControllerParameterType.Float:
                EnsureFloatParameterExists(controller, parameter, defaultValue);
                break;
            default:
                throw new ArgumentException($"Invalid parameter type: {type}");
        }
    }

    public static void CollectConditionParameters(Dictionary<string, PlanParameter> parameters, DnfCondition condition)
    {
        foreach (var conditionCase in condition.Cases)
        {
            foreach (var rule in conditionCase.Rules)
            {
                if (rule is not AnimatorConditionRule acr) continue;
                parameters.TryAdd(acr.ParameterName, new PlanParameter(
                    acr.ParameterName, acr.ParameterType, (float)acr.Condition.threshold));
            }
        }
    }

    public static float DiscreteFloatIndexToValue(int index)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        return index;
    }

    public static DnfCondition DiscreteFloatIndexCondition(string parameter, int index)
    {
        if (index < 0 || index == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(index));

        var conditions = new List<DnfCondition>();
        if (index > 0)
        {
            conditions.Add(DnfCondition.Single(new AnimatorConditionRule(
                new AnimatorCondition
                {
                    parameter = parameter,
                    mode = AnimatorConditionMode.Greater,
                    threshold = DiscreteFloatIndexToValue(index - 1)
                },
                AnimatorControllerParameterType.Float)));
        }

        conditions.Add(DnfCondition.Single(new AnimatorConditionRule(
            new AnimatorCondition
            {
                parameter = parameter,
                mode = AnimatorConditionMode.Less,
                threshold = DiscreteFloatIndexToValue(index + 1)
            },
            AnimatorControllerParameterType.Float)));
        return DnfCondition.All(conditions);
    }

    public static bool TryGetClip(this VirtualState state, [NotNullWhen(true)] out VirtualClip? clip)
    {
        var motion = state.Motion as VirtualClip;
        clip = motion;
        return motion != null;
    }

    public static VirtualClip SetNewClip(this VirtualState state, string name)
    {
        var clip = VirtualClip.Create(name);
        state.Motion = clip;
        return clip;
    }

    public static VirtualClip EnsureClip(this VirtualState state, string name)
    {
        if (state.TryGetClip(out var clip))
        {
            clip.Name = name;
            return clip;
        }
        clip = SetNewClip(state, name);
        return clip;
    }

    // 適当なGameObjectのactiveを切り替える2フレームアニメーションを作成
    public static VirtualClip CreateCustomEmptyClip(string clipName = "FaceTune Custom Empty Clip")
    {
        var clip = VirtualClip.Create(clipName);

        var curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(1f / clip.FrameRate, 0f);

        clip.SetFloatCurve("", typeof(GameObject), "m_IsActive", curve);
        return clip;
    }

    public static VirtualClip CreateDelayClip(float delay, string clipName = "Delay Clip")
    {
        var clip = VirtualClip.Create(clipName);

        var curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(delay, 0f);

        clip.SetFloatCurve("", typeof(GameObject), "m_IsActive", curve);
        return clip;
    }

    private const string AnimatedBlendShapePrefix = "blendShape.";

    public static void AddBlendShapeAnimation(this VirtualClip clip, string bodyPath, BlendShapeWeightAnimation animation)
    {
        clip.SetFloatCurve(bodyPath, typeof(SkinnedMeshRenderer), AnimatedBlendShapePrefix + animation.Name, animation.Curve);
    }

    public static void AddBlendShapeAnimations(this VirtualClip clip, string bodyPath, IEnumerable<BlendShapeWeightAnimation> animations)
    {
        foreach (var animation in animations)
        {
            AddBlendShapeAnimation(clip, bodyPath, animation);
        }
    }

}