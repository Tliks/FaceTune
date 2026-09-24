using Aoyon.FaceTune.Build;
using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal sealed class VRChatInitialLayerBuilder
{
    private static readonly Vector3 DefaultStatePosition = new(300, 0, 0);
    public const int LayerPriority = -1;

    private readonly BuildSettings _settings;
    private readonly ResolvedNonFacialAnimationSet _nonFacialDefaults;
    private readonly IReadOnlyList<BlendShapeWeight> _blendShapes;
    private readonly AnimatorGraph _graph;

    public VRChatInitialLayerBuilder(
        BuildSettings settings,
        ExpressionPlan expressionPlan,
        IReadOnlyList<BlendShapeWeight> blendShapes,
        AnimatorGraph graph)
    {
        _settings = settings;
        _blendShapes = blendShapes;
        _graph = graph;
        _nonFacialDefaults = AnimatorHelper.GetDefaultValueAnimations(
            settings.AvatarContext.Root,
            expressionPlan.Items
                .SelectMany(item => item.NonFacialAnimations.FloatCurves
                    .Select(entry => entry.Key)
                    .Concat(item.NonFacialAnimations.ObjectCurves.Select(entry => entry.Key))));
    }

    public void Build(
        VirtualAnimatorController controller,
        MmdSupport mmdSupport,
        AfkSupport afkSupport,
        AapProtocol aap)
    {
        var mmdWhen = mmdSupport.PlaybackWhen.Except(afkSupport.PlaybackWhen);
        AnimatorGraph.EnsureConditionParameters(controller, mmdWhen);
        aap.EnsureExpressionInactiveParameter(controller);

        var origin = DefaultStatePosition;
        var bodyPath = _settings.AvatarContext.BodyPath;
        var layer = _graph.AddLayer(controller, "Initial", LayerPriority);
        var defaultState = _graph.AddState(layer, "Default", origin);
        layer.StateMachine!.DefaultState = defaultState;

        var clip = defaultState.SetNewClip("Default");
        foreach (var (binding, curve) in _nonFacialDefaults.FloatCurves)
            clip.SetFloatCurve(binding, curve);
        foreach (var (binding, curve) in _nonFacialDefaults.ObjectCurves)
            clip.SetObjectCurve(binding, curve);
        clip.AddBlendShapeAnimations(bodyPath, _blendShapes.ToBlendShapeAnimations());

        mmdSupport.AddInitialMmdState(
            _graph,
            layer,
            defaultState,
            mmdWhen,
            _blendShapes,
            origin + new Vector3(0, AnimatorGraph.PositionYStep * 2, 0),
            bodyPath);

        afkSupport.AddInitialState(
            controller,
            _graph,
            layer,
            defaultState,
            _blendShapes,
            bodyPath,
            origin + new Vector3(0, AnimatorGraph.PositionYStep * 4, 0));
    }
}
