using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static class VRChatExternalTrackingControlRewriter
{
    public static void Apply(
        VirtualControllerContext controllerContext,
        AapProtocol aap,
        bool replaceEyeBlink,
        bool replaceLipSync)
    {
        if (!replaceEyeBlink && !replaceLipSync) return;

        foreach (var (key, controller) in controllerContext.Controllers)
        {
            if (!IsFxController(key)) continue;

            foreach (var layer in controller.Layers)
            {
                if (layer.StateMachine == null) continue;
                foreach (var state in layer.StateMachine.AllStates())
                    Apply(
                        controller,
                        state,
                        aap,
                        replaceEyeBlink,
                        replaceLipSync);
            }
        }
    }

    private static void Apply(
        VirtualAnimatorController controller,
        VirtualState state,
        AapProtocol aap,
        bool replaceEyeBlink,
        bool replaceLipSync)
    {
        var behaviours = state.Behaviours;
        var trackingControls = behaviours.OfType<VRCAnimatorTrackingControl>().ToArray();
        if (trackingControls.Length == 0) return;

        var writes = new List<(string ParameterName, float Value)>();
        foreach (var control in trackingControls)
        {
            var eyeTracking = replaceEyeBlink
                ? control.trackingEyes
                : VRCAnimatorTrackingControl.TrackingType.NoChange;
            var mouthTracking = replaceLipSync
                ? control.trackingMouth
                : VRCAnimatorTrackingControl.TrackingType.NoChange;
            writes.AddRange(aap.BuildTrackingReplacementWrites(eyeTracking, mouthTracking));
            if (replaceEyeBlink)
                control.trackingEyes = VRCAnimatorTrackingControl.TrackingType.NoChange;
            if (replaceLipSync)
                control.trackingMouth = VRCAnimatorTrackingControl.TrackingType.NoChange;
        }

        if (writes.Count > 0)
        {
            aap.EnsureParameters(
                controller,
                writes.Select(write => write.ParameterName));
            AddAapWritesToClip(state, writes);
        }

        state.Behaviours = behaviours
            .Where(behavior => !IsNoOpTrackingControl(behavior))
            .ToImmutableList();
    }

    private static void AddAapWritesToClip(
        VirtualState state,
        IReadOnlyList<(string ParameterName, float Value)> writes)
    {
        var clips = CollectClips(state.Motion).ToArray();
        if (clips.Length == 0)
        {
            var clip = VirtualClip.Create("FaceTune Tracking");
            state.Motion = clip;
            clips = new[] { clip };
        }

        foreach (var clip in clips)
        {
            foreach (var write in writes)
                clip.SetAap(write.ParameterName, write.Value);
        }
    }

    private static IEnumerable<VirtualClip> CollectClips(VirtualMotion? motion)
    {
        switch (motion)
        {
            case VirtualClip clip:
                yield return clip;
                break;
            case VirtualBlendTree tree:
                foreach (var child in tree.Children)
                {
                    foreach (var clip in CollectClips(child.Motion)) yield return clip;
                }
                break;
        }
    }

    private static bool IsFxController(object key)
    {
        return key switch
        {
            VRCAvatarDescriptor.AnimLayerType layerType =>
                layerType == VRCAvatarDescriptor.AnimLayerType.FX,
            IVirtualizeAnimatorController virtualize
                when virtualize.TargetControllerKey
                    is VRCAvatarDescriptor.AnimLayerType targetLayer =>
                targetLayer == VRCAvatarDescriptor.AnimLayerType.FX,
            _ => false
        };
    }

    private static bool IsNoOpTrackingControl(StateMachineBehaviour behavior)
    {
        if (behavior is not VRCAnimatorTrackingControl control) return false;

        var noChange = VRCAnimatorTrackingControl.TrackingType.NoChange;
        return control.trackingHead == noChange
            && control.trackingLeftHand == noChange
            && control.trackingRightHand == noChange
            && control.trackingHip == noChange
            && control.trackingLeftFoot == noChange
            && control.trackingRightFoot == noChange
            && control.trackingLeftFingers == noChange
            && control.trackingRightFingers == noChange
            && control.trackingEyes == noChange
            && control.trackingMouth == noChange;
    }
}
