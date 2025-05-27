using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core; 
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.VRC;

namespace com.aoyon.facetune.animator;

internal class AnimatorInstaller
{
    private readonly SessionContext _sessionContext;
    private readonly BuildContext _buildContext;
    private readonly AacFlBase _aac;
    private readonly AacFlController _ctrl;

    private const string SystemName = "FaceTune";
    private const bool UseWriteDefaults = true;

    private int layer_sum = 0;

    public AnimatorInstaller(BuildContext buildContext, SessionContext sessionContext)
    {
        _buildContext = buildContext;
        _sessionContext = sessionContext;
        _aac = CreateAac();
        _ctrl = _aac.NewAnimatorController();
    }

    public void SaveAsMergeAnimator(int layerPriority)
    {
        var mergeAnimator = _sessionContext.Root.AddComponent<ModularAvatarMergeAnimator>();
        mergeAnimator.animator = _ctrl.AnimatorController;
        mergeAnimator.layerType = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX;
        mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
        mergeAnimator.layerPriority = layerPriority;
        mergeAnimator.mergeAnimatorMode = MergeAnimatorMode.Append;
    }

    private AacFlLayer Addlayer(string layerName)
    {
        var layer = _ctrl.NewLayer($"{layer_sum}_{layerName}");
        layer.StateMachine.EnsureBehaviour<ModularAvatarMMDLayerControl>().DisableInMMDMode = true;
        layer_sum++;
        return layer;
    }

    private void AddExpressionsToState(AacFlState state, IEnumerable<Expression> expressions)
    {
        var facialExpressions = expressions.UnityOfType<FacialExpression>();
        var animationExpressions = expressions.UnityOfType<AnimationExpression>();
        
        // Todo:animationExpressionsとfacialExpressionsの統合や複数のanimationExpressionsへの対応など
        if (animationExpressions.Any())
        {
            state.WithAnimation(animationExpressions.First().Clip);
        }
        else
        {
            var clip = _aac.NewClip();
            var renderer = _sessionContext.FaceRenderer;
            var shapes = new BlendShapeSet();
            foreach (var facialExpression in facialExpressions) shapes.Add(facialExpression.BlendShapeSet);
            foreach (var blendShape in shapes.BlendShapes)
            {
                clip.BlendShape(renderer, blendShape.Name, blendShape.Weight);
            }
            state.WithAnimation(clip);
        }
    }

    public void CreateDefaultLayer()
    {
        var defaultLayer = Addlayer("Default");
        var defaultState = defaultLayer.NewState("Default");
        AddExpressionsToState(defaultState, new[] { _sessionContext.DefaultExpression });
        // SetTracks(defaultState, _sessionContext.DefaultExpression);
    }

    public void InstallPatternData(PatternData patternData)
    {
        foreach (var patternGroup in patternData.GetConsecutiveTypeGroups())
        {
            var type = patternGroup.Type;

            if (type == typeof(Preset))
            {
                var presets = patternGroup.Group.Select(item => (Preset)item).ToList();
                InstallPresetGroup(presets);
            }
            else if (type == typeof(SingleExpressionPattern))
            {
                var singleExpressionPatterns = patternGroup.Group.Select(item => (SingleExpressionPattern)item).ToList();
                InstallSingleExpressionPatternGroup(singleExpressionPatterns);
            }
        }
    }

    private const float TransitionDurationSeconds = 0.1f; // 変更可能にすべき？
    private void InstallPresetGroup(IReadOnlyList<Preset> presets)
    {
        var layers = new AacFlLayer[presets.Max(p => p.Patterns.Count)];
        var defaultStates = new AacFlState[presets.Max(p => p.Patterns.Count)];

        for (var i = 0; i < presets.Count; i++)
        {
            var preset = presets[i];
            var patterns = preset.Patterns;

            for (var j = 0; j < patterns.Count; j++)
            {
                var pattern = patterns[j];
                if (pattern == null || pattern.ExpressionWithConditions.Count == 0) continue;

                var layer = layers[j];
                if (layer == null)
                {
                    layer = Addlayer($"Merged Presets Priority: {j}");
                    layers[j] = layer;
                }

                var defaultState = defaultStates[j];
                if (defaultState == null)
                {
                    defaultState = layer.NewState("Default");
                    defaultStates[j] = defaultState;
                    SetTracks(defaultState, _sessionContext.DefaultExpression);
                    defaultState.Exits().Automatically().WithTransitionDurationSeconds(TransitionDurationSeconds);
                }
                                    
                ProcessExpressionWithConditions(layer, pattern.ExpressionWithConditions);
            }
        }
    }

