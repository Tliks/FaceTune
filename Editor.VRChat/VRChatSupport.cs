using Aoyon.FaceTune.Build;
using Aoyon.FaceTune.Build.Animator;
using Aoyon.FaceTune.Platforms.VRChat;
using nadena.dev.ndmf;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace Aoyon.FaceTune.Platforms;

internal sealed class VRChatSupport : IMetabasePlatformSupport
{
    private const string GestureLeftParameter = "GestureLeft";
    private const string GestureRightParameter = "GestureRight";
    private const string VisemeParameter = "Viseme";
    private const string PreviewModeParameter = "PreviewMode";
    private const string TrackingTypeParameter = "TrackingType";
    private const string VRModeParameter = "VRMode";
    private const string AvatarVersionParameter = "AvatarVersion";
    private const string VrcEmoteParameter = "VRCEmote";

    private readonly VRCAvatarDescriptor _descriptor;

    public IPlatformBuildBackend BuildBackend => VRChatBuildBackend.Instance;

    [InitializeOnLoadMethod]
    private static void Register()
    {
        MetabasePlatformSupport.Register(
            WellKnownPlatforms.VRChatAvatar30,
            root => root.TryGetComponent<VRCAvatarDescriptor>(out var descriptor)
                ? new VRChatSupport(descriptor)
                : null);
    }

    private VRChatSupport(VRCAvatarDescriptor descriptor)
    {
        _descriptor = descriptor;
    }

    public SkinnedMeshRenderer? GetFaceRenderer()
    {
        if (_descriptor.lipSync == VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape
            && _descriptor.VisemeSkinnedMesh != null)
        {
            return _descriptor.VisemeSkinnedMesh;
        }

        if (_descriptor.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes
            && _descriptor.customEyeLookSettings.eyelidsSkinnedMesh != null)
        {
            return _descriptor.customEyeLookSettings.eyelidsSkinnedMesh;
        }

        var avatarRoot = _descriptor.transform;
        for (var i = 0; i < avatarRoot.childCount; i++)
        {
            var child = avatarRoot.GetChild(i);
            if (child.name == "Body" && child.TryGetComponent<SkinnedMeshRenderer>(out var renderer))
            {
                return renderer;
            }
        }

        return null;
    }

    public ParameterDomainRegistry CreateBuiltInParameterDomains()
    {
        var domains = new ParameterDomainRegistry();
        domains.SetDefaultIntDomain(new IntParameterDomain(0, 255));
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
            _ => throw new NotSupportedException(
                $"Hand gesture match {condition.Match} is not supported by VRChat")
        };
    }

    public DnfCondition ResolveParameterCondition(ParameterCondition condition)
    {
        return DnfCondition.Single(AnimatorConditionRule.FromParameterCondition(condition));
    }

    public IEnumerable<string> GetExternallyControlledBlendShapeNames()
    {
        var names = new HashSet<string>();
        names.UnionWith(GetLipSyncBlendShapes());
        names.UnionWith(GetBlinkBlendShapes());
        return names;
    }

    public AnimatorController? GetAnimatorController()
    {
        return _descriptor.baseAnimationLayers
            .Where(layer => layer.type == VRCAvatarDescriptor.AnimLayerType.FX)
            .Select(layer => layer.animatorController)
            .OfType<AnimatorController>()
            .FirstOrDefault();
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
            AnimatorControllerParameterType.Int));
    }

    private IEnumerable<string> GetBlinkBlendShapes()
    {
        var settings = _descriptor.customEyeLookSettings;
        var renderer = settings.eyelidsSkinnedMesh;
        if (settings.eyelidsBlendshapes == null || renderer == null || renderer.sharedMesh == null
            || settings.eyelidsBlendshapes.Length == 0)
        {
            return Array.Empty<string>();
        }

        var index = settings.eyelidsBlendshapes[0];
        return 0 <= index && index < renderer.sharedMesh.blendShapeCount
            ? new[] { renderer.sharedMesh.GetBlendShapeName(index) }
            : Array.Empty<string>();
    }

    private IEnumerable<string> GetLipSyncBlendShapes()
    {
        return _descriptor.VisemeSkinnedMesh != null && _descriptor.VisemeBlendShapes != null
            ? _descriptor.VisemeBlendShapes
            : Array.Empty<string>();
    }
}
