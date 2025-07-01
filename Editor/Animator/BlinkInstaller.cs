
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace aoyon.facetune.animator;

internal class BlinkInstaller : InstallerBase
{
    private readonly Dictionary<AdvancedEyeBlinkSettings, int> _advancedEyeBlinkIndex = new();
    
    private const string ParameterPrefix = $"{FaceTuneConsts.ParameterPrefix}/Blink";
    private const string AllowAAP = $"{ParameterPrefix}/Allow";
    private const string UseAnimationAAP = $"{ParameterPrefix}/UseAnimation";
    private const string ModeAAP = $"{ParameterPrefix}/Mode";
    private const string UseCancelerAAP = $"{ParameterPrefix}/UseCanceler";
    private const string BlinkingAAP = $"{ParameterPrefix}/Blinking";
    private const string DelayMultiplier = $"{ParameterPrefix}/DelayMultiplier";

    public BlinkInstaller(VirtualAnimatorController virtualController, SessionContext sessionContext, bool useWriteDefaults) : base(virtualController, sessionContext, useWriteDefaults)
    {
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, AllowAAP);
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, UseAnimationAAP);
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, ModeAAP);
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, UseCancelerAAP);
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, BlinkingAAP).defaultFloat = -1f; // -1: Stare, 0: Closing, 1: Opening;
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, DelayMultiplier);
    }

    public void SetSettings(VirtualClip clip, FacialSettings facialSettings)
    {
        if (facialSettings.AllowEyeBlink != TrackingPermission.Keep)
        {
            // Allow
            var allowBlinkCurve = new AnimationCurve();
            var value = facialSettings.AllowEyeBlink == TrackingPermission.Allow ? 1 : 0;
            allowBlinkCurve.AddKey(0, value);
            clip.SetFloatCurve("", typeof(Animator), AllowAAP, allowBlinkCurve);

            var advancedSettings = facialSettings.AdvancedEyBlinkSettings;
            if (advancedSettings.UseAdvancedEyeBlink && advancedSettings.UseAnimation)
            {
                // UseAnimation
                var useAnimationCurve = new AnimationCurve();
                useAnimationCurve.AddKey(0, 1);
                clip.SetFloatCurve("", typeof(Animator), UseAnimationAAP, useAnimationCurve);

                // AnimationMode
                var index = GetAdvancedEyeBlinkIndex(advancedSettings);
                var animationModeCurve = new AnimationCurve();
                animationModeCurve.AddKey(0, VRCAAPHelper.IndexToValue(index));
                clip.SetFloatCurve("", typeof(Animator), ModeAAP, animationModeCurve);

                // UseCanceler
                if (advancedSettings.IsCancelerEnabled())
                {
                    var useCancelerCurve = new AnimationCurve();
                    useCancelerCurve.AddKey(0, 1);
                    clip.SetFloatCurve("", typeof(Animator), UseCancelerAAP, useCancelerCurve);
                }
            }
        }
    }

    private int GetAdvancedEyeBlinkIndex(AdvancedEyeBlinkSettings advancedEyeBlinkSettings)
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

    public void AddEyeBlinkLayer()
    {
        VirtualLayer? cancelerLayer = null;
        VirtualLayer? eyeBlinkLayer = null;
        
        if (_advancedEyeBlinkIndex.Keys.Any(k => k.IsCancelerEnabled()))
        {
            cancelerLayer = AddFTLayer(_virtualController, "EyeBlink (Canceler)", LayerPriority);
        }
        eyeBlinkLayer = AddFTLayer(_virtualController, "EyeBlink", LayerPriority);

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
            parameter = AllowAAP,
            mode = AnimatorConditionMode.Less,
            threshold = 0.99f
        });
        enabled.Transitions = enabled.Transitions.Add(enabledToDisabled);

        // Disabled -> Enabled
        var disabledToEnabled = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        disabledToEnabled.SetDestination(enabled);
        disabledToEnabled.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = AllowAAP,
            mode = AnimatorConditionMode.Greater,
            threshold = 0.99f
        });
        disabled.Transitions = disabled.Transitions.Add(disabledToEnabled);

        if (_advancedEyeBlinkIndex.Count == 0 || _advancedEyeBlinkIndex.Keys.All(k => !k.IsEnabled())) return;
    
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
            parameter = UseAnimationAAP,
            mode = AnimatorConditionMode.Greater,
            threshold = 0f
        });
        disabled.Transitions = disabled.Transitions.Add(disabledToAnimationGate);

        // AnimationGate -> Disabled
        var animationGateToDsiabled = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        animationGateToDsiabled.SetDestination(disabled);
        animationGateToDsiabled.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = UseAnimationAAP,
            mode = AnimatorConditionMode.Less,
            threshold = 1f
        });
        animationGate.Transitions = animationGate.Transitions.Add(animationGateToDsiabled);

        // Enabled -> DisableTracking
        var enabledToDisableTracking = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        enabledToDisableTracking.SetDestination(disableTracking);
        enabledToDisableTracking.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = UseAnimationAAP,
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

        var closeShapeNames = _advancedEyeBlinkIndex.Keys.SelectMany(s => s.CloseAnimations).Select(a => a.Name).ToHashSet();
        var openShapeNames = _advancedEyeBlinkIndex.Keys.SelectMany(s => s.OpenAnimations).Select(a => a.Name).ToHashSet();
        var cancelerShapeNames = _advancedEyeBlinkIndex.Keys.SelectMany(s => s.CancelerBlendShapeNames).ToHashSet();

        // 瞬き用のブレンドシェイプを複製する
        // Trackingとの競合を避けるためと、他表情で使用されている場合にそれをリセットせず移動量の追加のみ行うようにするため。
        var shapesToClone = new HashSet<string>(closeShapeNames);
        shapesToClone.UnionWith(openShapeNames);
        Action<Mesh, Mesh> onClone = (Mesh o, Mesh n) => ObjectRegistry.RegisterReplacedObject(o, n);
        Action<string> onNotFound = (string name) => { Debug.LogError($"Shape not found: {name}"); };
        var mapping = MeshHelper.CloneShapes(_sessionContext.FaceRenderer, shapesToClone, onClone, onNotFound, "_clone.blink");
        foreach (var (settings, index) in _advancedEyeBlinkIndex.ToList())
        {
            var newSettings = settings.GetRenamed(mapping);
            _advancedEyeBlinkIndex.Remove(settings);
            _advancedEyeBlinkIndex[newSettings] = index;
        }

        var starePosition = animationGatePosition + new Vector3(PositionXStep, 0, 0);
        foreach (var (advancedSettings, index) in _advancedEyeBlinkIndex.OrderBy(kvp => kvp.Value))
        {
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
                        parameter = ModeAAP,
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
                        parameter = ModeAAP,
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
                    parameter = ModeAAP,
                    mode = AnimatorConditionMode.Greater,
                    threshold = VRCAAPHelper.IndexToValue(index)
                },
                new AnimatorCondition
                {
                    parameter = ModeAAP,
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
            stareToClose.Conditions = ImmutableList.Create(new AnimatorCondition()
            {
                parameter = AllowAAP,
                mode = AnimatorConditionMode.Greater,
                threshold = 0.99f
            });
            stare.Transitions = stare.Transitions.Add(stareToClose);

            // Close -> Stare
            var closeToStare = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
            closeToStare.SetDestination(stare);
            closeToStare.Conditions = ImmutableList.Create(new AnimatorCondition()
            {
                parameter = AllowAAP,
                mode = AnimatorConditionMode.Less,
                threshold = 0.99f
            });
            close.Transitions = close.Transitions.Add(closeToStare);

            // Close -> Open
            var closeToOpen = AnimatorHelper.CreateTransitionWithExitTime();
            closeToOpen.SetDestination(open);
            close.Transitions = close.Transitions.Add(closeToOpen);

            // Open -> Stare
            var openToStare1 = AnimatorHelper.CreateTransitionWithExitTime();
            openToStare1.SetDestination(stare);
            open.Transitions = open.Transitions.Add(openToStare1);

            var openToStare2 = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
            openToStare2.SetDestination(stare);
            openToStare2.Conditions = ImmutableList.Create(new AnimatorCondition()
            {
                parameter = AllowAAP,
                mode = AnimatorConditionMode.Less,
                threshold = 0.99f
            });
            open.Transitions = open.Transitions.Add(openToStare2);

            
            // 瞬きの間隔を設定
            if (!advancedSettings.UseRandomInterval)
            {
                var blinkDelayClip = AnimatorHelper.CreateDelayClip(advancedSettings.IntervalSeconds, $"BlinkDelay {index}");
                stare.Motion = blinkDelayClip;
            }
            else
            {
                var blinkDelayClip = AnimatorHelper.CreateDelayClip(advancedSettings.RandomIntervalMinSeconds, $"BlinkDelay {index}");
                stare.Motion = blinkDelayClip;

                var maxMultiplier = 1f;
                var minMultiplier = advancedSettings.RandomIntervalMinSeconds / advancedSettings.RandomIntervalMaxSeconds;
                stare.EnsureBehavior<VRCAvatarParameterDriver>().parameters.Add(new VRC_AvatarParameterDriver.Parameter()
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Random,
                    name = DelayMultiplier,
                    valueMin = minMultiplier,
                    valueMax = maxMultiplier,
                });
                stare.SpeedParameter = DelayMultiplier;
            }

            // 目を閉じるアニメーションとAAPを設定
            var closeClip = close.GetOrCreateClip($"Close {index}");
            var closeCurve = new AnimationCurve();
            closeCurve.AddKey(0f, 0f);
            closeClip.SetFloatCurve("", typeof(Animator), BlinkingAAP, closeCurve);
            AddAnimationToState(closeClip, advancedSettings.CloseAnimations.Select(a => a.ToGeneric(_sessionContext.BodyPath)));

            // 目を開くアニメーションとAAPを設定
            var openClip = open.GetOrCreateClip($"Open {index}");
            var openCurve = new AnimationCurve();
            openCurve.AddKey(0f, 1f);
            openClip.SetFloatCurve("", typeof(Animator), BlinkingAAP, openCurve);
            AddAnimationToState(openClip, advancedSettings.OpenAnimations.Select(a => a.ToGeneric(_sessionContext.BodyPath)));

            starePosition.y += 3 * PositionYStep;
        }

        if (cancelerLayer is null) return;

        var passThroughPosition = EntryStatePosition + new Vector3(PositionXStep, 0, 0);
        var passThrough = AddFTState(cancelerLayer, "PassThrough", passThroughPosition);
        AsPassThrough(passThrough);
        var position = passThroughPosition + new Vector3(PositionXStep, 0, 0);
        foreach (var (advancedSettings, index) in _advancedEyeBlinkIndex.OrderBy(kvp => kvp.Value))
        {
            if (!advancedSettings.IsCancelerEnabled()) continue;

            var canceler = AddFTState(cancelerLayer, $"Canceler {index}", position);
            var cancelerAnimation = advancedSettings.CancelerBlendShapeNames.Select(name => BlendShapeAnimation.SingleFrame(name, 0f).ToGeneric(_sessionContext.BodyPath));
            AddAnimationToState(canceler, cancelerAnimation);

            // passThrough -> canceler

            var closeDuration = advancedSettings.CloseAnimations.Select(a => a.Time).Max();
            var passThroughToCanceler = AnimatorHelper.CreateTransitionWithDurationSeconds(closeDuration);
            passThroughToCanceler.SetDestination(canceler);
            // cancelerが有効かつ、BlinkingがStare(-1f)を上回りClosing(0f)のとき有効
            passThroughToCanceler.Conditions = ImmutableList.Create(
                new AnimatorCondition()
                {
                    parameter = UseCancelerAAP,
                    mode = AnimatorConditionMode.Greater,
                    threshold = 0f
                },
                new AnimatorCondition()
                {
                    parameter = BlinkingAAP,
                    mode = AnimatorConditionMode.Greater,
                    threshold = -1f
                },
                new AnimatorCondition()
                {
                    parameter = BlinkingAAP,
                    mode = AnimatorConditionMode.Less,
                    threshold = 0.01f
                }
            );
            passThrough.Transitions = ImmutableList.Create(passThroughToCanceler);

            // canceler -> passThrough

            // cancelerが無効な時は早期リターン
            var cancelerToPassThrough1 = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
            cancelerToPassThrough1.SetDestination(passThrough);
            cancelerToPassThrough1.Conditions = ImmutableList.Create(
                new AnimatorCondition()
                {
                    parameter = UseCancelerAAP,
                    mode = AnimatorConditionMode.Less,
                    threshold = 1f
                }
            );
            canceler.Transitions = canceler.Transitions.Add(cancelerToPassThrough1);

            // Stare(-1f)の場合は早期リターン
            var cancelerToPassThrough2 = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
            cancelerToPassThrough2.SetDestination(passThrough);
            cancelerToPassThrough2.Conditions = ImmutableList.Create(
                new AnimatorCondition()
                {
                    parameter = BlinkingAAP,
                    mode = AnimatorConditionMode.Less,
                    threshold = -0.99f
                }
            );
            canceler.Transitions = canceler.Transitions.Add(cancelerToPassThrough2);

            // Closing(0f)を上回り、Opening(1f)のとき逆方向にキャンセラーを適用する
            var openDuration = advancedSettings.OpenAnimations.Select(a => a.Time).Max();
            var cancelerToPassThrough3 = AnimatorHelper.CreateTransitionWithDurationSeconds(openDuration);
            cancelerToPassThrough3.SetDestination(passThrough);
            cancelerToPassThrough3.Conditions = ImmutableList.Create(
                new AnimatorCondition()
                {
                    parameter = BlinkingAAP,
                    mode = AnimatorConditionMode.Greater,
                    threshold = 0.01f
                }
            );
            canceler.Transitions = canceler.Transitions.Add(cancelerToPassThrough3);

            position.y += 2 * PositionYStep;
        }
    }
}