    private void InstallSingleExpressionPatternGroup(IReadOnlyList<SingleExpressionPattern> singleExpressionPatterns)
    {
        foreach (var singleExpressionPattern in singleExpressionPatterns)
        {
            if (singleExpressionPattern == null || singleExpressionPattern.ExpressionPattern.ExpressionWithConditions.Count == 0) continue;

            var layer = Addlayer(singleExpressionPattern.Name);
            var defaultState = layer.NewState("Default");
            SetTracks(defaultState, _sessionContext.DefaultExpression);

            defaultState.Exits().Automatically().WithTransitionDurationSeconds(TransitionDurationSeconds);
            ProcessExpressionWithConditions(layer, singleExpressionPattern.ExpressionPattern.ExpressionWithConditions);
        }
    }

    private void ProcessExpressionWithConditions(AacFlLayer layer, IEnumerable<ExpressionWithCondition> expressionWithConditions)
    {
        foreach (var expressionWithCondition in expressionWithConditions)
        {
            var conditions = expressionWithCondition.Conditions;
            var expressions = expressionWithCondition.Expressions;

            if (!expressions.Any()) continue;

            var expressionState = layer.NewState(expressions.First().Name);

            AddExpressionsToState(expressionState, expressions);
            SetTracks(expressionState, expressions);

            var entryToExpressionConditions = expressionState.TransitionsFromEntry().WhenConditions();
            var expressionToExitConditions = expressionState.Exits().WithTransitionDurationSeconds(TransitionDurationSeconds).WhenConditions();
            
            if (conditions.Any())
            {
                foreach (var cond in conditions)
                {
                    AddConditionToTransitions(cond, layer, entryToExpressionConditions, false);
                }

                // Exit時は条件を反転させ、OR条件。ただし、Transition生成時の初期のConditionが空とならないよう、最初だけAND
                AddConditionToTransitions(NegateCondition(conditions.First()), layer, expressionToExitConditions, false); // 最初の条件はAND
                foreach (var cond in conditions.Skip(1))
                {
                    AddConditionToTransitions(NegateCondition(cond), layer, expressionToExitConditions, true); // 2つ目以降はOR
                }
            }
        }
    }

    private Condition NegateCondition(Condition condition)
    {
        return condition switch
        {
            HandGestureCondition hgc => hgc with { ComparisonType = Negate(hgc.ComparisonType) },
            ParameterCondition pc => pc.ParameterType switch
            {
                ParameterType.Int =>
                    pc with { 
                        IntComparisonType = Negate(pc.IntComparisonType, pc.IntValue).newType,
                        IntValue = Negate(pc.IntComparisonType, pc.IntValue).newValue
                    },
                ParameterType.Float =>
                    pc with { FloatComparisonType = Negate(pc.FloatComparisonType) },
                ParameterType.Bool => pc with { BoolValue = Negate(pc.BoolValue) },
                _ => pc
            },
            _ => condition
        };
    }

