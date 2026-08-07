namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal sealed class GroupSettingsComponent : FaceTuneTagComponent, IHasConditions, IHasMenuInstallSettings, IExpressionSettingsSource
    {
        internal const string ComponentName = ComponentNamePrefix + "Group Settings";

        public List<TransitionSetting> Transition = new();
        public List<StyleSetting> Style = new();
        public List<EyeBlinkSetting> EyeBlink = new();
        public List<LipSyncSetting> LipSync = new();
        public List<Condition> Conditions = new();
        public List<SetSetting> Set = new();

        IEnumerable<Condition> IHasConditions.Conditions => Conditions;
        IReadOnlyList<TransitionSetting> IExpressionSettingsSource.TransitionSettings => Transition;
        IReadOnlyList<StyleSetting> IExpressionSettingsSource.StyleSettings => Style;
        IReadOnlyList<EyeBlinkSetting> IExpressionSettingsSource.EyeBlinkSettings => EyeBlink;
        IReadOnlyList<LipSyncSetting> IExpressionSettingsSource.LipSyncSettings => LipSync;
        MenuInstallSettings? IHasMenuInstallSettings.InstallSettings => Set.Count == 1 ? Set[0].Menu.InstallSettings : null;
    }
}