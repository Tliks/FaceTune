using Aoyon.FaceTune.Build;
using Aoyon.FaceTune.Platforms.VRChat;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;
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

    internal VRChatSupport(VRCAvatarDescriptor descriptor)
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

        return _descriptor.transform.FindDirectChildComponent<SkinnedMeshRenderer>("Body", StringComparison.Ordinal)
               ?? _descriptor.transform.FindDirectChildComponent<SkinnedMeshRenderer>("body", StringComparison.Ordinal)
               ?? _descriptor.transform.FindDirectChildComponent<SkinnedMeshRenderer>("Face", StringComparison.OrdinalIgnoreCase);
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
        var left = HandRule(GestureLeftParameter, true, condition.Gesture, parameterDomains);
        var right = HandRule(GestureRightParameter, true, condition.Gesture, parameterDomains);
        var matches = condition.Hand switch
        {
            HandGestureHand.Left => left,
            HandGestureHand.Right => right,
            HandGestureHand.Any => left.Or(right),
            HandGestureHand.Both => left.And(right),
            _ => throw new ArgumentOutOfRangeException(nameof(condition.Hand), condition.Hand, null)
        };
        return condition.Matches ? matches : matches.Complement();
    }

    public DnfCondition ResolveParameterCondition(
        ParameterCondition condition,
        ParameterDomainRegistry parameterDomains)
    {
        return DnfCondition.Single(
            AnimatorConditionRule.FromParameterCondition(condition),
            parameterDomains);
    }

    public IEnumerable<string> GetExternalEyeBlinkBlendShapeNames()
        => GetBlinkBlendShapes();

    public IEnumerable<string> GetExternalLipSyncBlendShapeNames()
        => GetLipSyncBlendShapes();

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
                threshold = ToPlatformGestureValue(handGesture)
            },
            AnimatorControllerParameterType.Int), parameterDomains);
    }

    private static int ToPlatformGestureValue(HandGesture gesture)
        => VRChatGestureMap.ToPlatformValue(gesture);

    private IEnumerable<string> GetBlinkBlendShapes()
    {
        var settings = _descriptor.customEyeLookSettings;
        var renderer = settings.eyelidsSkinnedMesh;
        if (settings.eyelidType != VRCAvatarDescriptor.EyelidType.Blendshapes
            || settings.eyelidsBlendshapes == null || renderer == null || renderer != GetFaceRenderer()
            || renderer.sharedMesh == null || settings.eyelidsBlendshapes.Length == 0)
        {
            return Array.Empty<string>();
        }

        return settings.eyelidsBlendshapes
            .Where(index => 0 <= index && index < renderer.sharedMesh.blendShapeCount)
            .Select(renderer.sharedMesh.GetBlendShapeName)
            .Distinct(StringComparer.Ordinal);
    }

    internal IEnumerable<string> GetLipSyncBlendShapes()
    {
        return _descriptor.lipSync == VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape
               && _descriptor.VisemeSkinnedMesh != null
               && _descriptor.VisemeSkinnedMesh == GetFaceRenderer()
               && _descriptor.VisemeBlendShapes != null
            ? _descriptor.VisemeBlendShapes.Where(name => !string.IsNullOrWhiteSpace(name))
            : Array.Empty<string>();
    }

    public IEnumerable<GameObject> GetMenuFolderObjects()
    {
        foreach (var group in _descriptor.GetComponentsInChildren<ModularAvatarMenuGroup>(true))
        {
            yield return group.targetObject != null ? group.targetObject : group.gameObject;
        }

        foreach (var item in _descriptor.GetComponentsInChildren<ModularAvatarMenuItem>(true))
        {
            if (item.PortableControl.Type != PortableControlType.SubMenu
                || item.MenuSource != SubmenuSource.Children) continue;
            yield return item.menuSource_otherObjectChildren != null
                ? item.menuSource_otherObjectChildren
                : item.gameObject;
        }
    }

    public void PostProcessDefaultBlendShapes(
        BuildSettings settings,
        AvatarControlSettings avatarControlSettings,
        BlendShapeWeightSet blendShapes)
        => MmdSupport.PostProcessDefaultBlendShapes(
            settings,
            avatarControlSettings,
            blendShapes);

    public MmdPlaybackSettings ResolveMmdPlaybackSettings(
        MMDSupportSettings? settings,
        DnfCondition? disableWhen)
        => MmdSupport.ResolveMmdPlaybackSettings(settings, disableWhen);

}

internal static class VRChatGestureMap
{
    private static readonly HandGesture[] Values =
    {
        HandGesture.Neutral,
        HandGesture.Fist,
        HandGesture.HandOpen,
        HandGesture.FingerPoint,
        HandGesture.Victory,
        HandGesture.RockNRoll,
        HandGesture.HandGun,
        HandGesture.ThumbsUp
    };

    public static int ToPlatformValue(HandGesture gesture)
    {
        var value = Array.IndexOf(Values, gesture);
        return value >= 0
            ? value
            : throw new NotSupportedException($"Hand gesture '{gesture}' is not supported by VRChat.");
    }

    public static HandGesture? FromPlatformValue(int value)
        => 0 <= value && value < Values.Length ? Values[value] : null;
}