    private void AddConditionToTransitions(
        Condition condition,
        AacFlLayer layer,
        AacFlTransitionContinuation conditions,
        bool useOrCondition)
    {
        switch (condition)
        {
            case HandGestureCondition handGestureCondition:
                var gesture = Convert(handGestureCondition.HandGesture);
                var isLeft = handGestureCondition.Hand == Hand.Left;
                var gestureParam = isLeft ? layer.Av3().GestureLeft : layer.Av3().GestureRight;
                AddComparisonCondition(conditions, gestureParam, handGestureCondition.ComparisonType, gesture, isOr: useOrCondition);
                break;
            case ParameterCondition parameterCondition:
                switch (parameterCondition.ParameterType)
                {
                    case ParameterType.Int:
                        var intParam = layer.IntParameter(parameterCondition.ParameterName);
                        AddComparisonCondition(conditions, intParam, parameterCondition.IntComparisonType, parameterCondition.IntValue, isOr: useOrCondition);
                        break;
                    case ParameterType.Float:
                        var floatParam = layer.FloatParameter(parameterCondition.ParameterName);
                        AddComparisonCondition(conditions, floatParam, parameterCondition.FloatComparisonType, parameterCondition.FloatValue, isOr: useOrCondition);
                        break;
                    case ParameterType.Bool:
                        var boolParam = layer.BoolParameter(parameterCondition.ParameterName);
                        AddComparisonCondition(conditions, boolParam, parameterCondition.BoolValue, isOr: useOrCondition);
                        break;
                }
                break;
        }
    }

    private void AddComparisonCondition<TEnum>(
        AacFlTransitionContinuation conditions,
        AacFlEnumIntParameter<TEnum> parameter,
        BoolComparisonType comparisonType,
        TEnum value,
        bool isOr = false) where TEnum : Enum
    {
        switch (comparisonType)
        {
            case BoolComparisonType.Equal:
                if (isOr) conditions.Or().When(parameter.IsEqualTo(value));
                else conditions.And(parameter.IsEqualTo(value));
                break;
            case BoolComparisonType.NotEqual:
                if (isOr) conditions.Or().When(parameter.IsNotEqualTo(value));
                else conditions.And(parameter.IsNotEqualTo(value));
                break;
        }
    }

    private void AddComparisonCondition(
        AacFlTransitionContinuation conditions,
        AacFlFloatParameter parameter,
        FloatComparisonType comparisonType,
        float value,
        bool isOr = false)
    {
        switch (comparisonType)
        {
            case FloatComparisonType.GreaterThan:
                if (isOr) conditions.Or().When(parameter.IsGreaterThan(value));
                else conditions.And(parameter.IsGreaterThan(value));
                break;
            case FloatComparisonType.LessThan:
                if (isOr) conditions.Or().When(parameter.IsLessThan(value));
                else conditions.And(parameter.IsLessThan(value));
                break;
        }
    }

    private void AddComparisonCondition(
        AacFlTransitionContinuation conditions,
        AacFlIntParameter parameter,
        IntComparisonType comparisonType,
        int value,
        bool isOr = false)
    {
        switch (comparisonType)
        {
            case IntComparisonType.Equal:
                if (isOr) conditions.Or().When(parameter.IsEqualTo(value));
                else conditions.And(parameter.IsEqualTo(value));
                break;
            case IntComparisonType.NotEqual:
                if (isOr) conditions.Or().When(parameter.IsNotEqualTo(value));
                else conditions.And(parameter.IsNotEqualTo(value));
                break;
            case IntComparisonType.GreaterThan:
                if (isOr) conditions.Or().When(parameter.IsGreaterThan(value));
                else conditions.And(parameter.IsGreaterThan(value));
                break;
            case IntComparisonType.LessThan:
                if (isOr) conditions.Or().When(parameter.IsLessThan(value));
                else conditions.And(parameter.IsLessThan(value));
                break;
        }
    }

    private void AddComparisonCondition(
        AacFlTransitionContinuation conditions,
        AacFlBoolParameter parameter,
        bool value,
        bool isOr = false)
    {
        if (isOr)
            conditions.Or().When(parameter.IsEqualTo(value));
        else
            conditions.And(parameter.IsEqualTo(value));
    }

