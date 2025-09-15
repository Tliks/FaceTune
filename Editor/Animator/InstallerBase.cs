using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf.animator;
using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Animator;

internal class InstallerBase
{
    protected readonly VirtualAnimatorController _controller;
    protected readonly AvatarContext _avatarContext;
    protected readonly IMetabasePlatformSupport _platformSupport;

    protected readonly bool _useWriteDefaults;

    protected readonly VirtualClip _emptyClip;

    protected const int LayerPriority = 1; // FaceEmo: 0

    protected static readonly Vector3 EntryStatePosition = new Vector3(50, 120, 0);
    protected const float PositionXStep = 250;
    protected const float PositionYStep = 50;

    protected const string TrueParameterName = FaceTuneConstants.TrueParameterName;

    public InstallerBase(VirtualAnimatorController virtualController, AvatarContext avatarContext, bool useWriteDefaults)
    {
        _controller = virtualController;
        _avatarContext = avatarContext;
        _platformSupport = Platforms.MetabasePlatformSupport.GetSupport(_avatarContext.Root.transform);
        _useWriteDefaults = useWriteDefaults;

        _useWriteDefaults = useWriteDefaults;
        _emptyClip = AnimatorHelper.CreateCustomEmpty();

        _controller.EnsureBoolParameterExists(TrueParameterName, true);
    }

    protected VirtualLayer AddLayer(string layerName, int priority, bool addMMDLayerControl = true)
    {
        var layerPriority = new LayerPriority(priority);
        var layer = _controller.AddLayer(layerPriority, $"{FaceTuneConstants.Name}: {layerName}");
        if (addMMDLayerControl)
        {
            layer.StateMachine!.EnsureBehavior<ModularAvatarMMDLayerControl>().DisableInMMDMode = true;
        }
        return layer; 
    }

    protected VirtualState AddState(VirtualLayer layer, string stateName, Vector3? position = null)
    {
        var state = layer.StateMachine!.AddState(stateName, position: position);
        state.WriteDefaultValues = _useWriteDefaults;
        return state;
    }

    protected VirtualClip AddAnimationToState(VirtualState state, IEnumerable<GenericAnimation> animations)
    {
        var clip = state.GetOrCreateClip(state.Name);
        clip.AddAnimations(animations);
        return clip;
    }

    protected void AsPassThrough(VirtualState state)
    {
        // Transition Durationを用いて上のレイヤーとブレンドする際、WD OFFの場合は空のClip、WD ONの場合はNone
        if (_useWriteDefaults)
        {
            state.Motion = null;
        }
        else
        {
            state.Motion = _emptyClip;
        }
    }
}