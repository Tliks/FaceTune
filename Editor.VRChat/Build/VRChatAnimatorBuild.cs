using nadena.dev.modular_avatar.core;
using Aoyon.FaceTune.Build;
using Aoyon.FaceTune.Build.Animator;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static class VRChatAnimatorBuilder
{
    private const int InitialControllerPriority = -1;
    private const int UnitControllerPriority = 0;
    private const int TrackingControlControllerPriority = 0;

    public static void Emit(
        BuildContext buildContext,
        BuildSettings settings,
        ExpressionProgram expressionProgram)
    {
        if (expressionProgram.IsEmpty) return;

        var controllerContext = buildContext.Extension<VirtualControllerContext>();
        var fx = controllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];

        bool? analyzedWriteDefaults;
        using (new Utils.ProfilingSampleScope("FaceTune.Emit.Animator.AnalyzeWriteDefaults"))
        {
            analyzedWriteDefaults = AnimatorHelper.AnalyzeLayerWriteDefaults(fx);
        }

        var platformServices = new VRChatAnimatorPlatformServices(analyzedWriteDefaults);

        ISet<Transform> unitBoundaryTransforms;
        using (new Utils.ProfilingSampleScope("FaceTune.Emit.Animator.FindUnitBoundaries"))
        {
            unitBoundaryTransforms = FindUnitBoundaryTransforms(settings, controllerContext);
        }

        AnimatorBuildPlan animatorPlan;
        using (new Utils.ProfilingSampleScope("FaceTune.Emit.Animator.BuildPlan"))
        {
            animatorPlan = AnimatorBuildPlanBuilder.Build(
                expressionProgram,
                settings,
                unitBoundaryTransforms,
                platformServices);
        }

        var installer = new AnimatorInstaller(
            settings.AvatarContext,
            analyzedWriteDefaults ?? true,
            platformServices);

        using (new Utils.ProfilingSampleScope("FaceTune.Emit.Animator.InstallInitial"))
        {
            var initialController = CreateMergeAnimatorController(
                controllerContext,
                animatorPlan.InitialLayer.Anchor,
                animatorPlan.InitialLayer.Name,
                InitialControllerPriority);
            installer.InstallInitial(initialController, animatorPlan.InitialLayer, InitialControllerPriority);
        }

        using (new Utils.ProfilingSampleScope("FaceTune.Emit.Animator.InstallUnits"))
        {
            foreach (var unit in animatorPlan.Units)
            {
                var unitController = CreateMergeAnimatorController(
                    controllerContext,
                    unit.Anchor,
                    $"Unit {unit.Id}",
                    UnitControllerPriority);
                installer.InstallUnit(unitController, unit, UnitControllerPriority);
            }
        }

        if (animatorPlan.TrackingControlLayer is { } trackingControl)
        {
            using (new Utils.ProfilingSampleScope("FaceTune.Emit.Animator.InstallTrackingControl"))
            {
                var trackingController = CreateMergeAnimatorController(
                    controllerContext,
                    trackingControl.Anchor,
                    trackingControl.Name,
                    TrackingControlControllerPriority);
                installer.InstallTrackingControl(
                    trackingController,
                    trackingControl,
                    TrackingControlControllerPriority);
            }
        }

        // Controller disable remains a VRChat-specific controller-assignment concern.
    }

    private static VirtualAnimatorController CreateMergeAnimatorController(
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

    private static ISet<Transform> FindUnitBoundaryTransforms(
        BuildSettings settings,
        VirtualControllerContext controllerContext)
    {
        var managedBlendShapeNames = settings.AvatarContext.FaceMesh.GetBlendShapeNames()
            .Where(name => !settings.ExcludedBlendShapeNames.Contains(name))
            .ToHashSet();
        if (managedBlendShapeNames.Count == 0) return new HashSet<Transform>();

        return settings.AvatarContext.Root.GetComponentsInChildren<Transform>(true)
            .Where(transform => IsUnitBoundaryTransform(transform, controllerContext, managedBlendShapeNames))
            .ToHashSet();
    }

    private static bool IsUnitBoundaryTransform(
        Transform transform,
        VirtualControllerContext controllerContext,
        ISet<string> managedBlendShapeNames)
    {
        return transform.TryGetComponent<ModularAvatarMergeAnimator>(out var merge)
               && merge.layerType == VRCAvatarDescriptor.AnimLayerType.FX
               && controllerContext.Controllers.TryGetValue(merge, out var controller)
               && OverlapsManagedBlendShapes(controller, managedBlendShapeNames);
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

    private static DnfCondition ParameterBool(string parameterName, bool value)
    {
        return DnfCondition.Single(
            AnimatorConditionRule.FromParameterCondition(ParameterCondition.Bool(parameterName, value)),
            ParameterDomainRegistry.Empty);
    }

    private sealed class VRChatAnimatorPlatformServices : IAnimatorPlatformServices
    {
        private readonly bool? _analyzedWriteDefaults;

        public VRChatAnimatorPlatformServices(bool? analyzedWriteDefaults)
        {
            _analyzedWriteDefaults = analyzedWriteDefaults;
        }

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

        public DnfCondition? GetLayerForceInactiveWhen(BuildSettings settings)
            => ResolveMmdAnimatorPolicy(settings, _analyzedWriteDefaults).LayerForceInactiveWhen;

        public InitialLayerPlan TransformInitialLayer(InitialLayerPlan initial, BuildSettings settings)
        {
            var policy = ResolveMmdAnimatorPolicy(settings, _analyzedWriteDefaults);
            if (policy.BlendShapePassthroughWhen == null || settings.MmdPlayback.BlendShapeNames.Count == 0)
                return initial;

            var mmdPlaybackState = new InitialStatePlan(
                "MMD Playback",
                policy.BlendShapePassthroughWhen,
                initial.DefaultState.BlendShapes
                    .Where(shape => !settings.MmdPlayback.BlendShapeNames.Contains(shape.Name))
                    .ToArray());
            return initial with
            {
                DefaultState = initial.DefaultState with { When = policy.BlendShapePassthroughWhen },
                States = initial.States.Append(mmdPlaybackState).ToArray()
            };
        }

        private readonly record struct MmdAnimatorPolicy(
            DnfCondition? BlendShapePassthroughWhen,
            DnfCondition? LayerForceInactiveWhen,
            DnfCondition? ControllerDisableWhen);

        private static MmdAnimatorPolicy ResolveMmdAnimatorPolicy(BuildSettings settings, bool? analyzedWriteDefaults)
        {
            var playback = settings.MmdPlayback;
            if (!playback.Enabled)
                return new(null, null, null);

            var passthroughWhen = playback.DisableWhen == null
                ? DnfCondition.Always
                : DnfCondition.All(new[]
                {
                    playback.DisableWhen,
                    ParameterBool("InStation", true),
                    ParameterBool("Seated", false)
                });
            if (playback.DisableWhen == null)
                return new(passthroughWhen, null, null);

            var mode = playback.DisableMode == MMDSupportSettings.Mode.Auto
                ? analyzedWriteDefaults == true ? MMDSupportSettings.Mode.DisableLayers : MMDSupportSettings.Mode.DisableFXlayer
                : playback.DisableMode;

            return mode switch
            {
                MMDSupportSettings.Mode.DisableLayers => new(passthroughWhen, passthroughWhen, null),
                MMDSupportSettings.Mode.DisableFXlayer => new(null, null, passthroughWhen),
                _ => throw new ArgumentOutOfRangeException(nameof(playback.DisableMode), playback.DisableMode, null)
            };
        }
    }
}
