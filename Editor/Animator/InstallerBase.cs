using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf.animator;
using aoyon.facetune.platform;

namespace aoyon.facetune.animator;

internal class InstallerBase
{
    protected readonly VirtualAnimatorController _controller;
    protected readonly SessionContext _sessionContext;
    protected readonly IPlatformSupport _platformSupport;

    protected readonly bool _useWriteDefaults;

    private readonly Dictionary<int, VirtualClip> _defaultClips = new();

    protected readonly VirtualClip _emptyClip;

    protected const int LayerPriority = 1; // FaceEmo: 0

    protected static readonly Vector3 EntryStatePosition = new Vector3(50, 120, 0);
    protected const float PositionXStep = 250;
    protected const float PositionYStep = 50;

    protected const string TrueParameterName = $"{FaceTuneConsts.ParameterPrefix}/True";

    public InstallerBase(VirtualAnimatorController virtualController, SessionContext sessionContext, bool useWriteDefaults)
    {
        _controller = virtualController;
        _sessionContext = sessionContext;
        _platformSupport = platform.PlatformSupport.GetSupport(_sessionContext.Root.transform);
        _useWriteDefaults = useWriteDefaults;

        _useWriteDefaults = useWriteDefaults;
        _emptyClip = AnimatorHelper.CreateCustomEmpty();

        _controller.EnsureParameterExists(AnimatorControllerParameterType.Bool, TrueParameterName).defaultBool = true;
    }

    protected VirtualLayer AddLayer(string layerName, int priority)
    {
        var layerPriority = new LayerPriority(priority);
        var layer = _controller.AddLayer(layerPriority, $"{FaceTuneConsts.ShortName}: {layerName}");
        layer.StateMachine!.EnsureBehavior<ModularAvatarMMDLayerControl>().DisableInMMDMode = true;
        return layer; 
    }

    protected VirtualState AddState(VirtualLayer layer, string stateName, Vector3? position = null)
    {
        var state = layer.StateMachine!.AddState(stateName, position: position);
        state.WriteDefaultValues = _useWriteDefaults;
        return state;
    }

    protected void AddAnimationToState(VirtualState state, IEnumerable<GenericAnimation> animations)
    {
        var clip = state.GetOrCreateClip(state.Name);
        clip.SetAnimations(animations);
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
    
    protected VirtualClip GetOrCreateDefautLayerAndClip(int priority, string name)
    {
        if (_defaultClips.TryGetValue(priority, out var clip)) return clip;
        
        var layer = AddLayer(name, priority);
        var state = layer.StateMachine!.AddState("Default", position: EntryStatePosition);
        clip = state.GetOrCreateClip(state.Name);
        _defaultClips[priority] = clip;
        return clip;
    }

    protected VirtualClip GetRequiredDefaultClip(int priority)
    {
        if (!_defaultClips.ContainsKey(priority)) throw new InvalidOperationException($"Default clip for priority {priority} not found");
        return _defaultClips[priority];
    }

    public virtual void EditDefaultClip(VirtualClip clip) { }
}