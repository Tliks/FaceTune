
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Animator;

internal class BlinkInstaller : InstallerBase
{
    private bool _shouldAddLayer = false;

    private readonly Dictionary<AdvancedEyeBlinkSettings, int> _advancedEyeBlinkIndex = new();
    
    private Dictionary<string, string> _clonedShapesMapping = new();

    private const string ParameterPrefix = $"{FaceTuneConsts.ParameterPrefix}/Blink";
    private const string AllowAAP = $"{ParameterPrefix}/Allow"; // 常に追加
    private const string UseAnimationAAP = $"{ParameterPrefix}/UseAnimation"; // 1つ以上有効なAdvancedEyeBlinkSettingsがあるとき
    private const string ModeAAP = $"{ParameterPrefix}/Mode"; // 同上
    private const string DelayMultiplier = $"{ParameterPrefix}/DelayMultiplier"; // 同上

    public BlinkInstaller(VirtualAnimatorController virtualController, SessionContext sessionContext, bool useWriteDefaults) : base(virtualController, sessionContext, useWriteDefaults)
    {
        _controller.EnsureParameterExists(AnimatorControllerParameterType.Float, AllowAAP);
    }

    public void SetSettings(VirtualClip clip, FacialSettings facialSettings)
    {
        if (facialSettings.AllowEyeBlink != TrackingPermission.Keep)
        {
            _shouldAddLayer = true;
            
            // Allow
            var allowBlinkCurve = new AnimationCurve();
            var value = facialSettings.AllowEyeBlink == TrackingPermission.Allow ? 1 : 0;
            allowBlinkCurve.AddKey(0, value);
            clip.SetFloatCurve("", typeof(UnityEngine.Animator), AllowAAP, allowBlinkCurve);
        }

        var advancedSettings = facialSettings.AdvancedEyBlinkSettings;
        if (advancedSettings.IsAnimationEnabled())
        {
            _shouldAddLayer = true;

            _controller.EnsureParameterExists(AnimatorControllerParameterType.Float, UseAnimationAAP);
            _controller.EnsureParameterExists(AnimatorControllerParameterType.Float, ModeAAP);
            _controller.EnsureParameterExists(AnimatorControllerParameterType.Float, DelayMultiplier);
            
            // UseAnimation
            var useAnimationCurve = new AnimationCurve();
            useAnimationCurve.AddKey(0, 1);
            clip.SetFloatCurve("", typeof(UnityEngine.Animator), UseAnimationAAP, useAnimationCurve);

            // AnimationMode
            var index = GetIndexForSettings(advancedSettings);
            var animationModeCurve = new AnimationCurve();
            animationModeCurve.AddKey(0, VRCAAPHelper.IndexToValue(index));
            clip.SetFloatCurve("", typeof(UnityEngine.Animator), ModeAAP, animationModeCurve);
        }
    }

    private int GetIndexForSettings(AdvancedEyeBlinkSettings advancedSettings)
    {
        return _advancedEyeBlinkIndex.GetOrAdd(advancedSettings, _advancedEyeBlinkIndex.Count);
    }

