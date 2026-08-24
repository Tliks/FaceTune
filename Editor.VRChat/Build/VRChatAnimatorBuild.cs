using nadena.dev.modular_avatar.core;
using Aoyon.FaceTune.Build;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static class VRChatAnimatorBuilder
{
    private const int InitialLayerPriority = -1;

    public static void Build(
        BuildContext buildContext,
        BuildSettings settings,
        AvatarControlSettings avatarControlSettings,
        ExpressionPlan expressionPlan)
    {
        if (expressionPlan.IsEmpty) return;

        var controllerContext = buildContext.Extension<VirtualControllerContext>();
        var fx = controllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
        var externalLipSyncBlendShapes = settings.AvatarContext.Root
            .TryGetComponent<VRCAvatarDescriptor>(out var descriptor)
            ? new VRChatSupport(descriptor).GetLipSyncBlendShapes().ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        bool? analyzedWriteDefaults;
        using (new Utils.ProfilingSampleScope("FaceTune.Build.Animator.AnalyzeWriteDefaults"))
        {
            analyzedWriteDefaults = AnimatorHelper.AnalyzeLayerWriteDefaults(fx);
        }

        var mmdPolicy = ResolveMmdAnimatorPolicy(avatarControlSettings, analyzedWriteDefaults);
        var layerMmdPlaybackWhen = mmdPolicy.DisableLayers
            ? mmdPolicy.PlaybackWhen
            : null;

        ISet<Transform> unitBoundaryTransforms;
        using (new Utils.ProfilingSampleScope("FaceTune.Build.Animator.FindUnitBoundaries"))
        {
            unitBoundaryTransforms = FindUnitBoundaryTransforms(
                settings,
                expressionPlan,
                controllerContext);
        }

        var externalPartitions = FindExternalPartitions(
            expressionPlan,
            settings.AvatarContext,
            unitBoundaryTransforms);
        var units = expressionPlan.Items
            .GroupBy(item => (
                Priority: item.Priority.Priority,
                ExternalPartition: externalPartitions[item.SourceTransform]))
            .Select((group, id) => (
                Id: id,
                Priority: group.Key.Priority,
                Anchor: group.First().SourceTransform,
                Expressions: (IReadOnlyList<ExpressionItem>)group.ToArray()))
            .ToArray();

        var aap = AapProtocol.From(expressionPlan.Items);
        if (settings.AvoidEyeBlinkConflicts && aap.ControlsEyeBlink
            || settings.AvoidLipSyncConflicts && aap.ControlsLipSync)
        {
            using var _ = new Utils.ProfilingSampleScope(
                "FaceTune.Build.Animator.ReplaceExternalTrackingControls");
            ReplaceExternalTrackingControls(
                controllerContext,
                settings.AvoidEyeBlinkConflicts && aap.ControlsEyeBlink,
                settings.AvoidLipSyncConflicts && aap.ControlsLipSync);
        }

        var graph = new AnimatorGraph(analyzedWriteDefaults ?? true);
        using (new Utils.ProfilingSampleScope("FaceTune.Build.Animator.BuildInitial"))
        {
            var initialController = CreateMergeAnimatorController(
                controllerContext,
                units[0].Anchor,
                "Initial",
                InitialLayerPriority);
            BuildInitialLayer(
                initialController,
                graph,
                settings,
                avatarControlSettings,
                externalLipSyncBlendShapes,
                mmdPolicy.PlaybackWhen,
                mmdPolicy.DisableFxLayer);
        }

        var expressionBuilder = new ExpressionAnimatorBuilder(
            settings.AvatarContext,
            graph,
            avatarControlSettings,
            layerMmdPlaybackWhen,
            aap);
        using (new Utils.ProfilingSampleScope("FaceTune.Build.Animator.BuildUnits"))
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

        var eyeBlinkBuilder = new EyeBlinkAnimatorBuilder(
            settings.AvatarContext,
            graph,
            aap,
            avatarControlSettings.DisableEyeBlinkWhen);
        var lipSyncBuilder = new LipSyncAnimatorBuilder(
            settings.AvatarContext,
            graph,
            aap,
            avatarControlSettings.DisableLipSyncWhen);
        if (eyeBlinkBuilder.ShouldBuild || lipSyncBuilder.ShouldBuild)
        {
            using var _ = new Utils.ProfilingSampleScope(
                "FaceTune.Build.Animator.BuildTrackingControls");
            var controlPriority = units.Max(unit => unit.Priority);
            var controlAnchor = expressionPlan.Items
                .Last(item => item.Priority.Priority == controlPriority)
                .SourceTransform;
            var controlController = CreateMergeAnimatorController(
                controllerContext,
                controlAnchor,
                "Tracking Controls",
                controlPriority);
            eyeBlinkBuilder.Build(controlController, layerMmdPlaybackWhen, controlPriority);
            lipSyncBuilder.Build(controlController, layerMmdPlaybackWhen, controlPriority);
        }
    }

    private static void BuildInitialLayer(
        VirtualAnimatorController controller,
        AnimatorGraph graph,
        BuildSettings settings,
        AvatarControlSettings avatarControlSettings,
        ISet<string> externalLipSyncBlendShapes,
        DnfCondition? mmdPlaybackWhen,
        bool disableFxLayerDuringMmd)
    {
        AnimatorGraph.EnsureConditionParameters(controller, mmdPlaybackWhen);
        var blendShapes = settings.AvatarContext.FaceRenderer
            .GetBlendShapeWeights(settings.AvatarContext.FaceMesh)
            .Where(shape => !settings.IsBlendShapeExplicitlyExcluded(shape.Name)
                && !externalLipSyncBlendShapes.Contains(shape.Name))
            .ToArray();

        var origin = AnimatorGraph.DefaultStatePosition;
        var layer = graph.AddLayer(controller, "Initial", InitialLayerPriority);
        var defaultState = graph.AddState(layer, "Default", origin);
        layer.StateMachine!.DefaultState = defaultState;
        SetInitialClip(defaultState, "Default", blendShapes, settings.AvatarContext.BodyPath);
        graph.SetExitTransitions(
            defaultState,
            mmdPlaybackWhen ?? DnfCondition.Never,
            0f);

        if (mmdPlaybackWhen is not { IsNever: false }) return;

        var mmdState = graph.AddState(
            layer,
            "MMD Playback",
            origin + new Vector3(0, AnimatorGraph.PositionYStep * 2, 0));
        var mmdBlendShapes = blendShapes
            .Where(shape => !avatarControlSettings.MmdPlayback
                .BlendShapeNames.Contains(shape.Name));
        SetInitialClip(
            mmdState,
            "MMD Playback",
            mmdBlendShapes,
            settings.AvatarContext.BodyPath);
        graph.AddEntryTransition(layer, mmdState, mmdPlaybackWhen);
        graph.SetExitTransitions(mmdState, mmdPlaybackWhen.Complement(), 0f);

        if (disableFxLayerDuringMmd)
        {
            SetFxPlayableWeight(defaultState, 1f);
            SetFxPlayableWeight(mmdState, 0f);
        }
    }

    private static void SetInitialClip(
        VirtualState state,
        string name,
        IEnumerable<BlendShapeWeight> blendShapes,
        string bodyPath)
    {
        state.SetNewClip(name).AddBlendShapeAnimations(
            bodyPath,
            blendShapes.ToBlendShapeAnimations());
    }

    private static void SetFxPlayableWeight(VirtualState state, float weight)
    {
        var control = state.EnsureBehavior<VRCPlayableLayerControl>();
        control.layer = VRCPlayableLayerControl.BlendableLayer.FX;
        control.goalWeight = weight;
        control.blendDuration = 0f;
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
        AnimatorGraph.EnsureAlwaysParameter(controller);
        controllerContext.Controllers[merge] = controller;
        return controller;
    }

    private static IReadOnlyDictionary<Transform, int> FindExternalPartitions(
        ExpressionPlan expressionPlan,
        AvatarContext avatarContext,
        ISet<Transform> unitBoundaryTransforms)
    {
        var expressionTransforms = expressionPlan.Items
            .Select(item => item.SourceTransform)
            .ToHashSet();
        var partitions = new Dictionary<Transform, int>();
        var partition = 0;
        var hasExpressionAbove = false;
        var hasBoundarySinceLastExpression = false;

        foreach (var transform in avatarContext.Root.GetComponentsInChildren<Transform>(true))
        {
            if (expressionTransforms.Contains(transform))
            {
                if (hasExpressionAbove && hasBoundarySinceLastExpression) partition++;
                partitions[transform] = partition;
                hasExpressionAbove = true;
                hasBoundarySinceLastExpression = false;
                continue;
            }

            if (hasExpressionAbove && !hasBoundarySinceLastExpression
                && unitBoundaryTransforms.Contains(transform))
            {
                hasBoundarySinceLastExpression = true;
            }
        }

        return partitions;
    }

    private static ISet<Transform> FindUnitBoundaryTransforms(
        BuildSettings settings,
        ExpressionPlan expressionPlan,
        VirtualControllerContext controllerContext)
    {
        var managedBindings = CollectManagedBindings(settings, expressionPlan).ToHashSet();
        if (managedBindings.Count == 0) return new HashSet<Transform>();

        return settings.AvatarContext.Root.GetComponentsInChildren<Transform>(true)
            .Where(transform => transform.TryGetComponent<ModularAvatarMergeAnimator>(out var merge)
                && merge.layerType == VRCAvatarDescriptor.AnimLayerType.FX
                && controllerContext.Controllers.TryGetValue(merge, out var controller)
                && CollectBindings(controller).Any(managedBindings.Contains))
            .ToHashSet();
    }

    private static IEnumerable<EditorCurveBinding> CollectManagedBindings(
        BuildSettings settings,
        ExpressionPlan expressionPlan)
    {
        foreach (var name in settings.AvatarContext.FaceMesh.GetBlendShapeNames()
                     .Where(name => !settings.IsBlendShapeExplicitlyExcluded(name)))
        {
            yield return EditorCurveBinding.FloatCurve(
                settings.AvatarContext.BodyPath,
                typeof(SkinnedMeshRenderer),
                "blendShape." + name);
        }

        foreach (var item in expressionPlan.Items)
        {
            foreach (var (binding, _) in item.NonFacialAnimations.FloatCurves)
                yield return binding;
            foreach (var (binding, _) in item.NonFacialAnimations.ObjectCurves)
                yield return binding;
        }
    }

    private static IEnumerable<EditorCurveBinding> CollectBindings(
        VirtualAnimatorController controller)
        => controller.Layers
            .Where(layer => layer.StateMachine != null)
            .SelectMany(layer => layer.StateMachine!.AllStates())
            .SelectMany(state => CollectBindings(state.Motion))
            .Distinct();

    private static IEnumerable<EditorCurveBinding> CollectBindings(VirtualMotion? motion)
    {
        return motion switch
        {
            VirtualClip clip => clip.GetFloatCurveBindings().Concat(clip.GetObjectCurveBindings()),
            VirtualBlendTree tree => tree.Children
                .Where(child => child.Motion != null)
                .SelectMany(child => CollectBindings(child.Motion)),
            _ => Array.Empty<EditorCurveBinding>()
        };
    }

    private static void ReplaceExternalTrackingControls(
        VirtualControllerContext controllerContext,
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
                    ReplaceExternalTrackingControls(state, replaceEyeBlink, replaceLipSync);
            }
        }
    }

    private static void ReplaceExternalTrackingControls(
        VirtualState state,
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
            writes.AddRange(AapProtocol.BuildTrackingReplacementWrites(eyeTracking, mouthTracking));
            if (replaceEyeBlink)
                control.trackingEyes = VRCAnimatorTrackingControl.TrackingType.NoChange;
            if (replaceLipSync)
                control.trackingMouth = VRCAnimatorTrackingControl.TrackingType.NoChange;
        }

        if (writes.Count > 0) AddAapWritesToClip(state, writes);

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
            {
                var curve = new AnimationCurve(new Keyframe(0f, write.Value));
                clip.SetFloatCurve("", typeof(UnityEngine.Animator), write.ParameterName, curve);
            }
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

    private static (
        DnfCondition? PlaybackWhen,
        bool DisableLayers,
        bool DisableFxLayer) ResolveMmdAnimatorPolicy(
        AvatarControlSettings avatarControlSettings,
        bool? analyzedWriteDefaults)
    {
        var playback = avatarControlSettings.MmdPlayback;
        if (!playback.Enabled) return (null, false, false);

        var playbackWhen = playback.DisableWhen ?? DnfCondition.Always;
        if (playback.DisableWhen == null) return (playbackWhen, false, false);

        var mode = playback.DisableMode == MMDSupportSettings.Mode.Auto
            ? analyzedWriteDefaults == true
                ? MMDSupportSettings.Mode.DisableLayers
                : MMDSupportSettings.Mode.DisableFXlayer
            : playback.DisableMode;
        return mode switch
        {
            MMDSupportSettings.Mode.DisableLayers => (playbackWhen, true, false),
            MMDSupportSettings.Mode.DisableFXlayer => (playbackWhen, false, true),
            _ => throw new ArgumentOutOfRangeException(
                nameof(playback.DisableMode),
                playback.DisableMode,
                null)
        };
    }
}
