using UnityEditor.Animations;
using nadena.dev.ndmf.animator;
using aoyon.facetune.build;

namespace aoyon.facetune.animator;

internal class AnimatorInstaller : InstallerBase
{
    private const int InitLayerPriority = -1; // 上書きを意図しない初期化レイヤー。

    private readonly float _transitionDurationSeconds;

    private readonly Dictionary<Expression, VirtualClip> _expressionClipCache = new();

    private readonly LipSyncInstaller _lipSyncInstaller;
    private readonly BlinkInstaller _blinkInstaller;

    private static readonly Vector3 ExclusiveStatePosition = new Vector3(300, 0, 0);

    public AnimatorInstaller(VirtualAnimatorController virtualController, SessionContext sessionContext, bool useWriteDefaults) : base(virtualController, sessionContext, useWriteDefaults)
    {
        _transitionDurationSeconds = 0.1f; // 変更可能にすべき？
        _lipSyncInstaller = new LipSyncInstaller(virtualController, sessionContext, useWriteDefaults);
        _blinkInstaller = new BlinkInstaller(virtualController, sessionContext, useWriteDefaults);
    }

    public void Execute(InstallerData installerData)
    {
        var patternData = installerData.PatternData;
        if (patternData.IsEmpty) return;

        CreateDefaultLayer(patternData, InitLayerPriority);

        foreach (var patternGroup in patternData.GetConsecutiveTypeGroups())
        {
            var type = patternGroup.Type;
            if (type == typeof(Preset))
            {
                var presets = patternGroup.Group.Select(item => (Preset)item).ToList();
                InstallPresetGroup(presets, LayerPriority);
            }
            else if (type == typeof(SingleExpressionPattern))
            {
                var singleExpressionPatterns = patternGroup.Group.Select(item => (SingleExpressionPattern)item).ToList();
                InstallSingleExpressionPatternGroup(singleExpressionPatterns, LayerPriority);
            }
        }
        
        var patternExpressions = patternData.GetAllExpressions();
        if (patternExpressions.Any(e => e.FacialSettings.AllowEyeBlink == TrackingPermission.Disallow
            || e.FacialSettings.AdvancedEyBlinkSettings.UseAdvancedEyeBlink))
        {
            _blinkInstaller.AddEyeBlinkLayer();
            _blinkInstaller.EditDefaultClip(GetRequiredDefaultClip(InitLayerPriority));
        }

        _lipSyncInstaller.MayAddLipSyncLayers();
        _lipSyncInstaller.EditDefaultClip(GetRequiredDefaultClip(InitLayerPriority));
    }

    private void CreateDefaultLayer(PatternData patternData, int priority)
    {
        var initializeClip = GetOrCreateDefautLayerAndClip(priority, "Initialize");
        var shapesAnimations = _sessionContext.SafeZeroBlendShapes
            .ToGenericAnimations(_sessionContext.BodyPath);
        initializeClip.SetAnimations(shapesAnimations);

        var allBindings = patternData.GetAllExpressions().SelectMany(e => e.Animations).Select(a => a.CurveBinding).Distinct();
        var facialBinding = SerializableCurveBinding.FloatCurve(_sessionContext.BodyPath, typeof(SkinnedMeshRenderer), FaceTuneConsts.AnimatedBlendShapePrefix);
        var nonFacialBindings = allBindings.Where(b => b != facialBinding);
        if (!nonFacialBindings.Any()) return;
        var propertiesAnimations = AnimatorHelper.GetDefaultValueAnimations(_sessionContext.Root, nonFacialBindings);
        initializeClip.SetAnimations(propertiesAnimations);
    }

    private void InstallPresetGroup(IReadOnlyList<Preset> presets, int priority)
    {
        var maxPatterns = presets.Max(p => p.Patterns.Count); 
        if (maxPatterns == 0) return;

        VirtualLayer[] layers = new VirtualLayer[maxPatterns];
        VirtualState[] defaultStates = new VirtualState[maxPatterns];

        for (int i = 0; i < maxPatterns; i++)
        {
            bool layerCreatedForThisIndex = false;
            foreach (var preset in presets)
            {
                if (i >= preset.Patterns.Count) continue;
                var pattern = preset.Patterns[i];
                if (pattern == null || pattern.ExpressionWithConditions == null || !pattern.ExpressionWithConditions.Any()) continue;

                if (!layerCreatedForThisIndex)
                {
                    layers[i] = AddLayer($"Preset Pattern Group {i}", priority); 
                    defaultStates[i] = AddState(layers[i], "PassThrough", ExclusiveStatePosition);
                    AsPassThrough(defaultStates[i]);
                    layerCreatedForThisIndex = true;
                }
                
                var basePosition = layers[i].StateMachine!.States.Last().Position + new Vector3(0, 2 * PositionYStep, 0);
                AddExpressionWithConditions(layers[i], defaultStates[i], pattern.ExpressionWithConditions, basePosition);
            }
        }
    }

