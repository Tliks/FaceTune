using Aoyon.FaceTune.Build;

namespace Aoyon.FaceTune.Platforms;

internal interface IMetabasePlatformSupport
{
    IPlatformBuildBackend? BuildBackend => null;

    SkinnedMeshRenderer? GetFaceRenderer();

    IEnumerable<string> GetProhibitedBlendShapeNames(FaceTuneWriteKind writeKind);

    IReadOnlyList<BlendShapeWeightAnimation>? GetBuiltInEyeBlinkAnimations(
        SkinnedMeshRenderer faceRenderer)
        => null;

    VrcVisemeLipSyncShapes? GetBuiltInLipSyncShapes(SkinnedMeshRenderer faceRenderer)
        => null;

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
