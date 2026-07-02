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

    public AvatarContext(
        GameObject root,
        SkinnedMeshRenderer faceRenderer,
        Mesh faceMesh,
        string bodyPath)
    {
        Root = root;
        FaceRenderer = faceRenderer;
        FaceMesh = faceMesh;
        BodyPath = bodyPath;
    }

    public static bool TryGet(
        GameObject target,
        [NotNullWhen(true)] out AvatarContext? avatarContext,
        out BuildResult result,
        ComputeContext? context = null)
    {
        avatarContext = null;
        context ??= ComputeContext.NullContext;

        var root = context.GetAvatarRoot(target);
        if (root == null)
        {
            result = BuildResult.NotFoundAvatarRoot;
            return false;
        }

        var platformSupport = MetabasePlatformSupport.GetSupport(root.transform);

        SkinnedMeshRenderer? faceRenderer = null;
        using var _settingsComponents = ListPool<SettingsComponent>.Get(out var settingsComponents);
        context.GetComponents<SettingsComponent>(root, settingsComponents);
        if (settingsComponents.Count > 1)
        {
            LocalizedLog.Warning("Log:warning:AvatarContext:MultipleSettingsComponent", null, settingsComponents);
        }
        if (settingsComponents.Count > 0)
        {
            var settingsComponent = settingsComponents[0];
            var faceObject = context.Observe(
                settingsComponent,
                c => c.Settings.FaceObjectReference.Get(c),
                (a, b) => a == b).DestroyedAsNull();
            faceRenderer = faceObject != null
                ? context.GetComponent<SkinnedMeshRenderer>(faceObject).DestroyedAsNull()
                : null;
        }

        faceRenderer ??= platformSupport.GetFaceRenderer();
        if (faceRenderer == null)
        {
            result = BuildResult.NotFoundFaceRenderer;
            return false;
        }

        var faceMesh = context.Observe(faceRenderer, r => r.sharedMesh, (a, b) => a == b);
        if (faceMesh == null)
        {
            result = BuildResult.NotFoundFaceMesh;
            return false;
        }

        var bodyPath = RuntimeUtil.RelativePath(root, faceRenderer.gameObject)!;
        avatarContext = new AvatarContext(root.gameObject, faceRenderer, faceMesh, bodyPath);
        result = BuildResult.Success;
        return true;
    }

    public enum BuildResult
    {
        Success,
        NotFoundAvatarRoot,
        NotFoundFaceRenderer,
        NotFoundFaceMesh,
    }
}
