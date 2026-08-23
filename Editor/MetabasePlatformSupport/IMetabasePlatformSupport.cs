using Aoyon.FaceTune.Build;

namespace Aoyon.FaceTune.Platforms;

internal interface IMetabasePlatformSupport
{
    IPlatformBuildBackend? BuildBackend => null;

    SkinnedMeshRenderer? GetFaceRenderer();

    IEnumerable<string> GetExternalEyeBlinkBlendShapeNames();

    IEnumerable<string> GetExternalLipSyncBlendShapeNames();

    MmdPlaybackSettings ResolveMmdPlaybackSettings(MMDSupportSettings? settings, DnfCondition? disableWhen)
    {
        return MmdPlaybackSettings.Disabled;
    }

    void PostProcessDefaultBlendShapes(
        BuildSettings settings,
        AvatarControlSettings avatarControlSettings,
        BlendShapeWeightSet blendShapes)
    {
    }

    IEnumerable<GameObject> GetMenuFolderObjects()
    {
        return Array.Empty<GameObject>();
    }

    ParameterDomainRegistry CreateBuiltInParameterDomains();

    DnfCondition? ResolveHandGestureCondition(
        HandGestureCondition condition,
        ParameterDomainRegistry parameterDomains);

    DnfCondition? ResolveParameterCondition(
        ParameterCondition condition,
        ParameterDomainRegistry parameterDomains);

    string? ResolveGestureParameter(Hand hand);

    string? ResolveGestureWeightParameter(Hand hand);
}
