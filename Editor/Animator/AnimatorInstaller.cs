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

        var defaultClip = _aac.NewClip();
        AddBlendShapes(defaultClip, _sessionContext.FaceRenderer, _sessionContext.DefaultBlendShapes);

        defaultLayer.NewState("Default").WithAnimation(defaultClip);
    }

    public void SaveAsMergeAnimator()
    {
        var modularAvatar = MaAc.Create(new GameObject(SystemName)
            {
            transform = { parent = _sessionContext.Root.transform }
        });
        modularAvatar.NewMergeAnimator(_ctrl, VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX);
    }

    private void AddBlendShapes(AacFlClip clip, SkinnedMeshRenderer renderer, IEnumerable<BlendShape> blendShapes)
    {
        foreach (var blendShape in blendShapes)
        {
            clip.BlendShape(renderer, blendShape.Name, blendShape.Weight);
        }
    }

    public void InstallPreset(List<Preset> presets)
    {
        var preset = presets[0]; // とりあえず先頭のみ
        var info = preset.Info;
        var patterns = preset.SortedExpressionPatterns.GetPatternsInPriorityOrder();

        for (var i = 0; i < patterns.Count; i++)
        {
            var pattern = patterns[i];
            var layer = _ctrl.NewLayer($"{info.PresetName}_{i}");

            var init = layer.NewState("Init");
            SetTracks(init, _sessionContext.DefaultExpression);

            foreach (var expressionWithCondition in pattern.Expressions)
            {
                var expressions = expressionWithCondition.Expressions;

                var facialExpressions = expressions.OfType<FacialExpression>();
                var animationExpressions = expressions.OfType<AnimationExpression>();

                // 一旦FacialExpressionのみ
                var shapes = GetBlendShapeSet(facialExpressions.ToList());
                var clip = _aac.NewClip();
                AddBlendShapes(clip, _sessionContext.FaceRenderer, shapes.BlendShapes);

                var name = expressions.First().Name;
                var expressionState = layer.NewState(name).WithAnimation(clip);
                SetTracks(expressionState, facialExpressions.Last());

                var conditions = init.TransitionsTo(expressionState).WithTransitionDurationSeconds(0.1f).WhenConditions();
                WithConditions(conditions, expressionWithCondition.Conditions, layer.Av3(), true);
                var exits = expressionState.Exits().WithTransitionDurationSeconds(0.1f).WhenConditions();
                WithConditions(exits, expressionWithCondition.Conditions, layer.Av3(), false);
            }
            init.At(0, 5);
        }
    }

    private void SetTracks(AacFlState state, FacialExpression expression)
    {
        SetTracks(state, AacAv3.Av3TrackingElement.Eyes, expression.AllowEyeBlink);
        SetTracks(state, AacAv3.Av3TrackingElement.Mouth, expression.AllowLipSync);
    }

    private void SetTracks(AacFlState state, AacAv3.Av3TrackingElement element, bool allow)
    {
        if (allow)
        {
            state.TrackingTracks(element);
        }
        else
        {
            state.TrackingAnimates(element);
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

    private void WithConditions(AacFlTransitionContinuation aacContinuation, List<Condition> ftConditions, AacAv3 av3, bool isEqualTo)
    {
        foreach (var ftCondition in ftConditions)
        {
            if (ftCondition.Type != ConditionType.HandGesture) continue;
            if (ftCondition.HandGestureCondition.Hand == Hand.Left)
            {
                if (isEqualTo)
                {
                    aacContinuation.And(av3.GestureLeft.IsEqualTo(Convert(ftCondition.HandGestureCondition.HandGesture)));
                }
                else
                {
                    aacContinuation.And(av3.GestureLeft.IsNotEqualTo(Convert(ftCondition.HandGestureCondition.HandGesture)));
                }
            }
            else
            {
                if (isEqualTo)
                {
                    aacContinuation.And(av3.GestureRight.IsEqualTo(Convert(ftCondition.HandGestureCondition.HandGesture)));
                }
                else
                {
                    aacContinuation.And(av3.GestureRight.IsNotEqualTo(Convert(ftCondition.HandGestureCondition.HandGesture)));
                }
            }
        }
    }

    private BlendShapeSet GetBlendShapeSet(List<FacialExpression> expressions)
    {
        var shapes = new BlendShapeSet();
        foreach (var expression in expressions) shapes.Merge(expression.BlendShapes);
        return shapes;
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
