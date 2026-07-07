using UnityEditor.Animations;
using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build.Animator;

internal class AnimatorInstaller : InstallerBase
{
    private const int InitLayerPriority = -1;

    private VirtualClip _initializationClip = null!;

    private readonly float _transitionDurationSeconds;
    private readonly Dictionary<ExpressionClipKey, VirtualClip> _expressionClipCache = new();
    private readonly LipSyncInstaller _lipSyncInstaller;
    private readonly BlinkInstaller _blinkInstaller;
    private readonly IReadOnlyCollection<string> _externallyControlledBlendShapeNames;
    private readonly VirtualControllerContext _controllerContext;
    private readonly BuildSettings _settings;
    private readonly IReadOnlyCollection<string> _excludedBlendShapeNames;
    private readonly string _lockFacialParameterName;

    private RuntimeDomainRegistry _runtimeRegistry = null!;
    private OutputUnit _currentUnit = null!;

    private static readonly Vector3 ExclusiveStatePosition = new Vector3(300, 0, 0);

    public AnimatorInstaller(
        VirtualAnimatorController virtualController,
        BuildSettings settings,
        bool useWriteDefaults,
        IAnimatorPlatformServices platformServices,
        IReadOnlyCollection<string> externallyControlledBlendShapeNames,
        VirtualControllerContext controllerContext) : base(virtualController, settings.AvatarContext, useWriteDefaults, platformServices)
    {
        _settings = settings;
        _transitionDurationSeconds = settings.DurationSeconds;
        _externallyControlledBlendShapeNames = externallyControlledBlendShapeNames;
        _controllerContext = controllerContext;
        _excludedBlendShapeNames = settings.ExcludedBlendShapeNames;
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

        var plan = AnimatorBuildPlan.From(expressionProgram, _settings, _platformServices, _controllerContext);
        _runtimeRegistry = plan.RuntimeRegistry;
        _runtimeRegistry.EnsureParameters(_controller);

        CreateInitializationLayer(InitLayerPriority);
        InstallPlan(plan, LayerPriority);
        AddBlendShapeInitialization(_blinkInstaller.ShapesToInitialize);
    }

    private void CreateInitializationLayer(int priority)
    {
        var layer = AddLayer("Initial", priority, false);
        var state = AddState(layer, "Initial", position: ExclusiveStatePosition);
        _initializationClip = state.SetNewClip(state.Name);

        var animations = _avatarContext.FaceRenderer
            .GetBlendShapeWeights(_avatarContext.FaceMesh)
            .Where(shape => !IsExcludedFromInitialization(shape.Name))
            .Select(shape => shape.ToBlendShapeAnimation())
            .ToArray();
        _initializationClip.AddBlendShapeAnimations(_avatarContext.BodyPath, animations);
    }

    private void AddBlendShapeInitialization(IEnumerable<BlendShapeWeight> blendShapes)
    {
        foreach (var shape in blendShapes.Where(shape => !IsExcludedFromInitialization(shape.Name)))
        {
            _initializationClip.AddBlendShapeAnimation(_avatarContext.BodyPath, shape.ToBlendShapeAnimation());
        }
    }

    private bool IsExcludedFromInitialization(string blendShapeName)
    {
        return _externallyControlledBlendShapeNames.Contains(blendShapeName)
            || _excludedBlendShapeNames.Contains(blendShapeName);
    }

    private void InstallPlan(AnimatorBuildPlan plan, int priority)
    {
        foreach (var unit in plan.Units)
        {
            _currentUnit = unit;
            foreach (var layer in unit.Layers)
            {
                InstallPackedLayer(layer, priority);
            }

            _blinkInstaller.AddEyeBlinkLayer(unit, plan.RuntimeRegistry.EyeBlink);
            _lipSyncInstaller.AddLipSyncLayer(unit, plan.RuntimeRegistry.LipSync);
        }
    }

    private void InstallPackedLayer(PackedLayer packedLayer, int priority)
    {
        var layer = AddLayer(packedLayer.Name, priority);
        var defaultState = AddState(layer, "PassThrough", ExclusiveStatePosition);
        AsPassThrough(defaultState);

        var position = ExclusiveStatePosition + new Vector3(0, 2 * PositionYStep, 0);
        for (var index = 0; index < packedLayer.Items.Count; index++)
        {
            var item = packedLayer.Items[index];
            var when = packedLayer.StateWhen(index);
            AddExpressionStates(layer, defaultState, item, when, position);
            position.y += Math.Max(1, when.Cases.Count) * PositionYStep;
        }
    }

    private void AddExpressionStates(
        VirtualLayer layer,
        VirtualState defaultState,
        AnimatorExpressionItem item,
        DnfCondition when,
        Vector3 basePosition)
    {
        if (when.IsNever) return;

        var states = AddStatesForDnf(layer, defaultState, when, _transitionDurationSeconds, basePosition);
        foreach (var state in states)
        {
            state.Name = item.Expression.Name;
            AddExpressionToState(state, item);
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
        public ExpressionRuntimeModes RuntimeModes { get; }
        public int UnitId { get; }

        public ExpressionClipKey(
            BlendShapeWeightAnimationSet animationSet,
            ExpressionSettings expressionSettings,
            ExpressionRuntimeModes runtimeModes,
            int unitId)
        {
            AnimationSet = new(animationSet);
            ExpressionSettings = expressionSettings;
            RuntimeModes = runtimeModes;
            UnitId = unitId;
        }
    }

    private void AddExpressionToState(VirtualState state, AnimatorExpressionItem item)
    {
        var expression = item.Expression;
        var key = new ExpressionClipKey(expression.AnimationSet, expression.ExpressionSettings, item.RuntimeModes, _currentUnit.Id);
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
            _runtimeRegistry.AddModeCurves(clip, _currentUnit, item);
            SetExpressionSettings(state, clip, expression.ExpressionSettings);
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
}
