using UnityEditor.Animations;
using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build.Animator;

internal class AnimatorInstaller : InstallerBase
{
    private const int InitLayerPriority = -1; // 上書きを意図しない初期化レイヤー。
    
    private VirtualClip _nonMMDInitializationClip = null!;
    private VirtualClip _MMDInitializationClip = null!;

    private readonly float _transitionDurationSeconds;

    private readonly Dictionary<ExpressionClipKey, VirtualClip> _expressionClipCache = new();

    private readonly LipSyncInstaller _lipSyncInstaller;
    private readonly BlinkInstaller _blinkInstaller;
    private readonly IReadOnlyCollection<string> _externallyControlledBlendShapeNames;
    private readonly string _lockFacialParameterName;

    private static readonly Vector3 ExclusiveStatePosition = new Vector3(300, 0, 0);

    public AnimatorInstaller(
        VirtualAnimatorController virtualController,
        BuildSettings settings,
        bool useWriteDefaults,
        IAnimatorPlatformServices platformServices,
        IReadOnlyCollection<string> externallyControlledBlendShapeNames) : base(virtualController, settings.AvatarContext, useWriteDefaults, platformServices)
    {
        _transitionDurationSeconds = 0.1f; // 変更可能にすべき？
        _externallyControlledBlendShapeNames = externallyControlledBlendShapeNames;
        _lockFacialParameterName = settings.LockFacialParameterName;
        _lipSyncInstaller = new LipSyncInstaller(virtualController, settings.AvatarContext, useWriteDefaults, platformServices, settings.DisableLipSyncParameterName);
        _blinkInstaller = new BlinkInstaller(virtualController, settings.AvatarContext, useWriteDefaults, platformServices, settings.DisableEyeBlinkParameterName);
        if (!string.IsNullOrWhiteSpace(settings.LockFacialParameterName))
        {
            _controller.EnsureBoolParameterExists(settings.LockFacialParameterName, false);
        }
    }

    public void Execute(ExpressionProgram expressionProgram)
    {
        if (expressionProgram.IsEmpty) return;

        CreateInitializationLayer(InitLayerPriority);
        InstallExpressionProgram(expressionProgram, LayerPriority);
        
        if (expressionProgram.Items.Any(e => e.FacialSettings.AllowEyeBlink == TrackingPermission.Disallow
            || e.FacialSettings.AdvancedEyBlinkSettings.UseAdvancedEyeBlink))
        {
            _blinkInstaller.AddEyeBlinkLayer();
            AddBlendShapeInitialization(_blinkInstaller.ShapesToInitialize);
        }

        _lipSyncInstaller.MayAddLipSyncLayers();
    }

    private void CreateInitializationLayer(int priority)
    {
        var nonMMDLayer = AddLayer("Initial", priority, false);
        var nonMMDState = AddState(nonMMDLayer, "Initial", position: ExclusiveStatePosition);
        _nonMMDInitializationClip = nonMMDState.SetNewClip(nonMMDState.Name);

        var MMDLayer = AddLayer("Initial (MMD)", priority, true);
        var MMDState = AddState(MMDLayer, "Initial (MMD)", position: ExclusiveStatePosition);
        _MMDInitializationClip = MMDState.SetNewClip(MMDState.Name);

        var animations = new List<BlendShapeWeightAnimation>();
        var mmdAnimations = new List<BlendShapeWeightAnimation>();

        foreach (var shape in _avatarContext.FaceRenderer.GetBlendShapeWeights(_avatarContext.FaceMesh).Where(b => !_externallyControlledBlendShapeNames.Contains(b.Name)))
        {
            animations.Add(shape.ToBlendShapeAnimation());
        }

        _MMDInitializationClip.AddBlendShapeAnimations(_avatarContext.BodyPath, mmdAnimations);
        _nonMMDInitializationClip.AddBlendShapeAnimations(_avatarContext.BodyPath, animations);
    }

    private void AddBlendShapeInitialization(IEnumerable<BlendShapeWeight> blendShapes)
    {
        foreach (var shape in blendShapes)
        {
            _nonMMDInitializationClip.AddBlendShapeAnimation(_avatarContext.BodyPath, shape.ToBlendShapeAnimation());
        }
    }

    private void InstallExpressionProgram(ExpressionProgram expressionProgram, int priority)
    {
        foreach (var item in expressionProgram.Items)
        {
            InstallExpressionItem(item, priority);
        }
    }

    private void InstallExpressionItem(ExpressionItem item, int priority)
    {
        var layer = AddLayer(item.Name, priority);
        var defaultState = AddState(layer, "PassThrough", ExclusiveStatePosition);
        AsPassThrough(defaultState);

        AddExpressionStates(
            layer,
            defaultState,
            item,
            item.ActiveWhen,
            ExclusiveStatePosition + new Vector3(0, 2 * PositionYStep, 0));
    }

    private void AddExpressionStates(
        VirtualLayer layer,
        VirtualState defaultState,
        ExpressionItem expression,
        DnfCondition when,
        Vector3 basePosition)
    {
        if (when.IsNever) return;

        var duration = _transitionDurationSeconds;
        var states = AddStatesForDnf(layer, defaultState, when, duration, basePosition);
        foreach (var state in states)
        {
            state.Name = expression.Name;
            AddExpressionToState(state, expression);
        }
    }

