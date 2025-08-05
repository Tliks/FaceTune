#if FT_VRCSDK3_AVATARS

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using Aoyon.FaceTune.Build;
using Aoyon.FaceTune.Animator;

namespace Aoyon.FaceTune.Platforms;

internal class VRChatSupport : IMetabasePlatformSupport
{
    [InitializeOnLoadMethod]
    static void Register()
    {
        MetabasePlatformSupport.Register(new VRChatSupport());
    }

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

    private AnimatorInstaller InitializeAnimatorInstaller(AnimatorServicesContext asc, SessionContext context)
    {
        var cc = asc.ControllerContext;
        var fx = cc.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
        var useWriteDefaults = AnimatorHelper.AnalyzeLayerWriteDefaults(fx) ?? true;
        return new AnimatorInstaller(fx, context, useWriteDefaults);
    }

    public void InstallPatternData(BuildPassContext buildPassContext, BuildContext buildContext, InstallerData installerData)
    {
        var installer = InitializeAnimatorInstaller(buildContext.Extension<AnimatorServicesContext>(), buildPassContext.SessionContext);
        installer.Execute(installerData);
    }

    public IEnumerable<string> GetTrackedBlendShape()
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
    
    public void SetEyeBlinkTrack(VirtualState state, bool isTracking)
    {
        var trackingControl = state.EnsureBehavior<VRCAnimatorTrackingControl>();
        trackingControl.trackingEyes = isTracking ? VRCAnimatorTrackingControl.TrackingType.Tracking : VRCAnimatorTrackingControl.TrackingType.Animation;
    }
    public void SetLipSyncTrack(VirtualState state, bool isTracking)
    {
        var trackingControl = state.EnsureBehavior<VRCAnimatorTrackingControl>();
        trackingControl.trackingMouth = isTracking ? VRCAnimatorTrackingControl.TrackingType.Tracking : VRCAnimatorTrackingControl.TrackingType.Animation;
    }
    public void StateAsRandrom(VirtualState state, string parameterName, float min, float max)
    {
        state.EnsureBehavior<VRCAvatarParameterDriver>().parameters.Add(new VRC_AvatarParameterDriver.Parameter()
        {
            type = VRC_AvatarParameterDriver.ChangeType.Random,
            name = parameterName,
            valueMin = min,
            valueMax = max,
        });
    }
    public AnimatorController? GetFXAnimatorController()
    {
        var descriptor = _root.GetComponent<VRCAvatarDescriptor>()!;
        foreach (var layer in descriptor.baseAnimationLayers)
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

    public (TrackingPermission eye, TrackingPermission mouth)? GetTrackingPermission(AnimatorState state)
    {
        var trackingControl = GetVRCAnimatorTrackingControl(state);
        if (trackingControl != null)
        {
            return GetTrackingPermission(trackingControl);
        }
        else
        {
            return null;
        }

        static VRC_AnimatorTrackingControl? GetVRCAnimatorTrackingControl(AnimatorState state)
        {
            if (state.behaviours == null) return null;
            foreach (var behaviour in state.behaviours)
            {
                if (behaviour is VRC_AnimatorTrackingControl trackingControl)
                {
                    return trackingControl;
                }
            }
            return null;
        }

        static (TrackingPermission eye, TrackingPermission mouth) GetTrackingPermission(VRC_AnimatorTrackingControl trackingControl)
        {
            var eye = trackingControl.trackingEyes switch
            {
                VRC_AnimatorTrackingControl.TrackingType.NoChange => TrackingPermission.Keep,
                VRC_AnimatorTrackingControl.TrackingType.Tracking => TrackingPermission.Allow,
                VRC_AnimatorTrackingControl.TrackingType.Animation => TrackingPermission.Disallow,
                _ => TrackingPermission.Keep
            };
            
            var mouth = trackingControl.trackingMouth switch
            {
                VRC_AnimatorTrackingControl.TrackingType.NoChange => TrackingPermission.Keep,
                VRC_AnimatorTrackingControl.TrackingType.Tracking => TrackingPermission.Allow,
                VRC_AnimatorTrackingControl.TrackingType.Animation => TrackingPermission.Disallow,
                _ => TrackingPermission.Keep
            };

            return (eye, mouth);
        }
    }
}

#endif