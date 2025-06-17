using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace com.aoyon.facetune.animator;

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
        var behavior = stateMachine.Behaviours.OfType<TBehavior>().FirstOrNull();
        if (behavior == null)
        {
            behavior = ScriptableObject.CreateInstance<TBehavior>();
            stateMachine.Behaviours = stateMachine.Behaviours.Add(behavior);
        }
        return behavior;
    }

    public static TBehavior EnsureBehavior<TBehavior>(this VirtualState state) where TBehavior : StateMachineBehaviour
    {
        var behavior = state.Behaviours.OfType<TBehavior>().FirstOrNull();
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

    // https://gist.github.com/nekobako/5a38c040261e2eb815330535857003ca
    public static List<GenericAnimation> GetDefaultValueAnimations(GameObject root, IEnumerable<SerializableCurveBinding> curveBindings)
    {
        var animations = new List<GenericAnimation>();
        foreach (var curveBinding_ in curveBindings)
        {
            var curveBinding = curveBinding_.ToEditorCurveBinding();
            var target = AnimationUtility.GetAnimatedObject(root, curveBinding);
            switch (target)
            {
                case null:
                    // Debug.LogWarning($"Target is null: {curveBinding}");
                    break;
                case Animator:
                    //Debug.Log($"{curveBinding.path}, {curveBinding.type}, {curveBinding.propertyName}, AAP");
                    continue;
                case SkinnedMeshRenderer renderer when curveBinding.type == typeof(SkinnedMeshRenderer) && curveBinding.propertyName.StartsWith("blendShape."):
                    var index = renderer.sharedMesh.GetBlendShapeIndex(curveBinding.propertyName);
                    var weight = renderer.GetBlendShapeWeight(index);
                    animations.Add(new GenericAnimation(curveBinding_, new AnimationCurve(new Keyframe(0, weight))));
                    break;
            }

            using var so = new SerializedObject(target);
            using var prop = so.FindProperty(curveBinding.propertyName);
            if (prop == null) { Debug.LogWarning($"Property is null: {curveBinding}"); continue; }
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    var value = prop.boolValue ? 1f : 0f;
                    animations.Add(new GenericAnimation(curveBinding_, new AnimationCurve(new Keyframe(0, value))));
                    break;
                case SerializedPropertyType.Float:
                    animations.Add(new GenericAnimation(curveBinding_, new AnimationCurve(new Keyframe(0, prop.floatValue))));
                    break;
                case SerializedPropertyType.ObjectReference:
                    var objectReference = prop.objectReferenceValue;
                    if (objectReference == null) { continue; }
                    animations.Add(new GenericAnimation(curveBinding_, new List<SerializableObjectReferenceKeyframe>(){ new(0, objectReference) }));
                    break;
                default:
                    continue;
            }
        }
        return animations;
    }

    public static VirtualClip GetOrCreateClip(this VirtualState state, string name)
    {
        var motion = state.Motion as VirtualClip;
        if (motion == null)
        {
            motion = VirtualClip.Create(name);
            state.Motion = motion;
        }
        else
        {
            motion.Name = name;
        }
        return motion;
    }
}