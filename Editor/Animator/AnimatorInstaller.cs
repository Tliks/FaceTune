using UnityEditor.Animations;
using nadena.dev.ndmf.animator;
using nadena.dev.modular_avatar.core;
using com.aoyon.facetune.platform;
using com.aoyon.facetune.ndmf;

namespace com.aoyon.facetune.animator;

internal class AnimatorInstaller
{
    private readonly VirtualAnimatorController _virtualController;

    private readonly SessionContext _sessionContext;
    private readonly IPlatformSupport _platformSupport;

    private readonly bool _useWriteDefaults;
    private const float TransitionDurationSeconds = 0.1f; // 変更可能にすべき？

    private readonly Dictionary<Expression, VirtualClip> _expressionClipCache = new();
    private readonly Dictionary<AdvancedEyBlinkSettings, int> _advancedEyeBlinkIndex = new();
    private readonly VirtualClip _emptyClip;

    private static readonly Vector3 EntryStatePosition = new Vector3(50, 120, 0);
    private static readonly Vector3 DefaultStatePosition = new Vector3(300, 0, 0);
    private const float PositionXStep = 250;
    private const float PositionYStep = 50;
    
    private const int InitLayerPriority = -1; // 上書きを意図しない初期化レイヤー。
    private const int LayerPriority = 1; // FaceEmo: 0

    private const string TrueParameterName = $"{FaceTuneConsts.ParameterPrefix}/True";
    
    // LipSync
    private const string AllowLipSyncAAP = $"{FaceTuneConsts.ParameterPrefix}/AllowLipSyncAAP";

    // Blink
    private const string BlinkParameter = $"{FaceTuneConsts.ParameterPrefix}/Blink";
    private const string BlinkAllowAAP = $"{BlinkParameter}/Allow";
    private const string BlinkUseAnimationAAP = $"{BlinkParameter}/UseAnimation";
    private const string BlinkAnimationModeAAP = $"{BlinkParameter}/AnimationMode";
    private const string BlinkClosingAAP = $"{BlinkParameter}/Closing";
    private const string BlinkOpeningAAP = $"{BlinkParameter}/Opening";

