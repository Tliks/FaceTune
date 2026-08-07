
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPathPrefix + ComponentName)]
    internal class ExpressionComponent : FaceTuneTagComponent, IHasExpressionData, IHasConditions, IHasMenuInstallSettings, IExpressionSettingsSource
    {
        internal const string ComponentName = FaceTuneConstants.Name;

        public ExpressionSettings ExpressionSettings = new();
        public FacialSettings FacialSettings = new();

        public bool ConditionEnabled;
        public Condition Condition = new();

        public bool DirectMenuEnabled;
        public DirectMenuSettings DirectMenuSettings = new();

        public ExpressionData Data = new();

        [ToggleLeft]
        public bool EnableRealTimePreview;

        // Optional local overrides. Each list is an independent 0..1 slot.
        public List<TransitionSetting> Transition = new();
        public List<StyleSetting> Style = new();
        public List<EyeBlinkSetting> EyeBlink = new();
        public List<LipSyncSetting> LipSync = new();

        ExpressionData IHasExpressionData.Data => Data;
        IReadOnlyList<TransitionSetting> IExpressionSettingsSource.TransitionSettings => Transition;
        IReadOnlyList<StyleSetting> IExpressionSettingsSource.StyleSettings => Style;
        IReadOnlyList<EyeBlinkSetting> IExpressionSettingsSource.EyeBlinkSettings => EyeBlink;
        IReadOnlyList<LipSyncSetting> IExpressionSettingsSource.LipSyncSettings => LipSync;
        IEnumerable<Condition> IHasConditions.Conditions => new[] { Condition };
        MenuInstallSettings? IHasMenuInstallSettings.InstallSettings
            => DirectMenuEnabled ? DirectMenuSettings.Menu.InstallSettings : null;

    }
}