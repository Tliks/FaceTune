using Aoyon.FaceTune.Platforms;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune;

internal class AvatarContext
{
    public readonly GameObject Root;
    public readonly SkinnedMeshRenderer FaceRenderer;
    
    public readonly Mesh FaceMesh;
    public readonly string BodyPath;
    public readonly IReadOnlyBlendShapeSet ZeroBlendShapes;
    public readonly HashSet<string> TrackedBlendShapes;
    public readonly IReadOnlyBlendShapeSet SafeZeroBlendShapes;

    public AvatarContext(
        GameObject root,
        SkinnedMeshRenderer faceRenderer,
        Mesh faceMesh,
        string bodyPath,
        IReadOnlyBlendShapeSet zeroWeightBlendShapes,
        HashSet<string> trackedBlendShapes,
        IReadOnlyBlendShapeSet safeBlendShapes
    )
    {
        Root = root;
        FaceRenderer = faceRenderer;
        FaceMesh = faceMesh;
        BodyPath = bodyPath;
        ZeroBlendShapes = zeroWeightBlendShapes;
        TrackedBlendShapes = trackedBlendShapes;
        SafeZeroBlendShapes = safeBlendShapes;
    }

    public static bool TryGetFaceRenderer(GameObject root, [NotNullWhen(true)] out SkinnedMeshRenderer? faceRenderer, [NotNullWhen(true)] out string? bodyPath, IMetabasePlatformSupport? platformSupport = null, ComputeContext? context = null)
    {
        faceRenderer = null;
        bodyPath = null;

        context ??= ComputeContext.NullContext;

        using var _settingsComponents = ListPool<SettingsComponent>.Get(out var settingsComponents);
        context.GetComponents<SettingsComponent>(root.gameObject, settingsComponents);
        if (settingsComponents.Count > 1)
        {
            LocalizedLog.Warning("Log:warning:AvatarContextBuilder:MultipleSettingsComponent", null, settingsComponents);
        }
        if (settingsComponents.Count > 0)
        {
            var settingsComponent = settingsComponents[0];
            var faceObject = context.Observe(settingsComponent, c => c.OverrideFaceRenderer ? c.FaceObjectReference.Get(c) : null, (a, b) => a == b).DestroyedAsNull();
            faceRenderer = faceObject != null ? context.GetComponent<SkinnedMeshRenderer>(faceObject).DestroyedAsNull() : null;
        }

        faceRenderer ??= (platformSupport ??= MetabasePlatformSupport.GetSupport(root.transform)).GetFaceRenderer();

        if (faceRenderer == null) return false;

        bodyPath = RuntimeUtil.RelativePath(root, faceRenderer.gameObject)!;
        return true;
    }
}

internal static class AvatarContextBuilder
{
    public static bool TryBuild(GameObject target, [NotNullWhen(true)] out AvatarContext? avatarContext, out AvatarContextBuildResult result, ComputeContext? context = null)
    {
        avatarContext = null;

        context ??= ComputeContext.NullContext;

        var root = context.GetAvatarRoot(target);
        if (root == null)
        {
            result = AvatarContextBuildResult.NotFoundAvatarRoot;
            return false;
        }

        var platformSupport = Platforms.MetabasePlatformSupport.GetSupport(root.transform);

        if (!TryGetFaceRenderer(root, out var faceRenderer, out var bodyPath, platformSupport, context))
        {
            result = AvatarContextBuildResult.NotFoundFaceRenderer;
            return false;
        }

        var faceMesh = context.Observe(faceRenderer, r => r.sharedMesh, (a, b) => a == b);
        if (faceMesh == null)
        {
            result = AvatarContextBuildResult.NotFoundFaceMesh;
            return false;
        }

        var zeroBlendShapes = new BlendShapeWeightSet();
        faceRenderer.GetBlendShapesAndSetWeightToZero(zeroBlendShapes);

        var trackedBlendShapes = new HashSet<string>(platformSupport.GetTrackedBlendShape());

        var safeZeroBlendShapes = new BlendShapeWeightSet(zeroBlendShapes.Where(shape => !trackedBlendShapes.Contains(shape.Name)));

        avatarContext = new AvatarContext(root.gameObject, faceRenderer, faceMesh, bodyPath, zeroBlendShapes, trackedBlendShapes, safeZeroBlendShapes);
        result = AvatarContextBuildResult.Success;
        return true;
    }


    public enum AvatarContextBuildResult
    {
        Success,
        NotFoundAvatarRoot,
        NotFoundFaceRenderer,
        NotFoundFaceMesh,
    }
}