    private VirtualState[] AddStatesForDnf(VirtualLayer layer, VirtualState defaultState, DnfCondition when, float duration, Vector3 basePosition)
    {
        var states = new List<VirtualState>();
        var newEntryTransitions = new List<VirtualTransition>();
        var position = basePosition;

        var lockFacialCondition = string.IsNullOrWhiteSpace(_lockFacialParameterName)
            ? new AnimatorCondition { parameter = TrueParameterName, mode = AnimatorConditionMode.If }
            : new AnimatorCondition { parameter = _lockFacialParameterName, mode = AnimatorConditionMode.IfNot };

        foreach (var whenCase in when.Cases)
        {
            var state = AddState(layer, "unnamed", position);
            states.Add(state);
            position.y += PositionYStep;

            var entryTransition = VirtualTransition.Create();
            entryTransition.SetDestination(state);
            entryTransition.Conditions = ToAnimatorConditions(whenCase).ToImmutableList();
            newEntryTransitions.Add(entryTransition);

            var exitTransitions = new List<VirtualStateTransition>();
            foreach (var exitCase in when.Not().Cases)
            {
                var exitTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(duration);
                exitTransition.SetExitDestination();
                exitTransition.Conditions = ToAnimatorConditions(exitCase).Append(lockFacialCondition).ToImmutableList();
                exitTransitions.Add(exitTransition);
            }
            state.Transitions = ImmutableList.CreateRange(state.Transitions.Concat(exitTransitions));
        }

        var exitTransitionsFromDefault = new List<VirtualStateTransition>();
        foreach (var entryTransition in newEntryTransitions)
        {
            var exitTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(duration);
            exitTransition.SetExitDestination();
            exitTransition.Conditions = entryTransition.Conditions.Add(lockFacialCondition);
            exitTransitionsFromDefault.Add(exitTransition);
        }
        defaultState.Transitions = ImmutableList.CreateRange(defaultState.Transitions.Concat(exitTransitionsFromDefault));

        layer.StateMachine!.EntryTransitions = ImmutableList.CreateRange(layer.StateMachine!.EntryTransitions.Concat(newEntryTransitions));

        return states.ToArray();
    }
    
    private IEnumerable<AnimatorCondition> ToAnimatorConditions(DnfCase conditionCase)
    {
        return conditionCase.Rules.Select(ToAnimatorCondition);
    }

    private AnimatorCondition ToAnimatorCondition(DnfRule rule)
    {
        var animatorConditionRule = (AnimatorConditionRule)rule;
        _controller.EnsureParameterExists(animatorConditionRule.ParameterType, animatorConditionRule.ParameterName);
        return animatorConditionRule.Condition;
    }

    private sealed record class ExpressionClipKey
    {
        public BlendShapeWeightAnimationSet AnimationSet { get; }
        public ExpressionSettings ExpressionSettings { get; }
        public FacialSettings FacialSettings { get; }

        public ExpressionClipKey(
            BlendShapeWeightAnimationSet animationSet,
            ExpressionSettings expressionSettings,
            FacialSettings facialSettings)
        {
            AnimationSet = new(animationSet);
            ExpressionSettings = expressionSettings;
            FacialSettings = facialSettings;
        }
    }

    private void AddExpressionToState(VirtualState state, ExpressionItem expression)
    {
        var key = new ExpressionClipKey(expression.AnimationSet, expression.ExpressionSettings, expression.FacialSettings);
        if (state.TryGetClip(out var clip))
        {
            var duplicate = clip.Clone();
            Impl(duplicate);
            state.Motion = duplicate;
        }
        else
        {
            if (_expressionClipCache.TryGetValue(key, out var cachedClip))
            {
                clip = cachedClip;
                state.Motion = clip;
            }
            else
            {
                clip = state.SetNewClip(state.Name);
                Impl(clip);
                _expressionClipCache[key] = clip;
            }
        }

        void Impl(VirtualClip clip)
        {
            clip.AddBlendShapeAnimations(_avatarContext.BodyPath, expression.AnimationSet);
            SetExpressionSettings(state, clip, expression.ExpressionSettings);
            SetFacialSettings(clip, expression.FacialSettings);
        }
    }

    private void SetExpressionSettings(VirtualState state, VirtualClip clip, ExpressionSettings expressionSettings)
    {
        if (expressionSettings.LoopTime)
        {
            var settings = clip.Settings;
            settings.loopTime = true;
            clip.Settings = settings;
        }
        else if (!string.IsNullOrEmpty(expressionSettings.MotionTimeParameterName))
        {
            _controller.EnsureParameterExists(AnimatorControllerParameterType.Float, expressionSettings.MotionTimeParameterName);
            state.TimeParameter = expressionSettings.MotionTimeParameterName;
        }
    }

    private void SetFacialSettings(VirtualClip clip, FacialSettings? facialSettings)
    {
        if (facialSettings == null) return;
        _blinkInstaller.SetSettings(clip, facialSettings);
        _lipSyncInstaller.SetSettings(clip, facialSettings);
    }
}
