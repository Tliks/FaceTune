using Aoyon.FaceTune.Platforms;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms.VRChat;

/// <summary>FaceTuneのtracking設定から単一のglobal LipSync layerを構築する。</summary>
internal sealed class LipSyncAnimatorBuilder
{
    private static readonly Vector3 LayoutOrigin = new(300, 0, 0);
    private const float VisemeTransitionDurationSeconds = 0.05f;

    private readonly AvatarContext _avatarContext;
    private readonly AnimatorGraph _graph;
    private readonly VRChatTrackingPlan _plan;
    private readonly AapProtocol _aap;

    public LipSyncAnimatorBuilder(
        AvatarContext avatarContext,
        AnimatorGraph graph,
        VRChatTrackingPlan plan,
        AapProtocol aap)
    {
        _avatarContext = avatarContext;
        _graph = graph;
        _plan = plan;
        _aap = aap;
    }

    public void Build(VirtualAnimatorController controller, int layerPriority)
    {
        var generated = _plan.GeneratedLipSyncSettings
            .Select((settings, index) => (
                Settings: settings,
                Mode: VRChatTrackingPlan.FirstCustomMode + index))
            .ToImmutableList();
        var custom = generated
            .Where(entry => entry.Settings.Mode == LipSyncSettings.Kind.Custom)
            .ToImmutableList();
        var disabledWhen = _aap.LipSyncModeIs(VRChatTrackingPlan.DisabledMode);
        var builtInWhen = DnfCondition.Any(
            generated
                .Where(entry => entry.Settings.Mode == LipSyncSettings.Kind.BuiltIn)
                .Select(entry => _aap.LipSyncModeIs(entry.Mode))
                .Prepend(_aap.LipSyncModeIs(VRChatTrackingPlan.BuiltInMode)));
        EnsureParameters(controller, generated, disabledWhen, builtInWhen);

        var layer = _graph.AddLayer(controller, "Lip Sync", layerPriority);
        var root = layer.StateMachine!;
        var xStep = AnimatorGraph.PositionXStep;
        var yStep = AnimatorGraph.PositionYStep;

        var evaluation = _graph.AddState(layer, "Mode Evaluation", LayoutOrigin);
        _graph.AsPassThrough(evaluation);
        SetLipSyncTracking(evaluation, false);

        var initial = _graph.AddInitialDelayState(
            layer,
            LayoutOrigin + new Vector3(0, yStep * 2, 0));
        SetLipSyncTracking(initial, false);
        _graph.AddExitTimeTransition(initial, evaluation);

        var disabled = _graph.AddState(
            layer,
            "Disabled",
            LayoutOrigin + new Vector3(xStep, -yStep, 0));
        _graph.AsPassThrough(disabled);
        SetLipSyncTracking(disabled, false);
        _graph.AddStateTransition(evaluation, disabled, disabledWhen, 0f);
        _graph.AddStateTransition(disabled, evaluation, disabledWhen.Complement(), 0f);

        var builtIn = _graph.AddState(
            layer,
            "Built-in",
            LayoutOrigin + new Vector3(xStep, 0, 0));
        _graph.AsPassThrough(builtIn);
        SetLipSyncTracking(builtIn, true);
        _graph.AddStateTransition(evaluation, builtIn, builtInWhen, 0f);
        _graph.AddStateTransition(builtIn, evaluation, builtInWhen.Complement(), 0f);

        if (custom.Count > 0)
        {
            InstallCustomSettings(
                root,
                custom,
                evaluation,
                LayoutOrigin + new Vector3(xStep, yStep, 0));
        }
    }

    private void EnsureParameters(
        VirtualAnimatorController controller,
        ImmutableList<(LipSyncSettings Settings, int Mode)> generated,
        DnfCondition disabledWhen,
        DnfCondition builtInWhen)
    {
        _aap.EnsureLipSyncParameters(controller);
        if (generated.Any(entry => entry.Settings.Mode == LipSyncSettings.Kind.Custom))
        {
            controller.EnsureIntParameterExists(VRChatSupport.VisemeParameter);
            controller.EnsureFloatParameterExists(VRChatSupport.VoiceParameter);
        }
        AnimatorGraph.EnsureConditionParameters(
            controller,
            generated.Select(entry => _aap.LipSyncModeIs(entry.Mode))
                .Append(disabledWhen)
                .Append(builtInWhen)
                .ToArray());
    }

