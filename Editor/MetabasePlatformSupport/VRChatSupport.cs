#if FaceTune_VRCSDK3_AVATARS

using UnityEditor.Animations;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using Aoyon.FaceTune.Build;
using Aoyon.FaceTune.Build.Animator;

namespace Aoyon.FaceTune.Platforms;

internal class VRChatSupport : IMetabasePlatformSupport
{
    [InitializeOnLoadMethod]
    static void Register()
    {
        MetabasePlatformSupport.Register(new VRChatSupport());
    }

    private const string GestureLeftParameter = "GestureLeft";
    private const string GestureRightParameter = "GestureRight";
    private const string VisemeParameter = "Viseme";
    private const string PreviewModeParameter = "PreviewMode";
    private const string TrackingTypeParameter = "TrackingType";
    private const string VRModeParameter = "VRMode";
    private const string AvatarVersionParameter = "AvatarVersion";
    private const string VrcEmoteParameter = "VRCEmote";

    private Transform _root = null!;
    private VRCAvatarDescriptor _descriptor = null!;

    public bool IsTarget(Transform root)
    {
        return root.TryGetComponent<VRCAvatarDescriptor>(out _);
    }

    public void Initialize(Transform root)
    {
        _root = root;
        _descriptor = root.TryGetComponent<VRCAvatarDescriptor>(out var descriptor) ? descriptor : null!;
    }

    public SkinnedMeshRenderer? GetFaceRenderer()
    {
        SkinnedMeshRenderer? faceRenderer = null;
        // Get from lipSync
        if (_descriptor.lipSync == VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape &&
            _descriptor.VisemeSkinnedMesh != null)
        {
            faceRenderer = _descriptor.VisemeSkinnedMesh;
        }
        // Get from eyelids
        else if (_descriptor.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes &&
            _descriptor.customEyeLookSettings.eyelidsSkinnedMesh != null)
        {
            faceRenderer = _descriptor.customEyeLookSettings.eyelidsSkinnedMesh;
        }
        // Get from body object
        else
        {
            var avatarRoot = _descriptor.gameObject.transform;
            for (int i = 0; i < avatarRoot.childCount; i++)
            {
                var child = avatarRoot.GetChild(i);
                if (child != null && child.name == "Body")
                {
                    faceRenderer = child.TryGetComponent<SkinnedMeshRenderer>(out var renderer) ? renderer : null;
                    if (faceRenderer != null) { break; }
                }
            }
        }

        return faceRenderer;
    }

    public ParameterDomainRegistry CreateBuiltInParameterDomains()
    {
        var domains = new ParameterDomainRegistry();

        // VRChat custom int expression parameters are unsigned 8-bit values.
        domains.SetDefaultIntDomain(new IntParameterDomain(0, 255));

        // Built-in / aliased int parameters with narrower documented ranges.
        domains.SetIntDomainOverride(PreviewModeParameter, new IntParameterDomain(0, 1));
        domains.SetIntDomainOverride(VisemeParameter, new IntParameterDomain(0, 14));
        domains.SetIntDomainOverride(GestureLeftParameter, new IntParameterDomain(0, 7));
        domains.SetIntDomainOverride(GestureRightParameter, new IntParameterDomain(0, 7));
        domains.SetIntDomainOverride(TrackingTypeParameter, new IntParameterDomain(0, 6));
        domains.SetIntDomainOverride(VRModeParameter, new IntParameterDomain(0, 1));
        domains.SetIntDomainOverride(AvatarVersionParameter, new IntParameterDomain(0, 3));
        domains.SetIntDomainOverride(VrcEmoteParameter, new IntParameterDomain(1, 16));
        return domains;
    }

    public DnfCondition ResolveHandGestureCondition(HandGestureCondition condition)
    {
        var gesture = condition.HandGesture;
        return condition.Match switch
        {
            HandGestureMatch.LeftHand => HandRule(GestureLeftParameter, true, gesture),
            HandGestureMatch.RightHand => HandRule(GestureRightParameter, true, gesture),
            HandGestureMatch.BothHands => HandRule(GestureLeftParameter, true, gesture)
                .And(HandRule(GestureRightParameter, true, gesture)),
            HandGestureMatch.AtLeastOneHand => HandRule(GestureLeftParameter, true, gesture)
                .Or(HandRule(GestureRightParameter, true, gesture)),
            HandGestureMatch.ExactlyOneHand => HandRule(GestureLeftParameter, true, gesture)
                .And(HandRule(GestureRightParameter, false, gesture))
                .Or(HandRule(GestureLeftParameter, false, gesture)
                    .And(HandRule(GestureRightParameter, true, gesture))),
            HandGestureMatch.NeitherHand => HandRule(GestureLeftParameter, false, gesture)
                .And(HandRule(GestureRightParameter, false, gesture)),
            _ => throw new NotSupportedException($"Hand gesture match {condition.Match} is not supported by VRChat")
        };
    }

    private static DnfCondition HandRule(string parameterName, bool equal, HandGesture handGesture)
    {
        return DnfCondition.Single(new AnimatorConditionRule(
            new AnimatorCondition
            {
                parameter = parameterName,
                mode = equal ? AnimatorConditionMode.Equals : AnimatorConditionMode.NotEqual,
                threshold = (int)handGesture
            },
            AnimatorControllerParameterType.Int,
            new IntParameterDomain(0, 7)));
    }

    public DnfCondition ResolveParameterCondition(ParameterCondition condition, ParameterDomainRegistry parameterDomains)
    {
        return DnfCondition.Single(AnimatorConditionRule.FromParameterCondition(condition, parameterDomains));
    }

