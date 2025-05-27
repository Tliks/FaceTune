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
        var layer = _ctrl.NewLayer(layerName);
        layer.StateMachine.EnsureBehaviour<ModularAvatarMMDLayerControl>().DisableInMMDMode = true;
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

    private const float TransitionDurationSeconds = 0.1f; // 変更可能にすべき？
    public void InstallPatternData(PatternData patternData)
    {
        foreach (var patternGroup in patternData.GetConsecutiveTypeGroups())
        {
            var type = patternGroup.Type;

            if (type == typeof(Preset))
            {
                var presets = patternGroup.Group.Select(item => (Preset)item).ToList();

                var layers = new AacFlLayer[presets.Max(p => p.Patterns.Count)];
                var defaultStates = new AacFlState[presets.Max(p => p.Patterns.Count)];

                for (var i = 0; i < presets.Count; i++)
                {
                    var preset = presets[i];
                    // var presetName = preset.PresetName; // 未使用のためコメントアウト
                    var patterns = preset.Patterns;

                    for (var j = 0; j < patterns.Count; j++)
                    {
                        var pattern = patterns[j];
                        if (pattern == null || pattern.ExpressionWithConditions.Count == 0) continue;

                        var layer = layers[j];
                        if (layer == null)
                        {
                            layer = Addlayer($"Merged Presets Priority: {j}"); // レイヤー名はインデックス
                            layers[j] = layer;
                        }

                        var defaultState = defaultStates[j];
                        if (defaultState == null)
                        {
                            defaultState = layer.NewState("Default");
                            defaultStates[j] = defaultState;
                            SetTracks(defaultState, _sessionContext.DefaultExpression);
                        }
                                    
                        ProcessExpressionWithConditions(layer, defaultState, pattern.ExpressionWithConditions);
                        defaultState.At(0, 5); // Preset内の各パターンのDefaultステートの位置
                    }
                }
            }
            else if (type == typeof(ExpressionPattern))
            {
                var expressionPatterns = patternGroup.Group.Select(item => (ExpressionPattern)item).ToList();
                foreach (var expressionPattern in expressionPatterns)
                {
                    if (expressionPattern == null || expressionPattern.ExpressionWithConditions.Count == 0) continue;

                    var layer = Addlayer(expressionPattern.ExpressionWithConditions.First().Expressions.First().Name);
                    var defaultState = layer.NewState("Default");
                    SetTracks(defaultState, _sessionContext.DefaultExpression); 

                    ProcessExpressionWithConditions(layer, defaultState, expressionPattern.ExpressionWithConditions);
                    defaultState.At(0, 5); // ExpressionPatternのDefaultステートの位置
                }
            }
        }
    }

    private void ProcessExpressionWithConditions(AacFlLayer layer, AacFlState defaultState, IEnumerable<ExpressionWithCondition> expressionWithConditions)
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
            var defaultToExpressionConditions = defaultState.TransitionsTo(expressionState).WithTransitionDurationSeconds(TransitionDurationSeconds).WhenConditions();
            var expressionToExitConditions = expressionState.Exits().WithTransitionDurationSeconds(TransitionDurationSeconds).WhenConditions();
            
            if (conditions.Any())
            {
                // Process conditions for Entry and DefaultToExpression transitions (AND logic)
                AddConditionToTransitions(conditions.First(), layer, entryToExpressionConditions, defaultToExpressionConditions, null, true);
                foreach (var cond in conditions.Skip(1))
                {
                    AddConditionToTransitions(cond, layer, entryToExpressionConditions, defaultToExpressionConditions, null, false);
                }

                // Process conditions for Exit transitions (OR logic for negated conditions)
                AddConditionToTransitions(conditions.First(), layer, null, null, expressionToExitConditions, true);
                foreach (var cond in conditions.Skip(1))
                {
                    AddConditionToTransitions(cond, layer, null, null, expressionToExitConditions, false);
                }
            }
        }
    }

    private void AddConditionToTransitions(
        Condition condition,
        AacFlLayer layer,
        AacFlTransitionContinuation? entryToExpressionConditions,
        AacFlTransitionContinuation? defaultToExpressionConditions,
        AacFlTransitionContinuation? expressionToExitConditions,
        bool isFirstInLogicBlock)
    {
        switch (condition)
        {
            case HandGestureCondition handGestureCondition:
                var gesture = Convert(handGestureCondition.HandGesture);
                var isLeft = handGestureCondition.Hand == Hand.Left;
                var gestureParam = isLeft ? layer.Av3().GestureLeft : layer.Av3().GestureRight;

                if (entryToExpressionConditions != null)
                    AddComparisonCondition(entryToExpressionConditions, gestureParam, handGestureCondition.ComparisonType, gesture, isOr: false);
                if (defaultToExpressionConditions != null)
                    AddComparisonCondition(defaultToExpressionConditions, gestureParam, handGestureCondition.ComparisonType, gesture, isOr: false);
                
                if (expressionToExitConditions != null)
                    AddComparisonCondition(expressionToExitConditions, gestureParam, Negate(handGestureCondition.ComparisonType), gesture, isOr: !isFirstInLogicBlock);
                break;
            case ParameterCondition parameterCondition:
                switch (parameterCondition.ParameterType)
                {
                    case ParameterType.Int:
                        var intParam = layer.IntParameter(parameterCondition.ParameterName);
                        if (entryToExpressionConditions != null)
                            AddComparisonCondition(entryToExpressionConditions, intParam, parameterCondition.IntComparisonType, parameterCondition.IntValue, isOr: false);
                        if (defaultToExpressionConditions != null)
                            AddComparisonCondition(defaultToExpressionConditions, intParam, parameterCondition.IntComparisonType, parameterCondition.IntValue, isOr: false);
                        if (expressionToExitConditions != null)
                            AddComparisonCondition(expressionToExitConditions, intParam, Negate(parameterCondition.IntComparisonType), parameterCondition.IntValue, isOr: !isFirstInLogicBlock);
                        break;
                    case ParameterType.Float:
                        var floatParam = layer.FloatParameter(parameterCondition.ParameterName);
                        if (entryToExpressionConditions != null)
                            AddComparisonCondition(entryToExpressionConditions, floatParam, parameterCondition.FloatComparisonType, parameterCondition.FloatValue, isOr: false);
                        if (defaultToExpressionConditions != null)
                            AddComparisonCondition(defaultToExpressionConditions, floatParam, parameterCondition.FloatComparisonType, parameterCondition.FloatValue, isOr: false);
                        if (expressionToExitConditions != null)
                            AddComparisonCondition(expressionToExitConditions, floatParam, Negate(parameterCondition.FloatComparisonType), parameterCondition.FloatValue, isOr: !isFirstInLogicBlock);
                        break;
                    case ParameterType.Bool:
                        var boolParam = layer.BoolParameter(parameterCondition.ParameterName);
                        if (entryToExpressionConditions != null)
                            AddComparisonCondition(entryToExpressionConditions, boolParam, parameterCondition.BoolValue, isOr: false);
                        if (defaultToExpressionConditions != null)
                            AddComparisonCondition(defaultToExpressionConditions, boolParam, parameterCondition.BoolValue, isOr: false);
                        if (expressionToExitConditions != null)
                            AddComparisonCondition(expressionToExitConditions, boolParam, Negate(parameterCondition.BoolValue), isOr: !isFirstInLogicBlock);
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

    private IntComparisonType Negate(IntComparisonType type)
    {
        // Todo: インクリメントが必要
        return type switch
        {
            IntComparisonType.Equal => IntComparisonType.NotEqual,
            IntComparisonType.NotEqual => IntComparisonType.Equal,
            IntComparisonType.GreaterThan => IntComparisonType.LessThan,
            IntComparisonType.LessThan => IntComparisonType.GreaterThan,
            _ => type
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