    private void InstallCustomSettings(
        VirtualStateMachine root,
        ImmutableList<(LipSyncSettings Settings, int Mode)> settingsEntries,
        VirtualState evaluation,
        Vector3 position)
    {
        var yStep = AnimatorGraph.PositionYStep;
        for (var index = 0; index < settingsEntries.Count; index++)
        {
            var entry = settingsEntries[index];
            var modeWhen = _aap.LipSyncModeIs(entry.Mode);
            var settingsMachine = _graph.AddStateMachine(
                root,
                $"Custom {index + 1}",
                position);
            _graph.AddStateTransition(evaluation, settingsMachine, modeWhen, 0f);
            InstallVisemes(settingsMachine, entry.Settings, modeWhen);
            _graph.AddStateMachineTransition(
                root,
                settingsMachine,
                settingsMachine,
                modeWhen);
            _graph.AddStateMachineTransition(
                root,
                settingsMachine,
                evaluation,
                modeWhen.Complement());

            position.y += yStep;
        }
    }

    private void InstallVisemes(
        VirtualStateMachine settingsMachine,
        LipSyncSettings settings,
        DnfCondition modeWhen)
    {
        var visemes = GetVisemes(settings.Shapes);
        var yStep = AnimatorGraph.PositionYStep;

        for (var index = 0; index < visemes.Length; index++)
        {
            var viseme = visemes[index];
            var visemeWhen = VisemeIs(viseme.Value);
            var state = _graph.AddState(
                settingsMachine,
                viseme.Name,
                LayoutOrigin + new Vector3(0, index * yStep, 0));
            SetVisemeClip(state, viseme.Shapes);
            state.TimeParameter = VRChatSupport.VoiceParameter;
            SetLipSyncTracking(state, false);
            _graph.AddEntryTransition(settingsMachine, state, visemeWhen);

            _graph.SetExitTransitions(
                state,
                visemeWhen.Complement(),
                VisemeTransitionDurationSeconds);
            _graph.AddExitTransitions(state, modeWhen.Complement(), 0f);
        }
    }

    private void SetVisemeClip(
        VirtualState state,
        IEnumerable<BlendShapeWeight> visemeShapes)
    {
        var output = new BlendShapeWeightSet(visemeShapes);
        state.SetNewClip(state.Name).AddBlendShapeAnimations(
            _avatarContext.BodyPath,
            output.Select(shape => new BlendShapeWeightAnimation(
                shape.Name,
                AnimationCurve.Linear(0f, 0f, 1f, shape.Weight))));
    }

    private static DnfCondition VisemeIs(int value)
        => DnfCondition.Single(
            new AnimatorConditionRule(
                new AnimatorCondition
                {
                    parameter = VRChatSupport.VisemeParameter,
                    mode = AnimatorConditionMode.Equals,
                    threshold = value
                },
                AnimatorControllerParameterType.Int),
            ParameterDomainRegistry.Empty);

    private static Viseme[] GetVisemes(VrcVisemeLipSyncShapes shapes)
        => new[]
        {
            new Viseme(0, "sil", shapes.Sil),
            new Viseme(1, "PP", shapes.PP),
            new Viseme(2, "FF", shapes.FF),
            new Viseme(3, "TH", shapes.TH),
            new Viseme(4, "DD", shapes.DD),
            new Viseme(5, "kk", shapes.KK),
            new Viseme(6, "CH", shapes.CH),
            new Viseme(7, "SS", shapes.SS),
            new Viseme(8, "nn", shapes.NN),
            new Viseme(9, "RR", shapes.RR),
            new Viseme(10, "aa", shapes.AA),
            new Viseme(11, "E", shapes.E),
            new Viseme(12, "ih", shapes.IH),
            new Viseme(13, "oh", shapes.OH),
            new Viseme(14, "ou", shapes.OU)
        };

    private static void SetLipSyncTracking(VirtualState state, bool isTracking)
    {
        var control = state.EnsureBehavior<VRCAnimatorTrackingControl>();
        control.trackingMouth = isTracking
            ? VRCAnimatorTrackingControl.TrackingType.Tracking
            : VRCAnimatorTrackingControl.TrackingType.Animation;
    }

    private readonly record struct Viseme(
        int Value,
        string Name,
        IReadOnlyList<BlendShapeWeight> Shapes);
}