    private (IntComparisonType newType, int newValue) Negate(IntComparisonType type, int currentValue)
    {
        return type switch
        {
            IntComparisonType.Equal => (IntComparisonType.NotEqual, currentValue),
            IntComparisonType.NotEqual => (IntComparisonType.Equal, currentValue),
            IntComparisonType.GreaterThan => (IntComparisonType.LessThan, currentValue + 1), // val > X  -> val < X+1 (つまり val <= X)
            IntComparisonType.LessThan => (IntComparisonType.GreaterThan, currentValue - 1),   // val < X  -> val > X-1 (つまり val >= X)
            _ => (type, currentValue)
        };
    }

    private FloatComparisonType Negate(FloatComparisonType type)
    {
        return type switch
        {
            FloatComparisonType.GreaterThan => FloatComparisonType.LessThan,
            FloatComparisonType.LessThan => FloatComparisonType.GreaterThan,
            _ => type
        };
    }

    private BoolComparisonType Negate(BoolComparisonType type)
    {
        return type switch
        {
            BoolComparisonType.Equal => BoolComparisonType.NotEqual,
            BoolComparisonType.NotEqual => BoolComparisonType.Equal,
            _ => type
        };
    }

    private bool Negate(bool value)
    {
        return !value;
    }

    private static void SetTracks(AacFlState state, IEnumerable<Expression> expressions)
    {
        foreach (var expression in expressions) SetTracks(state, expression);
    }

    private static void SetTracks(AacFlState state, Expression expression)
    {   
        if (expression.AllowEyeBlink is TrackingPermission.Keep && expression.AllowLipSync is TrackingPermission.Keep) return;

        SetTracks(state, AacAv3.Av3TrackingElement.Eyes, expression.AllowEyeBlink);
        SetTracks(state, AacAv3.Av3TrackingElement.Mouth, expression.AllowLipSync);

        static void SetTracks(AacFlState state, AacAv3.Av3TrackingElement element, TrackingPermission permission)
        {
            switch (permission)
            {
                case TrackingPermission.Allow:
                    state.TrackingTracks(element);
                    break;
                case TrackingPermission.Disallow:
                    state.TrackingAnimates(element);
                    break;
                case TrackingPermission.Keep:
                    break;
            }
        }
    }

    private AacAv3.Av3Gesture Convert(HandGesture handGesture)
    {
        switch (handGesture)
        {
            case HandGesture.Neutral:
                return AacAv3.Av3Gesture.Neutral;
            case HandGesture.Fist:
                return AacAv3.Av3Gesture.Fist;
            case HandGesture.HandOpen:
                return AacAv3.Av3Gesture.HandOpen;
            case HandGesture.FingerPoint:
                return AacAv3.Av3Gesture.Fingerpoint;
            case HandGesture.Victory:
                return AacAv3.Av3Gesture.Victory;
            case HandGesture.RockNRoll:
                return AacAv3.Av3Gesture.RockNRoll;
            case HandGesture.HandGun:
                return AacAv3.Av3Gesture.HandGun;
            case HandGesture.ThumbsUp:
                return AacAv3.Av3Gesture.ThumbsUp;
            default:
                throw new ArgumentOutOfRangeException(nameof(handGesture), handGesture, null);
        }
    }
    private AacFlBase CreateAac()
    {
        return AacV1.Create(new AacConfiguration()
        {
            SystemName = SystemName,
            AnimatorRoot = _sessionContext.Root.transform,
            DefaultValueRoot = _sessionContext.Root.transform,
            AssetKey = GUID.Generate().ToString(),
            AssetContainer = _buildContext.AssetContainer,
            ContainerMode = AacConfiguration.Container.OnlyWhenPersistenceRequired,
            AssetContainerProvider = new NDMFContainerProvider(_buildContext),
            DefaultsProvider = new AacDefaultsProvider(UseWriteDefaults)
        });
    }

    internal class NDMFContainerProvider : IAacAssetContainerProvider
    {
        private readonly BuildContext _ctx;
        public NDMFContainerProvider(BuildContext ctx) => _ctx = ctx;
        public void SaveAsPersistenceRequired(Object objectToAdd) => _ctx.AssetSaver.SaveAsset(objectToAdd);
        public void SaveAsRegular(Object objectToAdd) { }
        public void ClearPreviousAssets() { }
    }
}
