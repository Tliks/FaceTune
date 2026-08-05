
namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPathPrefix + ComponentName)]
    internal class FaceTuneComponent : FaceTuneTagComponent, IHasExpressionData, IHasConditions, IHasMenuInstallSettings
    {
        internal const string ComponentName = FaceTuneConstants.Name;

        public bool ConditionEnabled = false;
        public Condition Condition = new(ConditionCase.From(new HandGestureCondition()));

        public bool DirectMenuEnabled = false;
        public DirectMenuSettings DirectMenuSettings = new()
        {
            Menu = new MenuSettings
            {
                Icon = new MenuIconSettings { Mode = MenuIconMode.ExpressionPreview }
            }
        };

        public ExpressionSettings ExpressionSettings = new();
        public FacialSettings FacialSettings = new();
        
        public ExpressionData Data = new();

        [ToggleLeft]
        public bool EnableRealTimePreview = false;

        ExpressionData IHasExpressionData.Data => Data;
        IEnumerable<Condition> IHasConditions.Conditions => new[] { Condition };
        MenuInstallSettings? IHasMenuInstallSettings.InstallSettings
            => DirectMenuEnabled ? DirectMenuSettings.Menu.InstallSettings : null;

    }
}