    private void InstallSingleExpressionPatternGroup(IReadOnlyList<SingleExpressionPattern> singleExpressionPatterns, int priority)
    {
        for (int i = 0; i < singleExpressionPatterns.Count; i++)
        {
            var singleExpressionPattern = singleExpressionPatterns[i];
            if (singleExpressionPattern == null || singleExpressionPattern.ExpressionPattern.ExpressionWithConditions == null || 
                !singleExpressionPattern.ExpressionPattern.ExpressionWithConditions.Any()) continue;

            var layer = AddLayer(singleExpressionPattern.Name, priority);
            var defaultState = AddState(layer, "PassThrough", ExclusiveStatePosition);
            AsPassThrough(defaultState);
            var basePosition = ExclusiveStatePosition + new Vector3(0, 2 * PositionYStep, 0);
            AddExpressionWithConditions(layer, defaultState, singleExpressionPattern.ExpressionPattern.ExpressionWithConditions, basePosition); 
        }
    }

    private void AddExpressionWithConditions(VirtualLayer layer, VirtualState defaultState, IReadOnlyList<ExpressionWithConditions> expressionWithConditions, Vector3 basePosition)
    {
        var trueCondition = new[] { ParameterCondition.Bool(TrueParameterName, true) };
        // Pattern内で下のExpressionが優先されることを保証するため、Animatorにおいて上のStateが優先される仕様を用いるためにReverseする。
        // ワークアラウンド
        var expressionWithConditionList = expressionWithConditions.Reverse().Select(e =>
        {
            if (!e.Conditions.Any())
            {
                e.SetConditions(e.Conditions.Concat(trueCondition).ToList());
            }
            return e;
        }).ToList();
        var duration = _transitionDurationSeconds;
        var conditionsPerState = expressionWithConditionList.Select(e => (IEnumerable<Condition>)e.Conditions).ToArray();
        var states = AddExclusiveStates(layer, defaultState, conditionsPerState, duration, basePosition);
        for (int i = 0; i < states.Length; i++)
        {
            var expressionWithCondition = expressionWithConditionList[i];
            var state = states[i];
            state.Name = expressionWithCondition.Expression.Name;
            AddExpressionToState(state, expressionWithCondition.Expression);
        }
    }

    private VirtualState[] AddExclusiveStates(VirtualLayer layer, VirtualState defaultState, IEnumerable<Condition>[] conditionsPerState, float duration, Vector3 basePosition)
    {
        var states = new VirtualState[conditionsPerState.Length];
        var newEntryTransitions = new List<VirtualTransition>();

        var position = basePosition;

        for (int i = 0; i < conditionsPerState.Length; i++)
        {
            var conditions = conditionsPerState[i];

            var state = AddState(layer, "unnamed", position);
            states[i] = state;
            position.y += PositionYStep;

            var entryTransition = VirtualTransition.Create();
            entryTransition.SetDestination(state);
            entryTransition.Conditions = ToAnimatorConditions(conditions).ToImmutableList();
            newEntryTransitions.Add(entryTransition);

            var newExpressionStateTransitions = new List<VirtualStateTransition>();
            foreach (var condition in conditions)
            {
                var exitTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(duration);
                exitTransition.SetExitDestination();
                exitTransition.Conditions = ImmutableList.Create(ToAnimatorCondition(condition.ToNegation()));
                newExpressionStateTransitions.Add(exitTransition);
            }
            state.Transitions = ImmutableList.CreateRange(state.Transitions.Concat(newExpressionStateTransitions));
        }

        // entry to expressinの全TrasntionのORをdefault to Exitに入れる
        var exitTransitionsFromDefault = new List<VirtualStateTransition>();
        foreach (var entryTr in newEntryTransitions)
        {
            var exitTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(duration);
            exitTransition.SetExitDestination();
            exitTransition.Conditions = entryTr.Conditions; // newEntryTransition の条件をそのまま使用
            exitTransitionsFromDefault.Add(exitTransition);
        }
        defaultState.Transitions = ImmutableList.CreateRange(defaultState.Transitions.Concat(exitTransitionsFromDefault));

        layer.StateMachine!.EntryTransitions = ImmutableList.CreateRange(layer.StateMachine!.EntryTransitions.Concat(newEntryTransitions));

        return states;
    }
    
    private IEnumerable<AnimatorCondition> ToAnimatorConditions(IEnumerable<Condition> conditions)
    {
        if (!conditions.Any()) return new List<AnimatorCondition>();
        var transitionConditions = new List<AnimatorCondition>();
        foreach (var cond in conditions)
        {
            var animatorCondition = ToAnimatorCondition(cond);
            transitionConditions.Add(animatorCondition);
        }
        return transitionConditions;
    }

    private AnimatorCondition ToAnimatorCondition(Condition condition)
    {
        var (animatorCondition, parameter, parameterType) = condition.ToAnimatorCondition();
        _controller.EnsureParameterExists(parameterType, parameter);
        return animatorCondition;
    }

    private void AddExpressionToState(VirtualState state, Expression expression)
    {
        if (state.TryGetClip(out var clip))
        {
            var duplicate = clip.Clone();
            Impl(duplicate);
            state.Motion = duplicate;
        }
        else
        {
            if (_expressionClipCache.TryGetValue(expression, out var cachedClip))
            {
                clip = cachedClip;
                state.Motion = clip;
            }
            else
            {
                clip = state.CreateClip(state.Name);
                Impl(clip);
                _expressionClipCache[expression] = clip;
            }
        }

        void Impl(VirtualClip clip)
        {
            clip.SetAnimations(expression.Animations);
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
