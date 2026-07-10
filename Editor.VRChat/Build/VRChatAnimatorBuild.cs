using Aoyon.FaceTune.Build;
using Aoyon.FaceTune.Build.Animator;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static class VRChatAnimatorBuilder
{
    public static void Emit(
        BuildContext buildContext,
        BuildSettings settings,
        ExpressionProgram expressionProgram)
    {
        if (expressionProgram.IsEmpty) return;

        var controllerContext = buildContext.Extension<VirtualControllerContext>();
        var fx = controllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
        var platformServices = new VRChatAnimatorPlatformServices();

        var analyzedWriteDefaults = AnimatorHelper.AnalyzeLayerWriteDefaults(fx);
        var mmdPolicy = ResolveMmdAnimatorPolicy(settings, analyzedWriteDefaults);

        var animatorPlan = AnimatorBuildPlanBuilder.Build(
            expressionProgram,
            settings,
            platformServices,
            controllerContext,
            mmdPolicy.LayerForceInactiveWhen);

        new AnimatorInstaller(
            controllerContext,
            settings.AvatarContext,
            analyzedWriteDefaults ?? true,
            platformServices,
            animatorPlan).Execute();

        // mmdPolicy.ControllerDisableWhen is intentionally left for
        // platform-specific controller assignment.
    }

    private static (DnfCondition? LayerForceInactiveWhen, DnfCondition? ControllerDisableWhen)
        ResolveMmdAnimatorPolicy(BuildSettings settings, bool? analyzedWriteDefaults)
    {
        var playback = settings.MmdPlayback;
        if (!playback.Enabled || string.IsNullOrWhiteSpace(playback.DisableParameterName))
        {
            return (null, null);
        }

        var disableWhen = DnfCondition.All(new[]
        {
            ParameterBool(playback.DisableParameterName, true),
            ParameterBool("InStation", true),
            ParameterBool("Seated", false)
        });
        var mode = playback.DisableMode == MmdDisableMode.Auto
            ? analyzedWriteDefaults == true ? MmdDisableMode.DisableLayer : MmdDisableMode.DisableFx
            : playback.DisableMode;

        return mode switch
        {
            MmdDisableMode.DisableLayer => (disableWhen, null),
            MmdDisableMode.DisableFx => (null, disableWhen),
            _ => throw new ArgumentOutOfRangeException(nameof(playback.DisableMode), playback.DisableMode, null)
        };
    }

    private static DnfCondition ParameterBool(string parameterName, bool value)
    {
        return DnfCondition.Single(
            AnimatorConditionRule.FromParameterCondition(ParameterCondition.Bool(parameterName, value)));
    }

    private sealed class VRChatAnimatorPlatformServices : IAnimatorPlatformServices
    {
        public DiscreteFloatParameterRange FloatRange => new(-1f, 1f, 255);

        public void SetEyeBlinkTracking(VirtualState state, bool isTracking)
        {
            var trackingControl = state.EnsureBehavior<VRCAnimatorTrackingControl>();
            trackingControl.trackingEyes = isTracking
                ? VRCAnimatorTrackingControl.TrackingType.Tracking
                : VRCAnimatorTrackingControl.TrackingType.Animation;
        }

        public void SetLipSyncTracking(VirtualState state, bool isTracking)
        {
            var trackingControl = state.EnsureBehavior<VRCAnimatorTrackingControl>();
            trackingControl.trackingMouth = isTracking
                ? VRCAnimatorTrackingControl.TrackingType.Tracking
                : VRCAnimatorTrackingControl.TrackingType.Animation;
        }

        public void AddRandomDriver(VirtualState state, string parameterName, float min, float max)
        {
            state.EnsureBehavior<VRCAvatarParameterDriver>().parameters.Add(
                new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Random,
                    name = parameterName,
                    valueMin = min,
                    valueMax = max
                });
        }

        public bool IsUnitBoundaryTransform(
            Transform transform,
            VirtualControllerContext controllerContext,
            ISet<string> managedBlendShapeNames)
        {
            return managedBlendShapeNames.Count != 0
                   && transform.TryGetComponent<ModularAvatarMergeAnimator>(out var merge)
                   && merge.layerType == VRCAvatarDescriptor.AnimLayerType.FX
                   && controllerContext.Controllers.TryGetValue(merge, out var controller)
                   && OverlapsManagedBlendShapes(controller, managedBlendShapeNames);
        }

        public VirtualAnimatorController CreateController(
            VirtualControllerContext controllerContext,
            Transform anchor,
            string name,
            int priority)
        {
            var merge = anchor.gameObject.AddComponent<ModularAvatarMergeAnimator>();
            merge.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            merge.deleteAttachedAnimator = false;
            merge.pathMode = MergeAnimatorPathMode.Absolute;
            merge.matchAvatarWriteDefaults = false;
            merge.layerPriority = priority;
            merge.mergeAnimatorMode = MergeAnimatorMode.Append;

            var controller = VirtualAnimatorController.Create(
                controllerContext.CloneContext,
                $"{FaceTuneConstants.Name}: {name}");
            controllerContext.Controllers[merge] = controller;
            return controller;
        }

        private static bool OverlapsManagedBlendShapes(
            VirtualAnimatorController controller,
            ISet<string> managedBlendShapeNames)
        {
            return CollectBlendShapeNames(controller).Any(managedBlendShapeNames.Contains);
        }

        private static IEnumerable<string> CollectBlendShapeNames(VirtualAnimatorController controller)
        {
            return controller.Layers
                .Where(layer => layer.StateMachine != null)
                .SelectMany(layer => layer.StateMachine!.AllStates())
                .SelectMany(state => CollectBlendShapeNames(state.Motion))
                .Distinct();
        }

        private static IEnumerable<string> CollectBlendShapeNames(VirtualMotion? motion)
        {
            return motion switch
            {
                VirtualClip clip => CollectBlendShapeNames(clip),
                VirtualBlendTree tree => tree.Children
                    .Where(child => child.Motion != null)
                    .SelectMany(child => CollectBlendShapeNames(child.Motion)),
                _ => Array.Empty<string>()
            };
        }

        private static IEnumerable<string> CollectBlendShapeNames(VirtualClip clip)
        {
            return clip.GetFloatCurveBindings()
                .Where(binding => binding.type == typeof(SkinnedMeshRenderer)
                                  && binding.propertyName.StartsWith("blendShape."))
                .Select(binding => binding.propertyName["blendShape.".Length..]);
        }
    }
}
