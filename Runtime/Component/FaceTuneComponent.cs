
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPathPrefix + ComponentName)]
    internal class FaceTuneComponent : FaceTuneTagComponent, IHasExpressionData, IHasConditions, IHasMenuInstallSettings
    {
        internal const string ComponentName = FaceTuneConstants.Name;

        public bool ConditionEnabled = DefaultConditionEnabled;
        public Condition Condition = CreateDefaultCondition();

        public bool DirectMenuEnabled = DefaultDirectMenuEnabled;
        public DirectMenuSettings DirectMenuSettings = CreateDefaultDirectMenuSettings();

        public ExpressionSettings ExpressionSettings = CreateDefaultExpressionSettings();
        public FacialSettings FacialSettings = CreateDefaultFacialSettings();

        public ExpressionData Data = CreateDefaultData();

        [ToggleLeft]
        public bool EnableRealTimePreview = DefaultEnableRealTimePreview;

        public const bool DefaultConditionEnabled = false;
        public const bool DefaultDirectMenuEnabled = false;
        public const bool DefaultEnableRealTimePreview = false;

        internal static Condition CreateDefaultCondition() => new(ConditionCase.From(new HandGestureCondition()));
        internal static DirectMenuSettings CreateDefaultDirectMenuSettings() => new()
        {
            Menu = new MenuSettings
            {
                Icon = new MenuIconSettings { Mode = MenuIconMode.ExpressionPreview }
            }
        };
        internal static ExpressionSettings CreateDefaultExpressionSettings() => new();
        internal static FacialSettings CreateDefaultFacialSettings() => new();
        internal static ExpressionData CreateDefaultData() => new();

        ExpressionData IHasExpressionData.Data => Data;
        IEnumerable<Condition> IHasConditions.Conditions => new[] { Condition };
        MenuInstallSettings? IHasMenuInstallSettings.InstallSettings
            => DirectMenuEnabled ? DirectMenuSettings.Menu.InstallSettings : null;

    }
}