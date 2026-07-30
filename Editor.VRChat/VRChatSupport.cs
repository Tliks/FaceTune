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
    private const string GestureLeftWeightParameter = "GestureLeftWeight";
    private const string GestureRightWeightParameter = "GestureRightWeight";
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

    public string ResolveGestureParameter(Hand hand)
        => hand == Hand.Left ? GestureLeftParameter : GestureRightParameter;

    public string ResolveGestureWeightParameter(Hand hand)
        => hand == Hand.Left ? GestureLeftWeightParameter : GestureRightWeightParameter;

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

        return FindRenderer("Body", StringComparison.Ordinal)
               ?? FindRenderer("body", StringComparison.Ordinal)
               ?? FindRenderer("Face", StringComparison.OrdinalIgnoreCase);
    }

    private SkinnedMeshRenderer? FindRenderer(string name, StringComparison comparison)
    {
        var avatarRoot = _descriptor.transform;
        for (var i = 0; i < avatarRoot.childCount; i++)
        {
            var child = avatarRoot.GetChild(i);
            if (!string.Equals(child.name, name, comparison)) continue;
            if (child.TryGetComponent<SkinnedMeshRenderer>(out var renderer)) return renderer;
        }

        return null;
    }

    public ParameterDomainRegistry CreateBuiltInParameterDomains()
    {
        return new ParameterDomainRegistry(
            new IntParameterDomain(0, 255),
            (PreviewModeParameter, new IntParameterDomain(0, 1)),
            (VisemeParameter, new IntParameterDomain(0, 14)),
            (GestureLeftParameter, new IntParameterDomain(0, 7)),
            (GestureRightParameter, new IntParameterDomain(0, 7)),
            (TrackingTypeParameter, new IntParameterDomain(0, 6)),
            (VRModeParameter, new IntParameterDomain(0, 1)),
            (AvatarVersionParameter, new IntParameterDomain(0, 3)),
            (VrcEmoteParameter, new IntParameterDomain(1, 16)));
    }

    public DnfCondition ResolveHandGestureCondition(
        HandGestureCondition condition,
        ParameterDomainRegistry parameterDomains)
    {
        var gesture = condition.HandGesture;
        return condition.Match switch
        {
            HandGestureMatch.LeftHand => HandRule(GestureLeftParameter, true, gesture, parameterDomains),
            HandGestureMatch.RightHand => HandRule(GestureRightParameter, true, gesture, parameterDomains),
            HandGestureMatch.BothHands => HandRule(GestureLeftParameter, true, gesture, parameterDomains)
                .And(HandRule(GestureRightParameter, true, gesture, parameterDomains)),
            HandGestureMatch.AtLeastOneHand => HandRule(GestureLeftParameter, true, gesture, parameterDomains)
                .Or(HandRule(GestureRightParameter, true, gesture, parameterDomains)),
            HandGestureMatch.ExactlyOneHand => HandRule(GestureLeftParameter, true, gesture, parameterDomains)
                .And(HandRule(GestureRightParameter, false, gesture, parameterDomains))
                .Or(HandRule(GestureLeftParameter, false, gesture, parameterDomains)
                    .And(HandRule(GestureRightParameter, true, gesture, parameterDomains))),
            HandGestureMatch.NeitherHand => HandRule(GestureLeftParameter, false, gesture, parameterDomains)
                .And(HandRule(GestureRightParameter, false, gesture, parameterDomains)),
            _ => throw new NotSupportedException(
                $"Hand gesture match {condition.Match} is not supported by VRChat")
        };
    }

    public DnfCondition ResolveParameterCondition(
        ParameterCondition condition,
        ParameterDomainRegistry parameterDomains)
    {
        return DnfCondition.Single(
            AnimatorConditionRule.FromParameterCondition(condition),
            parameterDomains);
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

    private static DnfCondition HandRule(
        string parameterName,
        bool equal,
        HandGesture handGesture,
        ParameterDomainRegistry parameterDomains)
    {
        return DnfCondition.Single(new AnimatorConditionRule(
            new AnimatorCondition
            {
                parameter = parameterName,
                mode = equal ? AnimatorConditionMode.Equals : AnimatorConditionMode.NotEqual,
                threshold = (int)handGesture
            },
            AnimatorControllerParameterType.Int), parameterDomains);
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
