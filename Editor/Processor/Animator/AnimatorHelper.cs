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
}