    public IEnumerable<string> GetExternallyControlledBlendShapeNames()
    {
        var disAllowed = new HashSet<string>();
        var lipSync = GetLipSyncBlendShape();
        disAllowed.UnionWith(lipSync);
        var blink = GetBlinkBlendShape(); // 安全側に倒す
        disAllowed.UnionWith(blink);
        return disAllowed;
    }

    private IEnumerable<string> GetBlinkBlendShape()
    {
        if (_descriptor != null &&
            _descriptor.customEyeLookSettings.eyelidsBlendshapes != null &&
            _descriptor.customEyeLookSettings.eyelidsSkinnedMesh != null &&
            _descriptor.customEyeLookSettings.eyelidsSkinnedMesh.sharedMesh != null)
        {
            var skinnedMesh = _descriptor.customEyeLookSettings.eyelidsSkinnedMesh;

            if (_descriptor.customEyeLookSettings.eyelidsBlendshapes.Length > 0)
            {
                var index = _descriptor.customEyeLookSettings.eyelidsBlendshapes[0];
                if (0 <= index && index < skinnedMesh.sharedMesh.blendShapeCount)
                {
                    var name = skinnedMesh.sharedMesh.GetBlendShapeName(index);
                    return new string[] { name };
                }
            }
        }
        return new string[] { };
    }

    private IEnumerable<string> GetLipSyncBlendShape()
    {
        var ret = new List<string>();
        if (_descriptor != null &&
            _descriptor.VisemeSkinnedMesh != null &&
            _descriptor.VisemeBlendShapes is string[])
        {
            foreach (var name in _descriptor.VisemeBlendShapes)
            {
                ret.Add(name);
            }
        }
        return ret;
    }
    
    public void InstallBuild(BuildContext buildContext, BuildSettings settings, ExpressionProgram expressionProgram)
    {
        if (expressionProgram.IsEmpty) return;

        var controllerContext = buildContext.Extension<VirtualControllerContext>();
        var fx = controllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
        var platformServices = new VRChatAnimatorPlatformServices();

        var analyzedWriteDefaults = AnimatorHelper.AnalyzeLayerWriteDefaults(fx);
        var (layerForceInactiveWhen, controllerDisableWhen) = ResolveMmdAnimatorPolicy(settings, analyzedWriteDefaults);
        
        var animatorPlan = AnimatorBuildPlanBuilder.Build(
            expressionProgram,
            settings,
            platformServices,
            controllerContext,
            layerForceInactiveWhen);

        var installer = new AnimatorInstaller(
            controllerContext,
            settings.AvatarContext,
            analyzedWriteDefaults ?? true,
            platformServices,
            animatorPlan);
        installer.Execute();

        // controllerDisableWhen is intentionally left for platform-specific controller assignment.
        // Todo
    }

    private static (DnfCondition? LayerForceInactiveWhen, DnfCondition? ControllerDisableWhen) ResolveMmdAnimatorPolicy(
        BuildSettings settings,
        bool? analyzedWriteDefaults)
    {
        var playback = settings.MmdPlayback;
        if (!playback.Enabled || string.IsNullOrWhiteSpace(playback.DisableParameterName)) return (null, null);

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
        return DnfCondition.Single(AnimatorConditionRule.FromParameterCondition(ParameterCondition.Bool(parameterName, value)));
    }

    private sealed class VRChatAnimatorPlatformServices : IAnimatorPlatformServices
    {
        public void SetEyeBlinkTracking(VirtualState state, bool isTracking)
        {
            var trackingControl = state.EnsureBehavior<VRCAnimatorTrackingControl>();
            trackingControl.trackingEyes = isTracking ? VRCAnimatorTrackingControl.TrackingType.Tracking : VRCAnimatorTrackingControl.TrackingType.Animation;
        }

        public void SetLipSyncTracking(VirtualState state, bool isTracking)
        {
            var trackingControl = state.EnsureBehavior<VRCAnimatorTrackingControl>();
            trackingControl.trackingMouth = isTracking ? VRCAnimatorTrackingControl.TrackingType.Tracking : VRCAnimatorTrackingControl.TrackingType.Animation;
        }

        public void AddRandomDriver(VirtualState state, string parameterName, float min, float max)
        {
            state.EnsureBehavior<VRCAvatarParameterDriver>().parameters.Add(new VRC_AvatarParameterDriver.Parameter()
            {
                type = VRC_AvatarParameterDriver.ChangeType.Random,
                name = parameterName,
                valueMin = min,
                valueMax = max,
            });
        }

        public DiscreteFloatParameterRange AapFloatRange => new(-1f, 1f, 255);

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

        private static bool OverlapsManagedBlendShapes(VirtualAnimatorController controller, ISet<string> managedBlendShapeNames)
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
                .Where(binding => binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape."))
                .Select(binding => binding.propertyName["blendShape.".Length..]);
        }

        public VirtualAnimatorController CreateController(VirtualControllerContext controllerContext, Transform anchor, string name, int priority)
        {
            var merge = anchor.gameObject.AddComponent<ModularAvatarMergeAnimator>();

            merge.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            merge.deleteAttachedAnimator = false;
            merge.pathMode = MergeAnimatorPathMode.Absolute;
            merge.matchAvatarWriteDefaults = false;
            merge.layerPriority = priority;
            merge.mergeAnimatorMode = MergeAnimatorMode.Append;

            var controller = VirtualAnimatorController.Create(controllerContext.CloneContext, $"{FaceTuneConstants.Name}: {name}");
            controllerContext.Controllers[merge] = controller;
            return controller;
        }
    }

    public AnimatorController? GetAnimatorController()
    {
        foreach (var layer in _descriptor.baseAnimationLayers)
        {
            if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX
                && layer.animatorController != null
                && layer.animatorController is AnimatorController ac)
            {
                return ac;
            }
        }    
        return null;
    }
}

#endif