using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Platforms.VRChat;

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

    public static VirtualStateTransition CreateTransitionWithDurationSeconds(float duration)
    {
        var transition = VirtualStateTransition.Create();
        transition.ExitTime = null;
        transition.HasFixedDuration = true;
        transition.Duration = duration;
        return transition;
    }

    public static VirtualStateTransition CreateTransitionWithExitTime(
        float exitTime = 1f,
        float duration = 0f)
    {
        var transition = VirtualStateTransition.Create();
        transition.ExitTime = exitTime;
        transition.HasFixedDuration = true;
        transition.Duration = duration;
        return transition;
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

    public static float DiscreteFloatIndexToValue(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return index;
    }

    public static DnfCondition DiscreteFloatIndexCondition(string parameter, int index)
    {
        if (index < 0 || index == int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var conditions = new List<DnfCondition>();
        if (index > 0)
        {
            conditions.Add(DnfCondition.Single(
                new AnimatorConditionRule(
                    new AnimatorCondition
                    {
                        parameter = parameter,
                        mode = AnimatorConditionMode.Greater,
                        threshold = DiscreteFloatIndexToValue(index - 1)
                    },
                    AnimatorControllerParameterType.Float),
                ParameterDomainRegistry.Empty));
        }

        conditions.Add(DnfCondition.Single(
            new AnimatorConditionRule(
                new AnimatorCondition
                {
                    parameter = parameter,
                    mode = AnimatorConditionMode.Less,
                    threshold = DiscreteFloatIndexToValue(index + 1)
                },
                AnimatorControllerParameterType.Float),
            ParameterDomainRegistry.Empty));
        return DnfCondition.All(conditions);
    }

    public static VirtualClip SetNewClip(this VirtualState state, string name)
    {
        var clip = VirtualClip.Create(name);
        state.Motion = clip;
        return clip;
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

    public static ResolvedNonFacialAnimationSet GetDefaultValueAnimations(
        GameObject root,
        IEnumerable<EditorCurveBinding> curveBindings)
    {
        var result = new ResolvedNonFacialAnimationSet();
        foreach (var binding in curveBindings.Distinct())
        {
            var target = AnimationUtility.GetAnimatedObject(root, binding);
            if (target == null || target is Animator) continue;

            if (target is SkinnedMeshRenderer renderer
                && binding.type == typeof(SkinnedMeshRenderer)
                && binding.propertyName.StartsWith(
                    AnimatedBlendShapePrefix,
                    StringComparison.Ordinal))
            {
                var mesh = renderer.sharedMesh;
                if (mesh == null) continue;

                var shapeName = binding.propertyName[AnimatedBlendShapePrefix.Length..];
                var index = mesh.GetBlendShapeIndex(shapeName);
                if (index < 0) continue;

                result.AddFloatCurve(
                    binding,
                    CreateSingleFrameCurve(renderer.GetBlendShapeWeight(index)));
                continue;
            }

            if (target is Transform transform
                && TryGetTransformValue(transform, binding.propertyName, out var transformValue))
            {
                result.AddFloatCurve(binding, CreateSingleFrameCurve(transformValue));
                continue;
            }

            using var serializedObject = new SerializedObject(target);
            serializedObject.UpdateIfRequiredOrScript();
            var property = serializedObject.FindProperty(binding.propertyName);
            if (property == null) continue;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    result.AddFloatCurve(
                        binding,
                        CreateSingleFrameCurve(property.boolValue ? 1f : 0f));
                    break;
                case SerializedPropertyType.Float:
                    result.AddFloatCurve(binding, CreateSingleFrameCurve(property.floatValue));
                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Character:
                    result.AddFloatCurve(binding, CreateSingleFrameCurve(property.intValue));
                    break;
                case SerializedPropertyType.ObjectReference:
                    result.AddObjectCurve(
                        binding,
                        new[]
                        {
                            new ObjectReferenceKeyframe
                            {
                                time = 0f,
                                value = property.objectReferenceValue
                            }
                        });
                    break;
            }
        }

        return result;
    }

    private static bool TryGetTransformValue(
        Transform transform,
        string propertyName,
        out float value)
    {
        switch (propertyName)
        {
            case "m_LocalPosition.x":
            case "localPosition.x":
                value = transform.localPosition.x;
                return true;
            case "m_LocalPosition.y":
            case "localPosition.y":
                value = transform.localPosition.y;
                return true;
            case "m_LocalPosition.z":
            case "localPosition.z":
                value = transform.localPosition.z;
                return true;
            case "m_LocalRotation.x":
            case "localRotation.x":
                value = transform.localRotation.x;
                return true;
            case "m_LocalRotation.y":
            case "localRotation.y":
                value = transform.localRotation.y;
                return true;
            case "m_LocalRotation.z":
            case "localRotation.z":
                value = transform.localRotation.z;
                return true;
            case "m_LocalRotation.w":
            case "localRotation.w":
                value = transform.localRotation.w;
                return true;
            case "m_LocalEulerAnglesRaw.x":
            case "localEulerAnglesRaw.x":
                value = transform.localEulerAngles.x;
                return true;
            case "m_LocalEulerAnglesRaw.y":
            case "localEulerAnglesRaw.y":
                value = transform.localEulerAngles.y;
                return true;
            case "m_LocalEulerAnglesRaw.z":
            case "localEulerAnglesRaw.z":
                value = transform.localEulerAngles.z;
                return true;
            case "m_LocalScale.x":
            case "localScale.x":
                value = transform.localScale.x;
                return true;
            case "m_LocalScale.y":
            case "localScale.y":
                value = transform.localScale.y;
                return true;
            case "m_LocalScale.z":
            case "localScale.z":
                value = transform.localScale.z;
                return true;
            default:
                value = 0f;
                return false;
        }
    }

    private static AnimationCurve CreateSingleFrameCurve(float value)
        => new(new Keyframe(0f, value));

}