    public AnimatorInstaller(SessionContext sessionContext, VirtualAnimatorController virtualController, bool useWriteDefaults)
    {
        _sessionContext = sessionContext;
        _virtualController = virtualController;
        _platformSupport = platform.PlatformSupport.GetSupport(_sessionContext.Root.transform);
        _useWriteDefaults = useWriteDefaults;
        _emptyClip = AnimatorHelper.CreateCustomEmpty();

        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Bool, TrueParameterName).defaultBool = true;
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, AllowLipSyncAAP);

        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, BlinkAllowAAP);
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, BlinkUseAnimationAAP);
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, BlinkAnimationModeAAP);
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, BlinkClosingAAP);
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, BlinkOpeningAAP);
    }

    private static VirtualLayer AddFTLayer(VirtualAnimatorController controller, string layerName, int priority)
    {
        var layerPriority = new LayerPriority(priority);
        var layer = controller.AddLayer(layerPriority, $"{FaceTuneConsts.ShortName}: {layerName}");
        layer.StateMachine!.EnsureBehavior<ModularAvatarMMDLayerControl>().DisableInMMDMode = true;
        return layer; 
    }

    private VirtualState AddFTState(VirtualLayer layer, string stateName, Vector3? position = null)
    {
        var state = layer.StateMachine!.AddState(stateName, position: position);
        state.WriteDefaultValues = _useWriteDefaults;
        return state;
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
            AddSettingsToState(state, clip, expression.ExpressionSettings);
            AddAnimationToState(clip, expression.Animations);
            SetFacialSettings(clip, expression.FacialSettings);
        }
    }

    private void AddSettingsToState(VirtualState state, VirtualClip clip, ExpressionSettings expressionSettings)
    {
        if (expressionSettings.LoopTime)
        {
            var settings = clip.Settings;
            settings.loopTime = true;
            clip.Settings = settings;
        }
        else if (!string.IsNullOrEmpty(expressionSettings.MotionTimeParameterName))
        {
            _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, expressionSettings.MotionTimeParameterName);
            state.TimeParameter = expressionSettings.MotionTimeParameterName;
        }
    }

    private void AddAnimationToState(VirtualState state, IEnumerable<GenericAnimation> animations)
    {
        var clip = state.GetOrCreateClip(state.Name);
        AddAnimationToState(clip, animations);
    }

    private void AddAnimationToState(VirtualClip clip, IEnumerable<GenericAnimation> animations)
    {
        clip.SetAnimations(animations);
    }

    // ブレンドシェイプ及びアニメーション用の初期化レイヤーを生成する
    // DisbaleExisingControlが無効な場合は既存の制御より低い優先度、有効な場合は高い優先度
    // 優先度が同じ場合はブレンドシェイプ用とアニメーション用のレイヤーを結合する(軽量化)。
    private void CreateDefaultLayer(bool overrideShapes, bool overrideProperties, PatternData patternData)
    {
        VirtualLayer defaultLayer;
        VirtualState defaultState;

        var shapesLayerPriority = overrideShapes ? LayerPriority : InitLayerPriority;
        var propertiesLayerPriority = overrideProperties ? LayerPriority : InitLayerPriority;

        // ブレンドシェイプの初期化レイヤー
        var shapesLayer = AddFTLayer(_virtualController, "Default", shapesLayerPriority);
        var shapesState = AddFTState(shapesLayer, "Default", DefaultStatePosition);
        AddAnimationToState(shapesState, _sessionContext.ZeroWeightBlendShapes.ToGenericAnimations(_sessionContext.BodyPath));

        var bindings = patternData.GetAllExpressions().SelectMany(e => e.Animations).Select(a => a.CurveBinding).Distinct().ToList();

        var facialBinding = SerializableCurveBinding.FloatCurve(_sessionContext.BodyPath, typeof(SkinnedMeshRenderer), FaceTuneConsts.AnimatedBlendShapePrefix);
        var nonFacialBindings = bindings.Where(b => b != facialBinding);
        if (!nonFacialBindings.Any()) return;

        var defaultPropertiesAnimations = AnimatorHelper.GetDefaultValueAnimations(_sessionContext.Root, nonFacialBindings);
        if (!defaultPropertiesAnimations.Any()) return;

        if (shapesLayerPriority == propertiesLayerPriority)
        {
            AddAnimationToState(shapesState, defaultPropertiesAnimations);
        }
        else
        {
            var propertiesLayer = AddFTLayer(_virtualController, "Default", propertiesLayerPriority);
            var propertiesState = AddFTState(propertiesLayer, "Default", DefaultStatePosition);
            AddAnimationToState(propertiesState, defaultPropertiesAnimations);
        }

        defaultLayer = shapesLayer;
        defaultState = shapesState;
    }

    public void DisableExistingControlAndInstallPatternData(InstallData installData)
    {
        var patternData = installData.PatternData;
        if (patternData.IsEmpty) return;

        CreateDefaultLayer(installData.OverrideBlendShapes, installData.OverrideProperties, patternData);


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
            || e.FacialSettings.AdvancedEyBlinkSettings.UseAdvancedEyBlink))
        {
            AddEyeBlinkLayer();
        }
        if (patternExpressions.Any(e => e.FacialSettings.AllowLipSync == TrackingPermission.Disallow))
        {
            AddLipSyncLayer();
        }
    }

    private void AsPassThrough(VirtualState state)
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
                    layers[i] = AddFTLayer(_virtualController, $"Preset Pattern Group {i}", priority); 
                    defaultStates[i] = AddFTState(layers[i], "PassThrough", DefaultStatePosition);
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

            var layer = AddFTLayer(_virtualController, singleExpressionPattern.Name, priority);
            var defaultState = AddFTState(layer, "PassThrough", DefaultStatePosition);
            AsPassThrough(defaultState);
            var basePosition = DefaultStatePosition + new Vector3(0, 2 * PositionYStep, 0);
            AddExpressionWithConditions(layer, defaultState, singleExpressionPattern.ExpressionPattern.ExpressionWithConditions, basePosition); 
        }
    }

    private void AddExpressionWithConditions(VirtualLayer layer, VirtualState defaultState, IEnumerable<ExpressionWithConditions> expressionWithConditions, Vector3 basePosition)
    {
        var trueCondition = new[] { ParameterCondition.Bool(TrueParameterName, true) };
        var expressionWithConditionList = expressionWithConditions.Select(e => 
        {
            if (!e.Conditions.Any())
            {
                e.SetConditions(e.Conditions.Concat(trueCondition).ToList());
            }
            return e;
        }).ToList();
        var duration = TransitionDurationSeconds;
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

            var state = AddFTState(layer, "unnamed", position);
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
        _virtualController.EnsureParameterExists(parameterType, parameter);
        return animatorCondition;
    }

    private void SetFacialSettings(VirtualClip clip, FacialSettings? facialSettings)
    {
        if (facialSettings == null) return;

        // EyeBlink
        if (facialSettings.AllowEyeBlink != TrackingPermission.Keep)
        {
            // Allow
            var allowBlinkCurve = new AnimationCurve();
            var value = facialSettings.AllowEyeBlink == TrackingPermission.Allow ? 1 : 0;
            allowBlinkCurve.AddKey(0, value);
            clip.SetFloatCurve("", typeof(Animator), BlinkAllowAAP, allowBlinkCurve);

            var advancedSettings = facialSettings.AdvancedEyBlinkSettings;
            if (advancedSettings.UseAdvancedEyBlink && advancedSettings.UseAnimation)
            {
                // UseAnimation
                var useAnimationCurve = new AnimationCurve();
                useAnimationCurve.AddKey(0, 1);
                clip.SetFloatCurve("", typeof(Animator), BlinkUseAnimationAAP, useAnimationCurve);

                // AnimationMode
                var index = GetAdvancedEyeBlinkIndex(advancedSettings);
                var animationModeCurve = new AnimationCurve();
                animationModeCurve.AddKey(0, VRCAAPHelper.IndexToValue(index));
                clip.SetFloatCurve("", typeof(Animator), BlinkAnimationModeAAP, animationModeCurve);
            }
        }

        // LipSync
        if (facialSettings.AllowLipSync != TrackingPermission.Keep)
        {
            var curve = new AnimationCurve();
            var value = facialSettings.AllowLipSync == TrackingPermission.Allow ? 1 : 0;
            curve.AddKey(0, value);
            clip.SetFloatCurve("", typeof(Animator), AllowLipSyncAAP, curve);
        }
    }

    private int GetAdvancedEyeBlinkIndex(AdvancedEyBlinkSettings advancedEyeBlinkSettings)
    {
        if (_advancedEyeBlinkIndex.TryGetValue(advancedEyeBlinkSettings, out var index))
        {
            return index;
        }
        else
        {
            index = _advancedEyeBlinkIndex.Count;
            _advancedEyeBlinkIndex[advancedEyeBlinkSettings] = index;
        }
        return index;
    }

    private void AddEyeBlinkLayer()
    {
        var eyeBlinkLayer = AddFTLayer(_virtualController, "EyeBlink", LayerPriority);

        // 最初のフレームでTraking Controlを変更すると巻き戻される。
        // 2フレームの遅延が必要らしいので多めに0.1s程度遅延させる
        var delayState = AddFTState(eyeBlinkLayer, "Delay", EntryStatePosition + new Vector3(-20, 2 * PositionYStep, 0));
        var delayClip = AnimatorHelper.CreateDelayClip(0.1f);
        delayState.Motion = delayClip;

        // DefaultのTracking/Animationの入れ替え
        var enabledPosition = EntryStatePosition + new Vector3(PositionXStep, 0, 0);
        var enabled = AddFTState(eyeBlinkLayer, "Default: Enabled", enabledPosition);
        var disabled = AddFTState(eyeBlinkLayer, "Default: Disabled", enabledPosition + new Vector3(0, 2 * PositionYStep, 0));
        
        enabled.Motion = _emptyClip;
        disabled.Motion = _emptyClip;
        _platformSupport.SetEyeBlinkTrack(enabled, true);
        _platformSupport.SetEyeBlinkTrack(disabled, false);

        // Delay -> Enabled
        var delayToEnabled = AnimatorHelper.CreateTransitionWithExitTime(1f, 0f);
        delayToEnabled.SetDestination(enabled);
        delayState.Transitions = delayState.Transitions.Add(delayToEnabled);

        // Enabled -> Disabled
        var enabledToDisabled = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        enabledToDisabled.SetDestination(disabled);
        enabledToDisabled.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = BlinkAllowAAP,
            mode = AnimatorConditionMode.Less,
            threshold = 0.99f
        });
        enabled.Transitions = enabled.Transitions.Add(enabledToDisabled);

        // Disabled -> Enabled
        var disabledToEnabled = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        disabledToEnabled.SetDestination(enabled);
        disabledToEnabled.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = BlinkAllowAAP,
            mode = AnimatorConditionMode.Greater,
            threshold = 0.99f
        });
        disabled.Transitions = disabled.Transitions.Add(disabledToEnabled);
    
        // AnimationGate
        var disableTrackingPosition = enabledPosition + new Vector3(PositionXStep, 0, 0);
        var disableTracking = AddFTState(eyeBlinkLayer, "DisableTracking", disableTrackingPosition);
        var animationGatePosition = disableTrackingPosition + new Vector3(0, 2 * PositionYStep, 0);
        var animationGate = AddFTState(eyeBlinkLayer, "AnimationGate", animationGatePosition);

        disableTracking.Motion = _emptyClip;
        animationGate.Motion = _emptyClip;
        _platformSupport.SetEyeBlinkTrack(disableTracking, false);

        // Disabled -> AnimationGate
        var disabledToAnimationGate = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        disabledToAnimationGate.SetDestination(animationGate);
        disabledToAnimationGate.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = BlinkUseAnimationAAP,
            mode = AnimatorConditionMode.Greater,
            threshold = 0f
        });
        disabled.Transitions = disabled.Transitions.Add(disabledToAnimationGate);

        // AnimationGate -> Disabled
        var animationGateToDsiabled = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        animationGateToDsiabled.SetDestination(disabled);
        animationGateToDsiabled.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = BlinkUseAnimationAAP,
            mode = AnimatorConditionMode.Less,
            threshold = 1f
        });
        animationGate.Transitions = animationGate.Transitions.Add(animationGateToDsiabled);

        // Enabled -> DisableTracking
        var enabledToDisableTracking = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        enabledToDisableTracking.SetDestination(disableTracking);
        enabledToDisableTracking.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = BlinkUseAnimationAAP,
            mode = AnimatorConditionMode.Greater,
            threshold = 0f
        });
        enabled.Transitions = enabled.Transitions.Add(enabledToDisableTracking);

        // DisableTracking -> AnimationGate
        var disableTrackingToAnimationGate = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        disableTrackingToAnimationGate.SetDestination(animationGate);
        disableTrackingToAnimationGate.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = TrueParameterName,
            mode = AnimatorConditionMode.If
        });
        disableTracking.Transitions = disableTracking.Transitions.Add(disableTrackingToAnimationGate);

        // Animationを用いた瞬き制御
        var starePosition = animationGatePosition + new Vector3(PositionXStep, 0, 0);
        foreach (var advancedSettings in _advancedEyeBlinkIndex.OrderBy(kvp => kvp.Value))
        {
            var index = advancedSettings.Value;

            var stare = AddFTState(eyeBlinkLayer, $"Stare {index}", starePosition);
            var close = AddFTState(eyeBlinkLayer, $"Close {index}", starePosition + new Vector3(PositionXStep, 0, 0));
            var open = AddFTState(eyeBlinkLayer, $"Open {index}", starePosition + new Vector3(PositionXStep, 2 * PositionYStep, 0));

            // AnimationGate -> Stare
            var gateToStare = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
            gateToStare.SetDestination(stare);
            // floatでequalsは使えないので2
            if (index > 0)
            {
                gateToStare.Conditions = ImmutableList.Create(
                    new AnimatorCondition()
                    {
                        parameter = BlinkAnimationModeAAP,
                        mode = AnimatorConditionMode.Greater,
                        threshold = VRCAAPHelper.IndexToValue(index - 1)
                    }
                );
            }
            if (index < 255)
            {
                gateToStare.Conditions = gateToStare.Conditions.Add(
                    new AnimatorCondition()
                    {
                        parameter = BlinkAnimationModeAAP,
                        mode = AnimatorConditionMode.Less,
                        threshold = VRCAAPHelper.IndexToValue(index + 1)
                    }
                );
            }
            animationGate.Transitions = animationGate.Transitions.Add(gateToStare);

            // Stare -> AnimationGate (OR)
            var conditions = new List<AnimatorCondition>
            {
                new AnimatorCondition
                {
                    parameter = BlinkAnimationModeAAP,
                    mode = AnimatorConditionMode.Greater,
                    threshold = VRCAAPHelper.IndexToValue(index)
                },
                new AnimatorCondition
                {
                    parameter = BlinkAnimationModeAAP,
                    mode = AnimatorConditionMode.Less,
                    threshold = VRCAAPHelper.IndexToValue(index)
                }
            };
            foreach (var condition in conditions)
            {
                var stareToGate = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
                stareToGate.SetDestination(animationGate);
                stareToGate.Conditions = ImmutableList.Create(condition);
                stare.Transitions = stare.Transitions.Add(stareToGate);
            }

            // Stare -> Close
            var stareToClose = AnimatorHelper.CreateTransitionWithExitTime();
            stareToClose.SetDestination(close);
            stare.Transitions = stare.Transitions.Add(stareToClose);

            // Close -> Open
            var closeToOpen = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
            closeToOpen.SetDestination(open);
            closeToOpen.Conditions = ImmutableList.Create(new AnimatorCondition()
            {
                parameter = TrueParameterName,
                mode = AnimatorConditionMode.If
            });
            close.Transitions = close.Transitions.Add(closeToOpen);

            // Open -> Stare
            var openToStare = AnimatorHelper.CreateTransitionWithExitTime();
            openToStare.SetDestination(stare);
            open.Transitions = open.Transitions.Add(openToStare);

            starePosition.y += 3 * PositionYStep;
        }
    }
    
    private void AddLipSyncLayer()
    {
        var lipSyncLayer = AddFTLayer(_virtualController, "LipSync", LayerPriority);

        var delayState = AddFTState(lipSyncLayer, "Delay", EntryStatePosition + new Vector3(-20, 2 * PositionYStep, 0));
        var delayClip = AnimatorHelper.CreateDelayClip(0.2f);
        delayState.Motion = delayClip;

        var position = DefaultStatePosition;
        var enabled = AddFTState(lipSyncLayer, "Enabled", position);
        position.y += 2 * PositionYStep;
        var disabled = AddFTState(lipSyncLayer, "Disabled", position);

        _platformSupport.SetLipSyncTrack(enabled, true);
        _platformSupport.SetLipSyncTrack(disabled, false);

        var delayTransition = AnimatorHelper.CreateTransitionWithExitTime(1f, 0f);
        delayTransition.SetDestination(enabled);
        delayState.Transitions = ImmutableList.Create(delayTransition);

        var enabledTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        enabledTransition.SetDestination(enabled);
        enabledTransition.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = AllowLipSyncAAP,
            mode = AnimatorConditionMode.Greater,
            threshold = 0.99f
        });
        disabled.Transitions = ImmutableList.Create(enabledTransition);

        var disabledTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        disabledTransition.SetDestination(disabled);
        disabledTransition.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = AllowLipSyncAAP,
            mode = AnimatorConditionMode.Less,
            threshold = 0.99f
        });
        enabled.Transitions = ImmutableList.Create(disabledTransition);
    }
}