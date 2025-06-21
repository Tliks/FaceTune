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
            var propName = curveBinding.propertyName;
            switch (target)
            {
                case null:
                    Debug.LogWarning($"Target is null: path: {curveBinding.path}, type: {curveBinding.type}, propertyName: {propName}");
                    continue;
                case Animator:
                    //Debug.Log($"{curveBinding.path}, {curveBinding.type}, {propName}, AAP");
                    continue;
                case SkinnedMeshRenderer renderer when curveBinding.type == typeof(SkinnedMeshRenderer) && propName.StartsWith("blendShape."):
                    var index = renderer.sharedMesh.GetBlendShapeIndex(propName);
                    var weight = renderer.GetBlendShapeWeight(index);
                    animations.Add(CreateFloatCurveAnimation(curveBinding_, weight));
                    continue;
                case Transform transform:
                {
                    if (propName.StartsWith("localPosition."))
                    {
                        var pos = transform.localPosition;
                        float? value = null;
                        if (propName.EndsWith(".x")) value = pos.x;
                        else if (propName.EndsWith(".y")) value = pos.y;
                        else if (propName.EndsWith(".z")) value = pos.z;

                        if (value.HasValue)
                        {
                            animations.Add(CreateFloatCurveAnimation(curveBinding_, value.Value));
                            continue;
                        }
                    }
                    else if (propName.StartsWith("localRotation."))
                    {
                        var rot = transform.localRotation;
                        float? value = null;
                        if (propName.EndsWith(".x")) value = rot.x;
                        else if (propName.EndsWith(".y")) value = rot.y;
                        else if (propName.EndsWith(".z")) value = rot.z;
                        else if (propName.EndsWith(".w")) value = rot.w;

                        if (value.HasValue)
                        {
                            animations.Add(CreateFloatCurveAnimation(curveBinding_, value.Value));
                            continue;
                        }
                    }
                    if (propName.StartsWith("localEulerAnglesRaw."))
                    {
                        var euler = transform.localEulerAngles;
                        float? value = null;
                        if (propName.EndsWith(".x")) value = euler.x;
                        else if (propName.EndsWith(".y")) value = euler.y;
                        else if (propName.EndsWith(".z")) value = euler.z;

                        if (value.HasValue)
                        {
                            animations.Add(CreateFloatCurveAnimation(curveBinding_, value.Value));
                            continue;
                        }
                    }
                    else if (propName.StartsWith("localScale."))
                    {
                        var scale = transform.localScale;
                        float? value = null;
                        if (propName.EndsWith(".x")) value = scale.x;
                        else if (propName.EndsWith(".y")) value = scale.y;
                        else if (propName.EndsWith(".z")) value = scale.z;

                        if (value.HasValue)
                        {
                            animations.Add(CreateFloatCurveAnimation(curveBinding_, value.Value));
                            continue;
                        }
                    }
                    break;
                }
            }

            using var so = new SerializedObject(target);
            using var prop = so.FindProperty(propName);
            if (prop == null) { Debug.LogWarning($"Property is null: path: {curveBinding.path}, type: {curveBinding.type}, propertyName: {propName}, target: {target}"); continue; }
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    var value = prop.boolValue ? 1f : 0f;
                    animations.Add(CreateFloatCurveAnimation(curveBinding_, value));
                    break;
                case SerializedPropertyType.Float:
                    animations.Add(CreateFloatCurveAnimation(curveBinding_, prop.floatValue));
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

    private static GenericAnimation CreateFloatCurveAnimation(SerializableCurveBinding binding, float value)
    {
        return new GenericAnimation(binding, new AnimationCurve(new Keyframe(0, value)));
    }

    public static bool TryGetClip(this VirtualState state, [NotNullWhen(true)] out VirtualClip? clip)
    {
        var motion = state.Motion as VirtualClip;
        clip = motion;
        return motion != null;
    }

    public static VirtualClip CreateClip(this VirtualState state, string name)
    {
        var clip = VirtualClip.Create(name);
        state.Motion = clip;
        return clip;
    }

    public static VirtualClip GetOrCreateClip(this VirtualState state, string name)
    {
        if (state.TryGetClip(out var clip))
        {
            clip.Name = name;
            return clip;
        }
        clip = CreateClip(state, name);
        return clip;
    }

    // 適当なGameObjectのactiveを切り替える2フレームアニメーションを作成
    public static VirtualClip CreateCustomEmpty(string clipName = "Custom Empty Clip")
    {
        var clip = VirtualClip.Create(clipName);

        var curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(1f / clip.FrameRate, 0f);

        clip.SetFloatCurve("", typeof(GameObject), "m_IsActive", curve);
        return clip;
    }

    public static void SetAnimation(this VirtualClip clip, GenericAnimation animation)
    {
        var binding = animation.CurveBinding.ToEditorCurveBinding();
        clip.SetFloatCurve(binding.path, binding.type, binding.propertyName, animation.Curve);
    }

    public static void SetAnimations(this VirtualClip clip, IEnumerable<GenericAnimation> animations)
    {
        foreach (var animation in animations)
        {
            SetAnimation(clip, animation);
        }
    }
}