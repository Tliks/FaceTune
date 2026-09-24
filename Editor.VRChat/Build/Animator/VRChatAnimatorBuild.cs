using nadena.dev.modular_avatar.core;
using Aoyon.FaceTune.Build;
using Aoyon.FaceTune.Platforms;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static partial class VRChatAnimatorBuilder
{
    private static readonly Vector3 InitialDefaultStatePosition = new(300, 0, 0);
    private const int InitialLayerPriority = -1;
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
        var initialBlendShapes = settings.GetManagedBlendShapesForAnyWriteKind()
            .Where(shape => !externalLipSyncBlendShapes.Contains(shape.Name))
            .Select(shape => proxy.ProxyNames.Contains(shape.Name)
                ? shape with { Weight = 0f }
                : shape)
            .ToArray();
        buildContext.GetState<VRChatInitialBlendShapeState>().BlendShapes =
            initialBlendShapes;

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

        var nonFacialDefaults = AnimatorHelper.GetDefaultValueAnimations(
            settings.AvatarContext.Root,
            expressionPlan.Items
                .SelectMany(item => item.NonFacialAnimations.FloatCurves
                    .Select(entry => entry.Key)
                    .Concat(item.NonFacialAnimations.ObjectCurves.Select(entry => entry.Key))));

        var graph = new AnimatorGraph(
            analyzedWriteDefaults ?? true,
            controllerContext.CloneContext);
        var mmdSupport = new MmdSupport(graph, avatarControlSettings.MmdPlayback);
        var afkSupport = new AfkSupport(avatarControlSettings.SupportAfk);
        var useInactiveAap = (!mmdSupport.PlaybackWhen.IsNever
            && !mmdSupport.DisableFxLayer
            && (units.Length > 0 || trackingPlan.ShouldBuildAnyLayer))
            || !afkSupport.PlaybackWhen.IsNever;
        var aap = new AapProtocol(trackingPlan, useInactiveAap);

        if (units.Length > 0 || useInactiveAap)
        {
            using var _ = new Utils.ProfilingSampleScope(
                "Build.Animator.BuildInitial");
            var initialAnchor = units.Length > 0
                ? units[0].Anchor
                : buildContext.AvatarRootTransform;
            var initialController = CreateMergeAnimatorController(
                controllerContext,
                initialAnchor,
                "Initial",
                InitialLayerPriority);
            BuildInitialLayer(
                initialController,
                graph,
                settings,
                initialBlendShapes,
                nonFacialDefaults,
                mmdSupport,
                aap,
                afkSupport);
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

        if (settings.AvoidEyeBlinkConflicts && trackingPlan.ShouldBuildEyeBlinkLayer
            || settings.AvoidLipSyncConflicts && trackingPlan.ShouldBuildLipSyncLayer)
        {
            using var _ = new Utils.ProfilingSampleScope(
                "Build.Animator.ReplaceExternalTrackingControls");
            ReplaceExternalTrackingControls(
                controllerContext,
                aap,
                settings.AvoidEyeBlinkConflicts && trackingPlan.ShouldBuildEyeBlinkLayer,
                settings.AvoidLipSyncConflicts && trackingPlan.ShouldBuildLipSyncLayer);
        }

        var eyeBlinkBuilder = new EyeBlinkAnimatorBuilder(
            settings.AvatarContext,
            graph,
            trackingPlan,
            aap);
        var lipSyncCancellerBuilder = new LipSyncCancellerAnimatorBuilder(
            settings.AvatarContext,
            graph,
            trackingPlan,
            aap);
        var lipSyncBuilder = new LipSyncAnimatorBuilder(
            settings.AvatarContext,
            graph,
            trackingPlan,
            aap);
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
            if (trackingPlan.ShouldBuildEyeBlinkLayer)
                eyeBlinkBuilder.Build(controlController, TrackingControlLayerPriority);
            if (trackingPlan.ShouldBuildLipSyncCancellerLayer)
                lipSyncCancellerBuilder.Build(controlController, TrackingControlLayerPriority);
            if (trackingPlan.ShouldBuildLipSyncLayer)
                lipSyncBuilder.Build(controlController, TrackingControlLayerPriority);
        }
    }

    private static void BuildInitialLayer(
        VirtualAnimatorController controller,
        AnimatorGraph graph,
        BuildSettings settings,
        IReadOnlyList<BlendShapeWeight> blendShapes,
        ResolvedNonFacialAnimationSet nonFacialDefaults,
        MmdSupport mmdSupport,
        AapProtocol aap,
        AfkSupport afkSupport)
    {
        var mmdWhen = mmdSupport.PlaybackWhen.Except(afkSupport.PlaybackWhen);
        AnimatorGraph.EnsureConditionParameters(controller, mmdWhen);
        aap.EnsureExpressionInactiveParameter(controller);

        var origin = InitialDefaultStatePosition;
        var layer = graph.AddLayer(controller, "Initial", InitialLayerPriority);
        var defaultState = graph.AddState(layer, "Default", origin);
        layer.StateMachine!.DefaultState = defaultState;
        SetInitialClip(
            defaultState,
            "Default",
            blendShapes,
            settings.AvatarContext.BodyPath,
            nonFacialDefaults);
        mmdSupport.AddInitialMmdState(
            layer,
            defaultState,
            mmdWhen,
            blendShapes,
            origin + new Vector3(0, AnimatorGraph.PositionYStep * 2, 0),
            settings.AvatarContext.BodyPath);
        afkSupport.AddInitialState(
            controller,
            graph,
            layer,
            defaultState,
            blendShapes,
            settings.AvatarContext.BodyPath,
            origin + new Vector3(0, AnimatorGraph.PositionYStep * 4, 0));
    }

    private static void SetInitialClip(
        VirtualState state,
        string name,
        IEnumerable<BlendShapeWeight> blendShapes,
        string bodyPath,
        ResolvedNonFacialAnimationSet? nonFacialAnimations = null)
    {
        var clip = state.SetNewClip(name);
        if (nonFacialAnimations != null)
        {
            foreach (var (binding, curve) in nonFacialAnimations.FloatCurves)
                clip.SetFloatCurve(binding, curve);
            foreach (var (binding, curve) in nonFacialAnimations.ObjectCurves)
                clip.SetObjectCurve(binding, curve);
        }
        clip.AddBlendShapeAnimations(bodyPath, blendShapes.ToBlendShapeAnimations());
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

internal sealed class VRChatInitialBlendShapeState
{
    public IReadOnlyList<BlendShapeWeight> BlendShapes { get; set; } =
        Array.Empty<BlendShapeWeight>();
}