using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static partial class AnimatorHelper
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

        if (wdStateCounter.Count == 1)
        {
            writeDefaults = wdStateCounter.First().Key;
        }
        return writeDefaults;
    }

    private static bool IsWriteDefaultsSafeLayer(VirtualLayer virtualLayer)
    {
        if (virtualLayer.BlendingMode == AnimatorLayerBlendingMode.Additive)
        {
            return true;
        }

        var stateMachine = virtualLayer.StateMachine;
        if (stateMachine == null || stateMachine.StateMachines.Count != 0)
        {
            return false;
        }

        return stateMachine.States.Count == 1
            && stateMachine.AnyStateTransitions.Count == 0
            && stateMachine.DefaultState?.Transitions.Count == 0
            && stateMachine.DefaultState.Motion is VirtualBlendTree;
    }

    public static TBehavior EnsureBehavior<TBehavior>(this VirtualState state)
        where TBehavior : StateMachineBehaviour
    {
        var behavior = state.Behaviours.OfType<TBehavior>().FirstOrDefault();
        if (behavior == null)
        {
            behavior = ScriptableObject.CreateInstance<TBehavior>();
            state.Behaviours = state.Behaviours.Add(behavior);
        }
        return behavior;
    }

    public static void EnsureBoolParameterExists(
        this VirtualAnimatorController controller,
        string parameter,
        bool defaultValue = false)
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

    public static void EnsureIntParameterExists(
        this VirtualAnimatorController controller,
        string parameter,
        int defaultValue = 0)
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

    public static void EnsureFloatParameterExists(
        this VirtualAnimatorController controller,
        string parameter,
        float defaultValue = 0f)
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

    public static void EnsureParameterExists(
        this VirtualAnimatorController controller,
        AnimatorControllerParameterType type,
        string parameter,
        float defaultValue)
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

    public static VirtualClip SetNewClip(this VirtualState state, string name)
    {
        var clip = VirtualClip.Create(name);
        state.Motion = clip;
        return clip;
    }

    public static void SetAap(
        this VirtualClip clip,
        string parameterName,
        float value,
        float durationSeconds = 0f)
    {
        var curve = durationSeconds > 0f
            ? new AnimationCurve(
                new Keyframe(0f, value),
                new Keyframe(durationSeconds, value))
            : new AnimationCurve(new Keyframe(0f, value));
        clip.SetFloatCurve("", typeof(UnityEngine.Animator), parameterName, curve);
    }

    public static VirtualClip CreateCustomEmptyClip(
        string clipName = "FaceTune Custom Empty Clip")
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

    private const string AnimatedBlendShapePrefix = FaceTuneConstants.BlendShapePropertyPrefix;

    public static void AddBlendShapeAnimation(
        this VirtualClip clip,
        string bodyPath,
        BlendShapeWeightAnimation animation)
    {
        clip.SetFloatCurve(
            bodyPath,
            typeof(SkinnedMeshRenderer),
            AnimatedBlendShapePrefix + animation.Name,
            animation.Curve);
    }

    public static void AddBlendShapeAnimations(
        this VirtualClip clip,
        string bodyPath,
        IEnumerable<BlendShapeWeightAnimation> animations)
    {
        foreach (var animation in animations)
        {
            AddBlendShapeAnimation(clip, bodyPath, animation);
        }
    }

}