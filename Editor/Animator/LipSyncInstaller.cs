using UnityEditor.Animations;
using nadena.dev.ndmf.animator;

namespace aoyon.facetune.animator;

internal class LipSyncInstaller : InstallerBase
{
    private readonly Dictionary<AdvancedLipSyncSettings, int> _advancedLipSyncIndex = new();

    private const string ParameterPrefix = $"{FaceTuneConsts.ParameterPrefix}/LipSync";
    private const string AllowAAP = $"{ParameterPrefix}/Allow";
    private const string UseAdvancedAAP = $"{ParameterPrefix}/UseAdvanced";
    private const string UseCancelerAAP = $"{ParameterPrefix}/UseCanceler";
    private const string ModeAAP = $"{ParameterPrefix}/Mode";

    public LipSyncInstaller(VirtualAnimatorController virtualController, SessionContext sessionContext, bool useWriteDefaults) : base(virtualController, sessionContext, useWriteDefaults)
    {
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, AllowAAP);
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, UseAdvancedAAP);
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, UseCancelerAAP);
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, ModeAAP);
    }

    public void SetSettings(VirtualClip clip, FacialSettings facialSettings)
    {
        if (facialSettings.AllowLipSync != TrackingPermission.Keep)
        {
            var curve = new AnimationCurve();
            var value = facialSettings.AllowLipSync == TrackingPermission.Allow ? 1 : 0;
            curve.AddKey(0, value);
            clip.SetFloatCurve("", typeof(Animator), AllowAAP, curve);

            var advancedSettings = facialSettings.AdvancedLipSyncSettings;
            if (advancedSettings.IsEnabled())
            {
                var useAdvancedCurve = new AnimationCurve();
                useAdvancedCurve.AddKey(0, 1);
                clip.SetFloatCurve("", typeof(Animator), UseAdvancedAAP, useAdvancedCurve);

                var index = GetAdvancedLipSyncIndex(advancedSettings);
                var modeCurve = new AnimationCurve();
                modeCurve.AddKey(0, VRCAAPHelper.IndexToValue(index));
                clip.SetFloatCurve("", typeof(Animator), ModeAAP, modeCurve);

                if (advancedSettings.IsCancelerEnabled())
                {
                    var useCancelerCurve = new AnimationCurve();
                    useCancelerCurve.AddKey(0, 1);
                    clip.SetFloatCurve("", typeof(Animator), UseCancelerAAP, useCancelerCurve);
                }
            }
        }
    }

    private int GetAdvancedLipSyncIndex(AdvancedLipSyncSettings advancedLipSyncSettings)
    {
        if (_advancedLipSyncIndex.TryGetValue(advancedLipSyncSettings, out var index))
        {
            return index;
        }
        else
        {
            index = _advancedLipSyncIndex.Count;
            _advancedLipSyncIndex[advancedLipSyncSettings] = index;
        }
        return index;
    }

    public void AddLipSyncLayer()
    {
        VirtualLayer? cancelerLayer = null;
        VirtualLayer? lipSyncLayer = null;

        if (_advancedLipSyncIndex.Any(kvp => kvp.Key.IsCancelerEnabled()))
        {
            cancelerLayer = AddFTLayer(_virtualController, "LipSync (Canceler)", LayerPriority);
        }
        lipSyncLayer = AddFTLayer(_virtualController, "LipSync", LayerPriority);
        
        var delayState = AddFTState(lipSyncLayer, "Delay", EntryStatePosition + new Vector3(-20, 2 * PositionYStep, 0));
        var delayClip = AnimatorHelper.CreateDelayClip(0.1f);
        delayState.Motion = delayClip;
        
        var enabledPosition = EntryStatePosition + new Vector3(PositionXStep, 0, 0);
        var enabled = AddFTState(lipSyncLayer, "Enabled", enabledPosition);
        var disabled = AddFTState(lipSyncLayer, "Disabled", enabledPosition + new Vector3(0, 2 * PositionYStep, 0));

        _platformSupport.SetLipSyncTrack(enabled, true);
        _platformSupport.SetLipSyncTrack(disabled, false);

        var delayTransition = AnimatorHelper.CreateTransitionWithExitTime(1f, 0f);
        delayTransition.SetDestination(enabled);
        delayState.Transitions = ImmutableList.Create(delayTransition);

        var enabledTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        enabledTransition.SetDestination(enabled);
        enabledTransition.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = AllowAAP,
            mode = AnimatorConditionMode.Greater,
            threshold = 0.99f
        });
        disabled.Transitions = ImmutableList.Create(enabledTransition);

        var disabledTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(0f);
        disabledTransition.SetDestination(disabled);
        disabledTransition.Conditions = ImmutableList.Create(new AnimatorCondition()
        {
            parameter = AllowAAP,
            mode = AnimatorConditionMode.Less,
            threshold = 0.99f
        });
        enabled.Transitions = ImmutableList.Create(disabledTransition);

        if (cancelerLayer is null) return;

        var mutePosition = EntryStatePosition + new Vector3(PositionXStep, 0, 0);
        var mute = AddFTState(cancelerLayer, "Mute", mutePosition);
        mute.Motion = _emptyClip;
        var position = mutePosition + new Vector3(PositionXStep, 0, 0);
        var voiceParam = "Voice"; // Todo
        _virtualController.EnsureParameterExists(AnimatorControllerParameterType.Float, voiceParam);
        foreach (var (advancedSettings, index) in _advancedLipSyncIndex.OrderBy(kvp => kvp.Value))
        {
            if (!advancedSettings.IsCancelerEnabled()) continue;

            var lipsyncing = AddFTState(cancelerLayer, $"Lipsyncing {index}", position);
            var cancelerAnimation = advancedSettings.CancelerBlendShapeNames.Select(name => BlendShapeAnimation.SingleFrame(name, 0f).ToGeneric(_sessionContext.BodyPath));
            AddAnimationToState(lipsyncing, cancelerAnimation);

            // mute -> lipsyncing
            var muteToLipsyncing = AnimatorHelper.CreateTransitionWithDurationSeconds(0.1f); // Todo: 設定可能にしても良いかも
            muteToLipsyncing.SetDestination(lipsyncing);
            muteToLipsyncing.Conditions = ImmutableList.Create(
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
            );
            if (index > 0)
            {   
                muteToLipsyncing.Conditions = muteToLipsyncing.Conditions.Add(
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
                muteToLipsyncing.Conditions = muteToLipsyncing.Conditions.Add(
                    new AnimatorCondition()
                    {
                        parameter = ModeAAP,
                        mode = AnimatorConditionMode.Less,
                        threshold = VRCAAPHelper.IndexToValue(index + 1)
                    }
                );
            }
            mute.Transitions = mute.Transitions.Add(muteToLipsyncing);

            // lipsyncing -> mute
            var lipsyncingToMute1 = AnimatorHelper.CreateTransitionWithDurationSeconds(0.1f);
            lipsyncingToMute1.SetDestination(mute);
            lipsyncingToMute1.Conditions = ImmutableList.Create(
                new AnimatorCondition()
                {
                    parameter = voiceParam,
                    mode = AnimatorConditionMode.Less,
                    threshold = 0.01f
                }
            );
            lipsyncing.Transitions = lipsyncing.Transitions.Add(lipsyncingToMute1);

            var lipsyncingToMute2 = AnimatorHelper.CreateTransitionWithDurationSeconds(0.1f);
            lipsyncingToMute2.SetDestination(mute);
            lipsyncingToMute2.Conditions = ImmutableList.Create(
                new AnimatorCondition()
                {
                    parameter = UseCancelerAAP,
                    mode = AnimatorConditionMode.Less,
                    threshold = 0.01f
                }
            );
            lipsyncing.Transitions = lipsyncing.Transitions.Add(lipsyncingToMute2);

            var lipsyncingToMute3 = AnimatorHelper.CreateTransitionWithDurationSeconds(0.1f);
            lipsyncingToMute3.SetDestination(mute);
            lipsyncingToMute3.Conditions = ImmutableList.Create(
                new AnimatorCondition()
                {
                    parameter = ModeAAP,
                    mode = AnimatorConditionMode.Greater,
                    threshold = VRCAAPHelper.IndexToValue(index)
                }
            );
            lipsyncing.Transitions = lipsyncing.Transitions.Add(lipsyncingToMute3);

            var lipsyncingToMute4 = AnimatorHelper.CreateTransitionWithDurationSeconds(0.1f);
            lipsyncingToMute4.SetDestination(mute);
            lipsyncingToMute4.Conditions = ImmutableList.Create(
                new AnimatorCondition()
                {
                    parameter = ModeAAP,
                    mode = AnimatorConditionMode.Less,
                    threshold = VRCAAPHelper.IndexToValue(index)
                }
            );
            lipsyncing.Transitions = lipsyncing.Transitions.Add(lipsyncingToMute4);
            
            position.y += PositionYStep;
        }
    }
}

