#if FT_VRCSDK3_AVATARS

using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using nadena.dev.ndmf;
using com.aoyon.facetune.animator;

namespace com.aoyon.facetune.platform;

internal class VRChatSuport : IPlatformSupport
{
    private Transform _root = null!;
    private VRCAvatarDescriptor _descriptor = null!;

    public bool IsTarget(Transform root)
    {
        return root.TryGetComponent<VRCAvatarDescriptor>(out _);
    }

    public void Initialize(Transform root)
    {
        _root = root;
        _descriptor = root.GetComponentNullable<VRCAvatarDescriptor>()!;
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
                    faceRenderer = child.GetComponentNullable<SkinnedMeshRenderer>();
                    if (faceRenderer != null) { break; }
                }
            }
        }

        return faceRenderer;
    }

    public void InstallPresets(BuildContext buildContext, SessionContext context, IEnumerable<Preset> presets)
    {
        var animatorInstaller = new AnimatorInstaller(buildContext, context);
        animatorInstaller.CreateDefaultLayer();
        animatorInstaller.InstallPreset(presets);
        animatorInstaller.SaveAsMergeAnimator();
    }

    public IEnumerable<string> GetDisallowedBlendShape(SessionContext context)
    {
        var disAllowed = new HashSet<string>();
        var lipSync = GetLipSyncBlendShape();
        disAllowed.UnionWith(lipSync);
        /*
        if (context) // any condition
        {
            var blink = GetBlinkBlendShape();
            disAllowed.UnionWith(blink);
        }
        */ 
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
}

#endif