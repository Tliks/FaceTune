namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class PresetComponent : FaceTuneTagComponent
    {
        internal const string MenuPath = BasePath + "/" + ExpressionPattern + "/" + ComponentName;
        internal const string ComponentName = "FT Preset";

        public string OverridePresetName = string.Empty;
        public DefaultFacialExpressionComponent? OverrideDefaultExpressionComponent = null;

        internal ParameterCondition? AssignedPresetCondition { get; private set; }

        internal void SetAssignedPresetCondition(ParameterCondition presetCondition)
        {
            AssignedPresetCondition = presetCondition;
        }

        internal Preset? GetPreset(SessionContext sessionContext)
        {
            if (AssignedPresetCondition == null) return null;
            var patterns = gameObject.GetComponentsInChildren<PatternComponent>(true)
                .Select(c => c.GetPattern(sessionContext))
                .OfType<ExpressionPattern>()
                .ToList();
            if (patterns.Count == 0) return null;
            var presetName = string.IsNullOrWhiteSpace(OverridePresetName) ? gameObject.name : OverridePresetName;
            return new Preset(presetName, patterns, sessionContext.DEC.GetPresetDefaultExpression(this), AssignedPresetCondition);
        }

        internal GameObject GetMenuTarget()
        {
            // デフォルトは同階層にPresetのトグルを作る。
            // Todo: option to override
            var menuTarget = new GameObject(name);
            menuTarget.transform.SetParent(transform.parent);
            return menuTarget;
        }
    }
}