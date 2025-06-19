#if FT_VRCSDK3_AVATARS

using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;
using com.aoyon.facetune.animator;
using nadena.dev.ndmf.animator;
using com.aoyon.facetune.pass;

namespace com.aoyon.facetune.platform;

internal class VRChatSuport : IPlatformSupport
{
    [InitializeOnLoadMethod]
    static void Register()
    {
        PlatformSupport.Register(new VRChatSuport());
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

    private AnimatorInstaller InitializeAnimatorInstaller(BuildContext buildContext, SessionContext context)
    {
        var asc = buildContext.Extension<AnimatorServicesContext>();
        var cc = asc.ControllerContext;
        var fx = cc.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
        var useWriteDefaults = AnimatorHelper.AnalyzeLayerWriteDefaults(fx) ?? true;
        return new AnimatorInstaller(context, cc, fx, useWriteDefaults);
    }

    public void DisableExistingControlAndInstallPatternData(FTPassContext passContext, bool overrideShapes, bool overrideProperties, PatternData patternData)
    {
        var sessionContext = passContext.SessionContext;
        if (sessionContext == null)
        {
            throw new Exception("SessionContext is not set");
        }
        var installer = InitializeAnimatorInstaller(passContext.BuildContext, sessionContext);
        installer.DisableExistingControlAndInstallPatternData(overrideShapes, overrideProperties, patternData);
    }

    public IEnumerable<string> GetTrackedBlendShape()
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

    public MenuItemType GetMenuItemType(ModularAvatarMenuItem menuItem)
    {
        switch (menuItem.Control.type)
        {
            case VRCExpressionsMenu.Control.ControlType.Toggle:
                return MenuItemType.Toggle;
            case VRCExpressionsMenu.Control.ControlType.Button:
                return MenuItemType.Button;
            case VRCExpressionsMenu.Control.ControlType.SubMenu:
                return MenuItemType.SubMenu;
            case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                return MenuItemType.TwoAxisPuppet;
            case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                return MenuItemType.FourAxisPuppet;
            case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                return MenuItemType.RadialPuppet;
            default:
                throw new Exception($"Unknown menu item type: {menuItem.Control.type}");
        }
    }
    public void SetMenuItemType(ModularAvatarMenuItem menuItem, MenuItemType type)
    {
        switch (type)
        {
            case MenuItemType.Toggle:
                menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                break;
            case MenuItemType.Button:
                menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.Button;
                break;
            case MenuItemType.SubMenu:
                menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
                break;
            case MenuItemType.TwoAxisPuppet:
                menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet;
                break;
            case MenuItemType.FourAxisPuppet:
                menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.FourAxisPuppet;
                break;
            case MenuItemType.RadialPuppet:
                menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.RadialPuppet;
                break;
            default:
                throw new Exception($"Unknown menu item type: {type}");
        }
    }
    public string GetParameterName(ModularAvatarMenuItem menuItem)
    {
        return menuItem.Control.parameter.name;
    }
    public string GetUniqueParameterName(ModularAvatarMenuItem menuItem, HashSet<string> usedNames)
    {
        var baseName = menuItem.gameObject.name.Replace(" ", "_");
        var parameterName = $"facetune/{baseName}/toggle";
        int index = 1;
        while (usedNames.Contains(parameterName))
        {
            parameterName = $"facetune/{baseName}_{index}/toggle";
            index++;
        }
        return parameterName;
    }
    public void SetParameterName(ModularAvatarMenuItem menuItem, string parameterName)
    {
        menuItem.Control.parameter.name = parameterName;
    }
    public float GetParameterValue(ModularAvatarMenuItem menuItem)
    {
        return menuItem.Control.value;
    }
    public void SetParameterValue(ModularAvatarMenuItem menuItem, float value)
    {
        menuItem.Control.value = value;
    }

    public string AssignUniqueParameterName(ModularAvatarMenuItem menuItem, HashSet<string> usedNames)
    {
        var parameterName = GetUniqueParameterName(menuItem, usedNames);
        usedNames.Add(parameterName);
        var control = new VRCExpressionsMenu.Control()
        {
            name = menuItem.gameObject.name,
            type = VRCExpressionsMenu.Control.ControlType.Toggle,
            parameter = new VRCExpressionsMenu.Control.Parameter()
            {
                name = parameterName,
            },
            subParameters = new VRCExpressionsMenu.Control.Parameter[] { },
            value = 0,
            labels = new VRCExpressionsMenu.Control.Label[] { },
            icon = null,
        };
        menuItem.Control = control;
        return parameterName;
    }
    public (string?, ParameterCondition?) MenuItemAsCondition(ModularAvatarMenuItem menuItem, HashSet<string> usedNames)
    {
        var existingParameterName = menuItem.Control.parameter.name;
        if (!string.IsNullOrWhiteSpace(existingParameterName)) 
        {
            return (existingParameterName, ParameterCondition.Bool(existingParameterName, true));
        }
        if (menuItem.Control.type == VRCExpressionsMenu.Control.ControlType.Toggle ||
            menuItem.Control.type == VRCExpressionsMenu.Control.ControlType.Button)
        {
            var parameterName = GetUniqueParameterName(menuItem, usedNames);
            menuItem.Control.parameter.name = parameterName;
            return (parameterName, ParameterCondition.Bool(parameterName, true));
        }
        return (null, null);
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
}

#endif