    public void AddEyeBlinkLayer()
    {
        if (!_shouldAddLayer) return;

        var eyeBlinkLayer = AddLayer("EyeBlink", LayerPriority);

        // 最初のフレームでTraking Controlを変更すると巻き戻される。
        // 2フレームの遅延が必要らしいので多めに0.1s程度遅延させる
        var delayState = AddState(eyeBlinkLayer, "Delay", EntryStatePosition + new Vector3(-20, 2 * PositionYStep, 0));
        var delayClip = AnimatorHelper.CreateDelayClip(0.1f);
        delayState.Motion = delayClip;

        // DefaultのTracking/Animationの入れ替え
        var enabledPosition = EntryStatePosition + new Vector3(PositionXStep, 0, 0);
        var enabled = AddState(eyeBlinkLayer, "Default: Enabled", enabledPosition);
        var disabled = AddState(eyeBlinkLayer, "Default: Disabled", enabledPosition + new Vector3(0, 2 * PositionYStep, 0));
        
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
            parameter = AllowAAP,
            mode = AnimatorConditionMode.Less,
            threshold = 0.99f // 安全側(Stare)に倒す
        });
        enabled.Transitions = enabled.Transitions.Add(enabledToDisabled);

        // Disabled -> Enabled
        var disabledToEnabled = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        disabledToEnabled.SetDestination(enabled);
        disabledToEnabled.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = AllowAAP,
            mode = AnimatorConditionMode.Greater,
            threshold = 0.99f // 同上
        });
        disabled.Transitions = disabled.Transitions.Add(disabledToEnabled);

        if (_advancedEyeBlinkIndex.Count == 0 || _advancedEyeBlinkIndex.Keys.All(k => !k.IsAnimationEnabled())) return;

        AddAnimationStates(eyeBlinkLayer, enabled, disabled, enabledPosition);
    }

    private void AddAnimationStates(VirtualLayer layer, VirtualState enabled, VirtualState disabled, Vector3 enabledPosition)
    {
        var (animationGate, animationGatePosition) = CreateAnimationGate(layer, enabled, disabled, enabledPosition);
        CloneShapesForBlinking();
        AddBlinkingStates(layer, animationGate, animationGatePosition);
    }

    private (VirtualState, Vector3) CreateAnimationGate(VirtualLayer layer, VirtualState enabled, VirtualState disabled, Vector3 enabledPosition)
    {
        // AnimationGate
        var disableTrackingPosition = enabledPosition + new Vector3(PositionXStep, 0, 0);
        var disableTracking = AddState(layer, "DisableTracking", disableTrackingPosition);
        var animationGatePosition = disableTrackingPosition + new Vector3(0, 2 * PositionYStep, 0);
        var animationGate = AddState(layer, "AnimationGate", animationGatePosition);

        disableTracking.Motion = _emptyClip;
        animationGate.Motion = _emptyClip;
        _platformSupport.SetEyeBlinkTrack(disableTracking, false);

        // Disabled -> AnimationGate
        var disabledToAnimationGate = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        disabledToAnimationGate.SetDestination(animationGate);
        disabledToAnimationGate.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = UseAnimationAAP,
            mode = AnimatorConditionMode.Greater,
            threshold = 0.99f // 完全に遷移してから切り替える
        });
        disabled.Transitions = disabled.Transitions.Add(disabledToAnimationGate);

        // AnimationGate -> Disabled
        var animationGateToDsiabled = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        animationGateToDsiabled.SetDestination(disabled);
        animationGateToDsiabled.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = UseAnimationAAP,
            mode = AnimatorConditionMode.Less,
            threshold = 0.01f // 同上
        });
        animationGate.Transitions = animationGate.Transitions.Add(animationGateToDsiabled);

        // Enabled -> DisableTracking
        var enabledToDisableTracking = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        enabledToDisableTracking.SetDestination(disableTracking);
        enabledToDisableTracking.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = UseAnimationAAP,
            mode = AnimatorConditionMode.Greater,
            threshold = 0.99f // 同上
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

        return (animationGate, animationGatePosition);
    }

    private void CloneShapesForBlinking()
    {
        // 瞬き用のブレンドシェイプを複製する
        // Trackingとの競合を避けるためと、他表情で使用されている場合にそれをリセットせず移動量の追加のみ行うようにするため。
        // キャンセラーはリセットを意図する(上のレイヤーの影響を受ける)ため複製しない。

        var blinkShapeNames = _advancedEyeBlinkIndex.Keys.SelectMany(s => s.BlinkBlendShapeNames).ToHashSet();

        Action<Mesh, Mesh> onClone = (Mesh o, Mesh n) => ObjectRegistry.RegisterReplacedObject(o, n);
        Action<string> onNotFound = (string name) => { Debug.LogError($"Shape not found: {name}"); };
        _clonedShapesMapping = MeshHelper.CloneShapes(_sessionContext.FaceRenderer, blinkShapeNames, onClone, onNotFound, "_clone.blink");
        foreach (var (settings, index) in _advancedEyeBlinkIndex.ToList())
        {
            var newSettings = settings.GetRenamed(_clonedShapesMapping);
            _advancedEyeBlinkIndex.Remove(settings);
            _advancedEyeBlinkIndex[newSettings] = index;
        }
    }

    private void AddBlinkingStates(VirtualLayer layer, VirtualState animationGate, Vector3 animationGatePosition)
    {
        var starePosition = animationGatePosition + new Vector3(PositionXStep, 0, 0);
        foreach (var (settings, index) in _advancedEyeBlinkIndex.OrderBy(kvp => kvp.Value))
        {
            var stare = AddState(layer, $"Stare {index}", starePosition);
            var entryPassThrough = AddState(layer, $"Entry PassThrough {index}", starePosition + new Vector3(PositionXStep, 0, 0));
            var blink = AddState(layer, $"Blink {index}", starePosition + new Vector3(PositionXStep, 2 * PositionYStep, 0));
            var exitPassThrough = AddState(layer, $"Exit PassThrough {index}", starePosition + new Vector3(0, 2 * PositionYStep, 0));

            // 瞬きの間隔を設定
            if (!settings.UseRandomInterval)
            {
                var blinkDelayClip = AnimatorHelper.CreateDelayClip(settings.IntervalSeconds, $"BlinkDelay {index}");
                stare.Motion = blinkDelayClip;
            }
            else
            {
                var blinkDelayClip = AnimatorHelper.CreateDelayClip(settings.RandomIntervalMinSeconds, $"BlinkDelay {index}");
                stare.Motion = blinkDelayClip;

                var maxMultiplier = 1f;
                var minMultiplier = settings.RandomIntervalMinSeconds / settings.RandomIntervalMaxSeconds;
                _platformSupport.StateAsRandrom(stare, DelayMultiplier, minMultiplier, maxMultiplier);
                stare.SpeedParameter = DelayMultiplier;
            }

            AsPassThrough(entryPassThrough);
            AsPassThrough(exitPassThrough);

            // 目を閉じたときの表情を設定 
            var blinkAnimations = new List<GenericAnimation>();
            var holdDuration = settings.HoldDurationSeconds;
            var bodyPath = _sessionContext.BodyPath;
            if (holdDuration < 0.01f) holdDuration = 0.01f; // 0fにするとExitTimeで遷移が直ぐに行われない
            foreach (var name in settings.BlinkBlendShapeNames)
            {
                var curve = new AnimationCurve();
                curve.AddKey(0, 100);
                curve.AddKey(holdDuration, 100);
                blinkAnimations.Add(new BlendShapeAnimation(name, curve).ToGeneric(bodyPath));
            }
            if (settings.IsCancelerEnabled())
            {
                foreach (var name in settings.CancelerBlendShapeNames)
                {
                    var curve = new AnimationCurve();
                    curve.AddKey(0, 0);
                    curve.AddKey(holdDuration, 0);
                    blinkAnimations.Add(new BlendShapeAnimation(name, curve).ToGeneric(bodyPath));
                }
            }
            AddAnimationToState(blink, blinkAnimations);

            // AnimationGate -> Stare
            var gateToStare = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
            gateToStare.SetDestination(stare);
            gateToStare.Conditions = ImmutableList.CreateRange(VRCAAPHelper.IndexConditions(ModeAAP, true, index));
            animationGate.Transitions = animationGate.Transitions.Add(gateToStare);

            // Stare -> AnimationGate (OR)
            var stareToGate = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
            stareToGate.SetDestination(animationGate);
            var orTransitions = AnimatorHelper.SetORConditions(stareToGate, VRCAAPHelper.IndexConditions(ModeAAP, false, index));
            stare.Transitions = stare.Transitions.AddRange(orTransitions);

            // stare -> entryPassThrough
            var stareToEntryPassThrough = AnimatorHelper.CreateTransitionWithExitTime(); // StareのDelayAnimationの再生が終わったタイミングで瞬きを発火
            stareToEntryPassThrough.SetDestination(entryPassThrough);
            stareToEntryPassThrough.Conditions = ImmutableList.Create(new AnimatorCondition()
            {
                parameter = AllowAAP,
                mode = AnimatorConditionMode.Greater,
                threshold = 0.99f // 安全側(Stare)に倒す
            });
            stare.Transitions = stare.Transitions.Add(stareToEntryPassThrough);

            // entryPassThrough -> blink
            var entryPassThroughToBlink = AnimatorHelper.CreateTransitionWithDurationSeconds(settings.ClosingDurationSeconds);
            entryPassThroughToBlink.SetDestination(blink);
            entryPassThroughToBlink.Conditions = ImmutableList.Create(new AnimatorCondition()
            {
                parameter = TrueParameterName,
                mode = AnimatorConditionMode.If
            });
            entryPassThrough.Transitions = entryPassThrough.Transitions.Add(entryPassThroughToBlink);

            // blink -> exitPassThrough
            var blinkToExitPassThrough = AnimatorHelper.CreateTransitionWithExitTime(1f, settings.OpeningDurationSeconds);
            blinkToExitPassThrough.SetDestination(exitPassThrough);
            blinkToExitPassThrough.Conditions = ImmutableList.Create(new AnimatorCondition()
            {
                parameter = TrueParameterName,
                mode = AnimatorConditionMode.If
            });
            blink.Transitions = blink.Transitions.Add(blinkToExitPassThrough);

            // exitPassThrough -> stare
            var exitPassThroughToStare = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
            exitPassThroughToStare.SetDestination(stare);
            exitPassThroughToStare.Conditions = ImmutableList.Create(new AnimatorCondition()
            {
                parameter = TrueParameterName,
                mode = AnimatorConditionMode.If
            });
            exitPassThrough.Transitions = exitPassThrough.Transitions.Add(exitPassThroughToStare);

            starePosition.y += 3 * PositionYStep;
        }
    }

    public override void EditDefaultClip(VirtualClip clip)
    {
        var animations = _clonedShapesMapping.Values
            .Select(b => BlendShapeAnimation.SingleFrame(b, 0f))
            .Select(a => a.ToGeneric(_sessionContext.BodyPath));
        clip.SetAnimations(animations);
    }
}