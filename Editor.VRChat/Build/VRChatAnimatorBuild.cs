using nadena.dev.modular_avatar.core;
using Aoyon.FaceTune.Build;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static class VRChatAnimatorBuilder
{
    public static void Build(
        BuildContext buildContext,
        BuildSettings settings,
        AvatarControlSettings avatarControlSettings,
        ExpressionPlan expressionProgram)
    {
        if (expressionProgram.IsEmpty) return;

        var controllerContext = buildContext.Extension<VirtualControllerContext>();
        var fx = controllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];

        bool? analyzedWriteDefaults;
        using (new Utils.ProfilingSampleScope("FaceTune.Build.Animator.AnalyzeWriteDefaults"))
        {
            analyzedWriteDefaults = AnimatorHelper.AnalyzeLayerWriteDefaults(fx);
        }

        var mmdPolicy = ResolveMmdAnimatorPolicy(avatarControlSettings, analyzedWriteDefaults);

        ISet<Transform> unitBoundaryTransforms;
        using (new Utils.ProfilingSampleScope("FaceTune.Build.Animator.FindUnitBoundaries"))
        {
            unitBoundaryTransforms = FindUnitBoundaryTransforms(settings, controllerContext);
        }

        AnimatorBuildPlan animatorPlan;
        using (new Utils.ProfilingSampleScope("FaceTune.Build.Animator.BuildPlan"))
        {
            animatorPlan = AnimatorBuildPlanBuilder.Build(
                expressionProgram,
                settings,
                avatarControlSettings,
                unitBoundaryTransforms,
                mmdPolicy);
        }

        var installer = new AnimatorInstaller(
            settings.AvatarContext,
            analyzedWriteDefaults ?? true);

        using (new Utils.ProfilingSampleScope("FaceTune.Build.Animator.InstallInitial"))
        {
            var initialController = CreateMergeAnimatorController(
                controllerContext,
                animatorPlan.InitialLayer.Anchor,
                animatorPlan.InitialLayer.Name,
                animatorPlan.InitialLayer.Priority);
            installer.InstallInitial(
                initialController,
                animatorPlan.InitialLayer,
                animatorPlan.InitialLayer.Priority);
        }

        using (new Utils.ProfilingSampleScope("FaceTune.Build.Animator.InstallUnits"))
        {
            foreach (var unit in animatorPlan.Units)
            {
                var unitController = CreateMergeAnimatorController(
                    controllerContext,
                    unit.Anchor,
                    $"Unit {unit.Id}",
                    unit.Priority);
                installer.InstallUnit(unitController, unit, unit.Priority);
            }
        }

        if (animatorPlan.EyeBlinkLayer != null || animatorPlan.LipSyncLayer != null)
        {
            using var _ = new Utils.ProfilingSampleScope(
                "FaceTune.Build.Animator.InstallTrackingControls");
            var controlController = CreateMergeAnimatorController(
                controllerContext,
                animatorPlan.ControlAnchor,
                "Tracking Controls",
                animatorPlan.ControlPriority);
            if (animatorPlan.EyeBlinkLayer is { } eyeBlink)
            {
                installer.InstallEyeBlink(
                    controlController,
                    eyeBlink,
                    animatorPlan.ControlPriority);
            }
            if (animatorPlan.LipSyncLayer is { } lipSync)
            {
                installer.InstallLipSync(
                    controlController,
                    lipSync,
                    animatorPlan.ControlPriority);
            }
        }
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
            .Where(name => !settings.IsBlendShapeExcluded(name))
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

    private static MmdAnimatorPolicy ResolveMmdAnimatorPolicy(
        AvatarControlSettings avatarControlSettings,
        bool? analyzedWriteDefaults)
    {
        var playback = avatarControlSettings.MmdPlayback;
        if (!playback.Enabled) return new(null, MmdDisableMode.None);

        var playbackWhen = playback.DisableWhen ?? DnfCondition.Always;
        if (playback.DisableWhen == null)
            return new(playbackWhen, MmdDisableMode.None);

        var mode = playback.DisableMode == MMDSupportSettings.Mode.Auto
            ? analyzedWriteDefaults == true
                ? MMDSupportSettings.Mode.DisableLayers
                : MMDSupportSettings.Mode.DisableFXlayer
            : playback.DisableMode;
        return new MmdAnimatorPolicy(
            playbackWhen,
            mode switch
            {
                MMDSupportSettings.Mode.DisableLayers => MmdDisableMode.DisableLayers,
                MMDSupportSettings.Mode.DisableFXlayer => MmdDisableMode.DisableFxLayer,
                _ => throw new ArgumentOutOfRangeException(nameof(playback.DisableMode), playback.DisableMode, null)
            });
    }
}
