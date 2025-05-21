using nadena.dev.ndmf;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.ModularAvatar;
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

    public void CreateDefaultLayer()
    {
        var defaultLayer = _ctrl.NewLayer("Default");
        var defaultState = defaultLayer.NewState("Default");
        AddExpressionsToState(defaultState, new[] { _sessionContext.DefaultExpression });
    }

    public void SaveAsMergeAnimator()
    {
        var modularAvatar = MaAc.Create(new GameObject(SystemName)
        {
            transform = { parent = _sessionContext.Root.transform }
        });
        modularAvatar.NewMergeAnimator(_ctrl, VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX);
    }

    private void AddExpressionsToState(AacFlState state, IEnumerable<Expression> expressions)
    {
        var facialExpressions = expressions.OfType<FacialExpression>();
        var animationExpressions = expressions.OfType<AnimationExpression>();
        
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
            foreach (var facialExpression in facialExpressions) shapes.Add(facialExpression.BlendShapes);
            foreach (var blendShape in shapes.BlendShapes)
            {
                clip.BlendShape(renderer, blendShape.Name, blendShape.Weight);
            }
            state.WithAnimation(clip);
        }

        foreach (var expression in expressions)
        {
            SetTracks(state, expression);
        }
    }

    public void InstallPreset(IEnumerable<Preset> presets)
    {
        var preset = presets.First(); // とりあえず先頭のみ
        var presetName = preset.PresetName;
        var patterns = preset.SortedPatterns.Patterns;

        for (var i = 0; i < patterns.Count(); i++)
        {
            var pattern = patterns[i];
            if (pattern == null || pattern.ExpressionWithConditions.Count == 0) continue;

            var layer = _ctrl.NewLayer($"{presetName}_{i}");

            var defaultState = layer.NewState("Default");
            var defaultToExitConditions = defaultState.Exits().WithTransitionDurationSeconds(0.1f).WhenConditions();
            
            foreach (var expressionWithCondition in pattern.ExpressionWithConditions)
            {
                var conditions = expressionWithCondition.Conditions;
                var expressions = expressionWithCondition.Expressions;

                var expressionState = layer.NewState(expressions.First().Name);

                AddExpressionsToState(expressionState, expressions);

                var entryConditions = expressionState.TransitionsFromEntry().WhenConditions();
                var exitConditions = expressionState.Exits().WithTransitionDurationSeconds(0.1f).WhenConditions();
                foreach (var condition in conditions)
                {
                    AddConditionToTransitions(condition, layer, entryConditions, exitConditions, defaultToExitConditions);
                }
            }
        }
    }

    private void AddConditionToTransitions(
        Condition condition,
        AacFlLayer layer,
        AacFlTransitionContinuation entryConditions,
        AacFlTransitionContinuation exitConditions,
        AacFlTransitionContinuation defaultToExitConditions)
    {
        switch (condition)
        {
            case HandGestureCondition handGestureCondition:
                var gesture = Convert(handGestureCondition.HandGesture);
                var isLeft = handGestureCondition.Hand == Hand.Left;
                var gestureParam = isLeft ? layer.Av3().GestureLeft : layer.Av3().GestureRight;
                AddComparisonCondition(entryConditions, gestureParam, handGestureCondition.ComparisonType, gesture);
                AddComparisonCondition(exitConditions, gestureParam, Negate(handGestureCondition.ComparisonType), gesture);
                AddComparisonCondition(defaultToExitConditions, gestureParam, handGestureCondition.ComparisonType, gesture, isOr: true);
                break;
            case ParameterCondition parameterCondition:
                switch (parameterCondition.ParameterType)
                {
                    case ParameterType.Int:
                        var intParam = layer.IntParameter(parameterCondition.ParameterName);
                        AddComparisonCondition(entryConditions, intParam, parameterCondition.ComparisonType, parameterCondition.IntValue);
                        AddComparisonCondition(exitConditions, intParam, Negate(parameterCondition.ComparisonType), parameterCondition.IntValue);
                        AddComparisonCondition(defaultToExitConditions, intParam, parameterCondition.ComparisonType, parameterCondition.IntValue, isOr: true);
                        break;
                    case ParameterType.Float:
                        var floatParam = layer.FloatParameter(parameterCondition.ParameterName);
                        AddComparisonCondition(entryConditions, floatParam, parameterCondition.ComparisonType, parameterCondition.FloatValue);
                        AddComparisonCondition(exitConditions, floatParam, Negate(parameterCondition.ComparisonType), parameterCondition.FloatValue);
                        AddComparisonCondition(defaultToExitConditions, floatParam, parameterCondition.ComparisonType, parameterCondition.FloatValue, isOr: true);
                        break;
                    case ParameterType.Bool:
                        var boolParam = layer.BoolParameter(parameterCondition.ParameterName);
                        AddComparisonCondition(entryConditions, boolParam, parameterCondition.BoolComparisonType, parameterCondition.BoolValue);
                        AddComparisonCondition(exitConditions, boolParam, Negate(parameterCondition.BoolComparisonType), parameterCondition.BoolValue);
                        AddComparisonCondition(defaultToExitConditions, boolParam, parameterCondition.BoolComparisonType, parameterCondition.BoolValue, isOr: true);
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
        ComparisonType comparisonType,
        float value,
        bool isOr = false)
    {
        switch (comparisonType)
        {
            case ComparisonType.GreaterThan:
                if (isOr) conditions.Or().When(parameter.IsGreaterThan(value));
                else conditions.And(parameter.IsGreaterThan(value));
                break;
            case ComparisonType.LessThan:
                if (isOr) conditions.Or().When(parameter.IsLessThan(value));
                else conditions.And(parameter.IsLessThan(value));
                break;
        }
    }

    private void AddComparisonCondition(
        AacFlTransitionContinuation conditions,
        AacFlIntParameter parameter,
        ComparisonType comparisonType,
        int value,
        bool isOr = false)
    {
        switch (comparisonType)
        {
            case ComparisonType.GreaterThan:
                if (isOr) conditions.Or().When(parameter.IsGreaterThan(value));
                else conditions.And(parameter.IsGreaterThan(value));
                break;
            case ComparisonType.LessThan:
                if (isOr) conditions.Or().When(parameter.IsLessThan(value));
                else conditions.And(parameter.IsLessThan(value));
                break;
        }
    }

    private void AddComparisonCondition(
        AacFlTransitionContinuation conditions,
        AacFlBoolParameter parameter,
        BoolComparisonType comparisonType,
        bool value,
        bool isOr = false)
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

    private ComparisonType Negate(ComparisonType type)
    {
        return type switch
        {
            ComparisonType.GreaterThan => ComparisonType.LessThan,
            ComparisonType.LessThan => ComparisonType.GreaterThan,
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
