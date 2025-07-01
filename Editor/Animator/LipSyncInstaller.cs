using UnityEditor.Animations;
using nadena.dev.ndmf.animator;

namespace aoyon.facetune.animator;

internal class LipSyncInstaller : InstallerBase
{
    private bool _shouldAddLayer = false;
    private bool _shouldAddCancelerLayer = false;

    private readonly Dictionary<AdvancedLipSyncSettings, int> _indexForAdvancedSettings = new();

    private const string ParameterPrefix = $"{FaceTuneConsts.ParameterPrefix}/LipSync";
    private const string AllowAAP = $"{ParameterPrefix}/Allow"; // 常に追加
    private const string UseAdvancedAAP = $"{ParameterPrefix}/UseAdvanced"; // 1つ以上有効なAdvancedLipSyncSettingsがあるとき
    private const string UseCancelerAAP = $"{ParameterPrefix}/UseCanceler"; // 同上
    private const string ModeAAP = $"{ParameterPrefix}/Mode"; // 同上

    public LipSyncInstaller(VirtualAnimatorController virtualController, SessionContext sessionContext, bool useWriteDefaults) : base(virtualController, sessionContext, useWriteDefaults)
    {
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, AllowAAP);
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
            clip.SetFloatCurve("", typeof(Animator), AllowAAP, curve);

            var advancedSettings = facialSettings.AdvancedLipSyncSettings;
            if (advancedSettings.IsEnabled())
            {
                _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, UseAdvancedAAP);
                _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, UseCancelerAAP);
                _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, ModeAAP);

                // UseAdvanced
                var useAdvancedCurve = new AnimationCurve();
                useAdvancedCurve.AddKey(0, 1);
                clip.SetFloatCurve("", typeof(Animator), UseAdvancedAAP, useAdvancedCurve);

                // Mode
                var index = GetIndexForSettings(advancedSettings);
                var modeCurve = new AnimationCurve();
                modeCurve.AddKey(0, VRCAAPHelper.IndexToValue(index));
                clip.SetFloatCurve("", typeof(Animator), ModeAAP, modeCurve);

                if (advancedSettings.IsCancelerEnabled())
                {
                    _shouldAddCancelerLayer = true;
                    
                    // UseCanceler
                    var useCancelerCurve = new AnimationCurve();
                    useCancelerCurve.AddKey(0, 1);
                    clip.SetFloatCurve("", typeof(Animator), UseCancelerAAP, useCancelerCurve);
                }
            }
        }
    }

    private int GetIndexForSettings(AdvancedLipSyncSettings advancedSettings)
    {
        return _indexForAdvancedSettings.GetOrAdd(advancedSettings, _indexForAdvancedSettings.Count);
    }

    public void MayAddLipSyncLayers()
    {
        if (!_shouldAddLayer) return;

        var lipSyncLayer = AddFTLayer(_virtualController, "LipSync", LayerPriority);
        
        var delayState = AddFTState(lipSyncLayer, "Delay", EntryStatePosition + new Vector3(-20, 2 * PositionYStep, 0));
        var delayClip = AnimatorHelper.CreateDelayClip(0.1f);
        delayState.Motion = delayClip;
        
        var enabledPosition = EntryStatePosition + new Vector3(PositionXStep, 0, 0);
        var enabled = AddFTState(lipSyncLayer, "Enabled", enabledPosition);
        var disabled = AddFTState(lipSyncLayer, "Disabled", enabledPosition + new Vector3(0, 2 * PositionYStep, 0));

        _platformSupport.SetLipSyncTrack(enabled, true);
        _platformSupport.SetLipSyncTrack(disabled, false);

        var delayToEnabledTransition = AnimatorHelper.CreateTransitionWithExitTime(1f, 0f);
        delayToEnabledTransition.SetDestination(enabled);
        delayState.Transitions = ImmutableList.Create(delayToEnabledTransition);

        var enabledToDisabledTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        enabledToDisabledTransition.SetDestination(disabled);
        enabledToDisabledTransition.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = AllowAAP,
            mode = AnimatorConditionMode.Greater,
            threshold = 0.99f // 安全側(Mute)に倒す
        });
        enabled.Transitions = ImmutableList.Create(enabledToDisabledTransition);

        var disabledToEnabledTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        disabledToEnabledTransition.SetDestination(enabled);
        disabledToEnabledTransition.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = AllowAAP,
            mode = AnimatorConditionMode.Less,
            threshold = 0.99f // 同上
        });
        disabled.Transitions = ImmutableList.Create(disabledToEnabledTransition);

        if (_shouldAddCancelerLayer)
        {
            AddCancelerLayer();
        }
    }

    private void AddCancelerLayer()
    {
        var cancelerLayer = AddFTLayer(_virtualController, "LipSync (Canceler)", LayerPriority);

        var mutePosition = EntryStatePosition + new Vector3(PositionXStep, 0, 0);
        var mute = AddFTState(cancelerLayer, "Mute", mutePosition);
        mute.Motion = _emptyClip;

        var voiceParam = "Voice"; // Todo
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, voiceParam);

        var position = mutePosition + new Vector3(PositionXStep, 0, 0);
        foreach (var (settings, index) in _indexForAdvancedSettings.OrderBy(kvp => kvp.Value))
        {
            if (!settings.IsCancelerEnabled()) continue;

            var lipsyncing = AddFTState(cancelerLayer, $"Lipsyncing {index}", position);
            var cancelerAnimation = settings.CancelerBlendShapeNames.Select(name => BlendShapeAnimation.SingleFrame(name, 0f).ToGeneric(_sessionContext.BodyPath));
            AddAnimationToState(lipsyncing, cancelerAnimation);

            // mute -> lipsyncing
            var muteToLipsyncing = AnimatorHelper.CreateTransitionWithDurationSeconds(settings.CancelerDurationSeconds);
            muteToLipsyncing.SetDestination(lipsyncing);
            var andConditions = new List<AnimatorCondition> {
                new AnimatorCondition()
                {
                    parameter = UseCancelerAAP,
                    mode = AnimatorConditionMode.Greater,
                    threshold = 0f
                },
                new AnimatorCondition()
                {
                    parameter = voiceParam,
                    mode = AnimatorConditionMode.Greater,
                    threshold = 0f
                }
            };
            andConditions.AddRange(VRCAAPHelper.IndexConditions(ModeAAP, true, index));
            muteToLipsyncing.Conditions = ImmutableList.CreateRange(andConditions);
            mute.Transitions = mute.Transitions.Add(muteToLipsyncing);

            // lipsyncing -> mute
            var lipsyncingToMute = AnimatorHelper.CreateTransitionWithDurationSeconds(settings.CancelerDurationSeconds);
            lipsyncingToMute.SetDestination(mute);
            var orConditions = new List<AnimatorCondition>
            {
                new AnimatorCondition()
                {
                    parameter = voiceParam,
                    mode = AnimatorConditionMode.Less,
                    threshold = 0.01f
                },
                new AnimatorCondition()
                {
                    parameter = UseCancelerAAP,
                    mode = AnimatorConditionMode.Less,
                    threshold = 1f
                }
            };
            orConditions.AddRange(VRCAAPHelper.IndexConditions(ModeAAP, false, index));
            var orTransitions = AnimatorHelper.SetORConditions(lipsyncingToMute, orConditions);
            lipsyncing.Transitions = lipsyncing.Transitions.AddRange(orTransitions);

            position.y += PositionYStep;
        }
    }
}