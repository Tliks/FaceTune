using UnityEditor.Animations;
using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build.Animator;

internal class LipSyncInstaller : InstallerBase
{
    private bool _shouldAddLayer = false;
    private bool _shouldAddCancelerLayer = false;

    private readonly Dictionary<AdvancedLipSyncSettings, int> _indexForAdvancedSettings = new();
    private readonly string _forceDisableLipSyncParameter;

    private const string ParameterPrefix = $"{FaceTuneConstants.ParameterPrefix}/LipSync";
    private const string AllowAAP = $"{ParameterPrefix}/Allow"; // 常に追加
    private const string UseAdvancedAAP = $"{ParameterPrefix}/UseAdvanced"; // 1つ以上有効なAdvancedLipSyncSettingsがあるとき
    private const string ModeAAP = $"{ParameterPrefix}/Mode"; // 同上
    private const string UseCancelerAAP = $"{ParameterPrefix}/UseCanceler"; // 1つ以上有効なCancelerがあるとき

    public LipSyncInstaller(
        VirtualAnimatorController virtualController,
        AvatarContext avatarContext,
        bool useWriteDefaults,
        IAnimatorPlatformServices platformServices,
        string forceDisableLipSyncParameter) : base(virtualController, avatarContext, useWriteDefaults, platformServices)
    {
        _forceDisableLipSyncParameter = forceDisableLipSyncParameter;
        if (!string.IsNullOrWhiteSpace(_forceDisableLipSyncParameter))
        {
            _controller.EnsureBoolParameterExists(_forceDisableLipSyncParameter);
        }
        _controller.EnsureFloatParameterExists(AllowAAP);
    }

    public void SetSettings(VirtualClip clip, FacialSettings facialSettings)
    {
        if (facialSettings.AllowLipSync != TrackingPermission.Keep)
        {
            _shouldAddLayer = true;

            // Allow
            var curve = new AnimationCurve();
            var value = facialSettings.AllowLipSync == TrackingPermission.Allow ? 1 : 0;
            curve.AddKey(0, value);
            clip.SetFloatCurve("", typeof(UnityEngine.Animator), AllowAAP, curve);
        }

        var advancedSettings = facialSettings.AdvancedLipSyncSettings;
        if (advancedSettings.IsEnabled())
        {
            _shouldAddLayer = true;

            _controller.EnsureFloatParameterExists(UseAdvancedAAP);
            _controller.EnsureFloatParameterExists(ModeAAP);

            // UseAdvanced
            var useAdvancedCurve = new AnimationCurve();
            useAdvancedCurve.AddKey(0, 1);
            clip.SetFloatCurve("", typeof(UnityEngine.Animator), UseAdvancedAAP, useAdvancedCurve);

            // Mode
            var index = GetIndexForSettings(advancedSettings);
            var modeCurve = new AnimationCurve();
            modeCurve.AddKey(0, VRCAAPHelper.IndexToValue(index));
            clip.SetFloatCurve("", typeof(UnityEngine.Animator), ModeAAP, modeCurve);

            if (advancedSettings.IsCancelerEnabled())
            {
                _shouldAddCancelerLayer = true;
                
                _controller.EnsureFloatParameterExists(UseCancelerAAP);

                // UseCanceler
                var useCancelerCurve = new AnimationCurve();
                useCancelerCurve.AddKey(0, 1);
                clip.SetFloatCurve("", typeof(UnityEngine.Animator), UseCancelerAAP, useCancelerCurve);
            }
        }
    }

    private int GetIndexForSettings(AdvancedLipSyncSettings advancedSettings)
    {
        return _indexForAdvancedSettings.GetOrAdd(advancedSettings, _indexForAdvancedSettings.Count);
    }

    private void AddForceDisableCondition(ICollection<AnimatorCondition> conditions, AnimatorConditionMode mode)
    {
        if (string.IsNullOrWhiteSpace(_forceDisableLipSyncParameter)) return;
        conditions.Add(new AnimatorCondition
        {
            parameter = _forceDisableLipSyncParameter,
            mode = mode
        });
    }

