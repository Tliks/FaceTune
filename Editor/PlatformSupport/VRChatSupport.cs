#if FT_VRCSDK3_AVATARS

using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using nadena.dev.ndmf;
using com.aoyon.facetune.animator;

namespace com.aoyon.facetune.platform;

internal class VRChatSuport : IPlatformSupport
{     
    public bool IsTarget(Transform root)
    {
        return root.TryGetComponent<VRCAvatarDescriptor>(out _);
    }

    public SkinnedMeshRenderer? GetFaceRenderer(Transform root)
    {
        if (!IsTarget(root)) throw new InvalidOperationException();

        var descriptor = root.GetComponentNullable<VRCAvatarDescriptor>()!;

        SkinnedMeshRenderer? faceRenderer = null;
        // Get from lipSync
        if (descriptor.lipSync == VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape &&
            descriptor.VisemeSkinnedMesh != null)
        {
            faceRenderer = descriptor.VisemeSkinnedMesh;
        }
        // Get from eyelids
        else if (descriptor.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes &&
            descriptor.customEyeLookSettings.eyelidsSkinnedMesh != null)
        {
            faceRenderer = descriptor.customEyeLookSettings.eyelidsSkinnedMesh;
        }
        // Get from body object
        else
        {
            var avatarRoot = descriptor.gameObject.transform;
            for (int i = 0; i < avatarRoot.childCount; i++)
            {
                var child = avatarRoot.GetChild(i);
                if (child != null && child.name == "Body")
                {
                    faceRenderer = child.GetComponentNullable<SkinnedMeshRenderer>();
                    if (faceRenderer != null) { break; }
                }
            }
        }

        return faceRenderer;
    }

    public void InstallPresets(BuildContext buildContext, SessionContext context, List<Preset> presets)
    {
        var animatorInstaller = new AnimatorInstaller(buildContext, context);
        animatorInstaller.CreateDefaultLayer();
        animatorInstaller.InstallPreset(presets);
        animatorInstaller.SaveAsMergeAnimator();
    }
}

#endif