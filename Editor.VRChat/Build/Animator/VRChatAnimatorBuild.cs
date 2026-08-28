using nadena.dev.modular_avatar.core;
using Aoyon.FaceTune.Build;
using Aoyon.FaceTune.Platforms;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static class VRChatAnimatorBuilder
{
    private const int InitialLayerPriority = -1;
    private const int TrackingControlLayerPriority = int.MaxValue;

    public static void Build(
        BuildContext buildContext,
        BuildSettings settings,
        AvatarControlSettings avatarControlSettings,
        ExpressionPlan expressionPlan)
    {
        var controlsTracking = avatarControlSettings.DisableEyeBlinkWhen != null
            || avatarControlSettings.DisableLipSyncWhen != null;
        if (expressionPlan.IsEmpty && !controlsTracking) return;

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

        var nonFacialDefaults = AnimatorHelper.GetDefaultValueAnimations(
            settings.AvatarContext.Root,
            expressionPlan.Items
                .SelectMany(item => item.NonFacialAnimations.FloatCurves
                    .Select(entry => entry.Key)
                    .Concat(item.NonFacialAnimations.ObjectCurves.Select(entry => entry.Key))));

        var aap = AapProtocol.From(expressionPlan.Items, avatarControlSettings);
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
        var mmdSupport = new MmdSupport(
            settings.AvatarContext.Root,
            graph,
            avatarControlSettings.MmdPlayback,
            MetabasePlatformSupport.GetForBuild(buildContext),
            settings.ParameterDomains,
            analyzedWriteDefaults);
        if (units.Length > 0)
        {
            using var _ = new Utils.ProfilingSampleScope(
                "FaceTune.Build.Animator.BuildInitial");
            var initialController = CreateMergeAnimatorController(
                controllerContext,
                units[0].Anchor,
                "Initial",
                InitialLayerPriority);
            BuildInitialLayer(
                initialController,
                graph,
                settings,
                externalLipSyncBlendShapes,
                nonFacialDefaults,
                mmdSupport);
        }

        var expressionBuilder = new ExpressionAnimatorBuilder(
            settings,
            graph,
            avatarControlSettings,
            mmdSupport,
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
            mmdSupport,
            aap,
            avatarControlSettings.DisableEyeBlinkWhen);
        var lipSyncBuilder = new LipSyncAnimatorBuilder(
            settings.AvatarContext,
            graph,
            mmdSupport,
            aap,
            avatarControlSettings.DisableLipSyncWhen);
        if (eyeBlinkBuilder.ShouldBuild || lipSyncBuilder.ShouldBuild)
        {
            using var _ = new Utils.ProfilingSampleScope(
                "FaceTune.Build.Animator.BuildTrackingControls");
            var controlAnchor = new GameObject($"{FaceTuneConstants.Name} Tracking Controls");
            controlAnchor.transform.SetParent(buildContext.AvatarRootTransform, false);
            var controlController = CreateMergeAnimatorController(
                controllerContext,
                controlAnchor.transform,
                "Tracking Controls",
                TrackingControlLayerPriority);
            eyeBlinkBuilder.Build(controlController, TrackingControlLayerPriority);
            lipSyncBuilder.Build(controlController, TrackingControlLayerPriority);
        }
    }

    private static void BuildInitialLayer(
        VirtualAnimatorController controller,
        AnimatorGraph graph,
        BuildSettings settings,
        ISet<string> externalLipSyncBlendShapes,
        ResolvedNonFacialAnimationSet nonFacialDefaults,
        MmdSupport mmdSupport)
    {
        AnimatorGraph.EnsureConditionParameters(controller, mmdSupport.PlaybackWhen);
        var blendShapes = settings.AvatarContext.FaceRenderer
            .GetBlendShapeWeights(settings.AvatarContext.FaceMesh)
            .Where(shape => !settings.IsBlendShapeExplicitlyExcluded(shape.Name)
                && !externalLipSyncBlendShapes.Contains(shape.Name))
            .ToArray();

        var origin = AnimatorGraph.DefaultStatePosition;
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
            blendShapes,
            origin + new Vector3(0, AnimatorGraph.PositionYStep * 2, 0),
            settings.AvatarContext.BodyPath);
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
                FaceTuneConstants.BlendShapePropertyPrefix + name);
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

}