    public void MayAddLipSyncLayers()
    {
        if (!_shouldAddLayer) return;

        var lipSyncLayer = AddLayer("LipSync", LayerPriority);
        
        var delayState = AddState(lipSyncLayer, "Delay", EntryStatePosition + new Vector3(-20, 2 * PositionYStep, 0));
        var delayClip = AnimatorHelper.CreateDelayClip(0.1f);
        delayState.Motion = delayClip;
        
        var enabledPosition = EntryStatePosition + new Vector3(PositionXStep, 0, 0);
        var enabled = AddState(lipSyncLayer, "Enabled", enabledPosition);
        var disabled = AddState(lipSyncLayer, "Disabled", enabledPosition + new Vector3(0, 2 * PositionYStep, 0));

        enabled.Motion = _emptyClip;
        disabled.Motion = _emptyClip;
        _platformServices.SetLipSyncTracking(enabled, true);
        _platformServices.SetLipSyncTracking(disabled, false);

        var delayToEnabledTransition = AnimatorHelper.CreateTransitionWithExitTime(1f, 0f);
        delayToEnabledTransition.SetDestination(enabled);
        delayState.Transitions = ImmutableList.Create(delayToEnabledTransition);

        var enabledToDisabledTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        enabledToDisabledTransition.SetDestination(disabled);
        var disableORConditions = new List<AnimatorCondition>
        {
            new AnimatorCondition()
            {
                parameter = AllowAAP,
                mode = AnimatorConditionMode.Less,
                threshold = 0.99f // 安全側(Mute)に倒す
            }
        };
        AddForceDisableCondition(disableORConditions, AnimatorConditionMode.If);
        var orTransitions = AnimatorHelper.SetORConditions(enabledToDisabledTransition, disableORConditions);
        enabled.Transitions = ImmutableList.CreateRange(orTransitions);

        var disabledToEnabledTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        disabledToEnabledTransition.SetDestination(enabled);
        var disabledToEnabledConditions = new List<AnimatorCondition>()
        {
            new AnimatorCondition()
            {
                parameter = AllowAAP,
                mode = AnimatorConditionMode.Greater,
                threshold = 0.99f // 同上
            }
        };
        AddForceDisableCondition(disabledToEnabledConditions, AnimatorConditionMode.IfNot);
        disabledToEnabledTransition.Conditions = ImmutableList.CreateRange(disabledToEnabledConditions);
        disabled.Transitions = ImmutableList.Create(disabledToEnabledTransition);

        if (_shouldAddCancelerLayer)
        {
            AddCancelerLayer();
        }
    }

    private const float CancelerThreshold = 0.01f;  // 0fだと流石に不安定になる
    private void AddCancelerLayer()
    {
        var cancelerLayer = AddLayer("LipSync (Canceler)", LayerPriority);

        // キャンセラーに使うブレンドシェイプは複製されておらず、かつ transition durationを使うのでPassThrough
        var passThroughPosition = EntryStatePosition + new Vector3(PositionXStep, 0, 0);
        var passThrough = AddState(cancelerLayer, "PassThrough", passThroughPosition);
        AsPassThrough(passThrough);

        var voiceParam = "Voice"; // Todo
        _controller.EnsureFloatParameterExists(voiceParam);

        var position = passThroughPosition + new Vector3(PositionXStep, 0, 0);
        foreach (var (settings, index) in _indexForAdvancedSettings.OrderBy(kvp => kvp.Value))
        {
            if (!settings.IsCancelerEnabled()) continue;

            var lipsyncing = AddState(cancelerLayer, $"Lipsyncing {index}", position);
            var cancelerAnimation = settings.CancelerBlendShapeNames.Select(name => BlendShapeWeightAnimation.SingleFrame(name, 0f));
            AddBlendShapeAnimationsToState(lipsyncing, cancelerAnimation);

            // PassThrough -> lipsyncing
            var passThroughToLipsyncing = AnimatorHelper.CreateTransitionWithDurationSeconds(settings.CancelerEntryDurationSeconds);
            passThroughToLipsyncing.SetDestination(lipsyncing);
            var andConditions = new List<AnimatorCondition> {
                new AnimatorCondition()
                {
                    parameter = UseCancelerAAP,
                    mode = AnimatorConditionMode.Greater,
                    threshold = 0.01f // 遷移を開始した直後から有効(有効側に寄せる)
                },
                new AnimatorCondition()
                {
                    parameter = voiceParam,
                    mode = AnimatorConditionMode.Greater,
                    threshold = CancelerThreshold
                }
            };
            AddForceDisableCondition(andConditions, AnimatorConditionMode.IfNot);
            andConditions.AddRange(VRCAAPHelper.IndexConditions(ModeAAP, true, index));
            passThroughToLipsyncing.Conditions = ImmutableList.CreateRange(andConditions);
            passThrough.Transitions = passThrough.Transitions.Add(passThroughToLipsyncing);

            // lipsyncing -> PassThrough
            var lipsyncingToPassThrough = AnimatorHelper.CreateTransitionWithDurationSeconds(settings.CancelerExitDurationSeconds);
            lipsyncingToPassThrough.SetDestination(passThrough);
            var orConditions = new List<AnimatorCondition>();
            orConditions.AddRange(VRCAAPHelper.IndexConditions(ModeAAP, false, index));
            AddForceDisableCondition(orConditions, AnimatorConditionMode.If);
            orConditions.Add(new AnimatorCondition()
            {
                parameter = UseCancelerAAP,
                mode = AnimatorConditionMode.Less,
                threshold = 0.01f // 有効化側に寄せる
            });
            orConditions.Add(new AnimatorCondition()
            {
                parameter = voiceParam,
                mode = AnimatorConditionMode.Less,
                threshold = CancelerThreshold
            });
            var orTransitions = AnimatorHelper.SetORConditions(lipsyncingToPassThrough, orConditions);
            lipsyncing.Transitions = lipsyncing.Transitions.AddRange(orTransitions);

            position.y += PositionYStep;
        }
    }
}