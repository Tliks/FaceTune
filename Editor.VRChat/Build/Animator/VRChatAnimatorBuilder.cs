using nadena.dev.modular_avatar.core;
using Aoyon.FaceTune.Build;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static partial class VRChatAnimatorBuilder
{
    private const int TrackingControlLayerPriority = int.MaxValue - 1;

    public static void Build(
        BuildContext buildContext,
        BuildSettings settings,
        AvatarControlSettings avatarControlSettings,
        ExpressionPlan expressionPlan)
    {
        var controllerContext = buildContext.Extension<VirtualControllerContext>();
        var fx = controllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];

        var externalLipSyncBlendShapes = settings.AvatarContext.Root
            .TryGetComponent<VRCAvatarDescriptor>(out var descriptor)
            ? new VRChatSupport(descriptor).GetBuldInLipSyncBlendShapes().ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var proxy = CustomLipSyncBlendShapeProxy.Apply(
            buildContext,
            settings,
            expressionPlan,
            externalLipSyncBlendShapes);
        settings = proxy.Settings;
        expressionPlan = proxy.Expressions;

        var facialDefaults = buildContext.GetState<VRChatFacialDefaultsState>();
        facialDefaults.BlendShapes = VRChatFacialDefaultsState.ResolveBlendShapes(
            settings,
            externalLipSyncBlendShapes,
            proxy.ProxyNames);

        var trackingPlan = VRChatTrackingPlan.Build(
            expressionPlan.Items,
            avatarControlSettings);
        if (expressionPlan.IsEmpty && !trackingPlan.ShouldBuildAnyLayer) return;

        bool? analyzedWriteDefaults;
        using (new Utils.ProfilingSampleScope("Build.Animator.AnalyzeWriteDefaults"))
        {
            analyzedWriteDefaults = AnimatorHelper.AnalyzeLayerWriteDefaults(fx);
        }

        var units = ResolveUnits(settings, expressionPlan, controllerContext);

        var mmdSupport = new MmdSupport(avatarControlSettings.MmdPlayback);
        var afkSupport = new AfkSupport(avatarControlSettings.SupportAfk);
        var useInactiveAap = (!mmdSupport.PlaybackWhen.IsNever
            && !mmdSupport.DisableFxLayer
            && (!expressionPlan.IsEmpty || trackingPlan.ShouldBuildAnyLayer))
            || !afkSupport.PlaybackWhen.IsNever;
        var aap = new AapProtocol(trackingPlan, useInactiveAap);
        var graph = new AnimatorGraph(
            analyzedWriteDefaults ?? true,
            controllerContext.CloneContext);

        var replaceEyeBlink = settings.AvoidEyeBlinkConflicts
            && trackingPlan.ShouldBuildEyeBlinkLayer;
        var replaceLipSync = settings.AvoidLipSyncConflicts
            && trackingPlan.ShouldBuildLipSyncLayer;
        if (replaceEyeBlink || replaceLipSync)
        {
            using var _ = new Utils.ProfilingSampleScope(
                "Build.Animator.ReplaceExternalTrackingControls");
            VRChatExternalTrackingControlRewriter.Apply(
                controllerContext,
                aap,
                replaceEyeBlink,
                replaceLipSync);
        }

        if (!expressionPlan.IsEmpty || useInactiveAap)
        {
            using var _ = new Utils.ProfilingSampleScope(
                "Build.Animator.BuildInitial");
            var initialAnchor = expressionPlan.IsEmpty
                ? buildContext.AvatarRootTransform
                : units[0].Anchor;
            var initialBuilder = new VRChatInitialLayerBuilder(
                settings, expressionPlan, facialDefaults.BlendShapes, graph);
            var initialController = CreateMergeAnimatorController(
                controllerContext,
                initialAnchor,
                "Initial",
                VRChatInitialLayerBuilder.LayerPriority);
            initialBuilder.Build(initialController, mmdSupport, afkSupport, aap);
        }

        var expressionBuilder = new ExpressionAnimatorBuilder(
            settings,
            graph,
            avatarControlSettings,
            aap);
        using (new Utils.ProfilingSampleScope("Build.Animator.BuildUnits"))
        {
            foreach (var unit in units)
            {
                var unitController = CreateMergeAnimatorController(
                    controllerContext,
                    unit.Anchor,
                    $"Unit {unit.Id}",
                    unit.Priority);
                expressionBuilder.Build(
                    unitController,
                    unit.Id,
                    unit.Expressions,
                    unit.Priority);
            }
        }

        if (trackingPlan.ShouldBuildAnyLayer)
        {
            using var _ = new Utils.ProfilingSampleScope(
                "Build.Animator.BuildTrackingControls");
            var controlAnchor = new GameObject($"{FaceTuneConstants.Name} Tracking Controls");
            controlAnchor.transform.SetParent(buildContext.AvatarRootTransform, false);
            var controlController = CreateMergeAnimatorController(
                controllerContext,
                controlAnchor.transform,
                "Tracking Controls",
                TrackingControlLayerPriority);

            var eyeBlinkBuilder = new EyeBlinkAnimatorBuilder(
                settings.AvatarContext, graph, trackingPlan, aap);
            var lipSyncCancellerBuilder = new LipSyncCancellerAnimatorBuilder(
                settings.AvatarContext, graph, trackingPlan, aap);
            var lipSyncBuilder = new LipSyncAnimatorBuilder(
                settings.AvatarContext, graph, trackingPlan, aap);

            if (trackingPlan.ShouldBuildEyeBlinkLayer)
                eyeBlinkBuilder.Build(controlController, TrackingControlLayerPriority);
            if (trackingPlan.ShouldBuildLipSyncCancellerLayer)
                lipSyncCancellerBuilder.Build(controlController, TrackingControlLayerPriority);
            if (trackingPlan.ShouldBuildLipSyncLayer)
                lipSyncBuilder.Build(controlController, TrackingControlLayerPriority);
        }
    }

    private static VirtualAnimatorController CreateMergeAnimatorController(
        VirtualControllerContext controllerContext,
        Transform anchor,
        string name,
        int priority)
    {
        using var _ = new Utils.ProfilingSampleScope("Animator.CreateMergeController");
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
        AnimatorGraph.EnsureAlwaysParameter(controller);
        controllerContext.Controllers[merge] = controller;
        return controller;
    }
}

internal sealed class VRChatFacialDefaultsState
{
    public IReadOnlyList<BlendShapeWeight> BlendShapes { get; set; } =
        Array.Empty<BlendShapeWeight>();

    public static BlendShapeWeight[] ResolveBlendShapes(
        BuildSettings settings,
        ISet<string> externalLipSyncBlendShapes,
        ISet<string> proxyNames)
        => settings.GetManagedBlendShapesForAnyWriteKind()
            .Where(shape => !externalLipSyncBlendShapes.Contains(shape.Name))
            .Select(shape => proxyNames.Contains(shape.Name)
                ? shape with { Weight = 0f }
                : shape)
            .ToArray();
}