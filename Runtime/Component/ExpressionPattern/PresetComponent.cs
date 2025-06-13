namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class PresetComponent : FaceTuneTagComponent
    {
        internal const string MenuPath = FaceTune + "/" + ExpressionPattern + "/" + ComponentName;
        internal const string ComponentName = "FT Preset";

        public string OverridePresetName = string.Empty;
        public DefaultFacialExpressionComponent? OverrideDefaultExpressionComponent = null;

        internal Preset? GetPreset(SessionContext sessionContext, ParameterCondition presetCondition)
        {
            var patterns = gameObject.GetComponentsInChildren<PatternComponent>(true)
                .Select(c => c.GetPattern(sessionContext))
                .UnityOfType<ExpressionPattern>()
                .ToList();
            if (patterns.Count == 0) return null;
            var presetName = string.IsNullOrWhiteSpace(OverridePresetName) ? gameObject.name : OverridePresetName;
            return new Preset(presetName, patterns, sessionContext.DEC.GetPresetDefaultExpression(this), presetCondition);
        }

        internal GameObject GetMenuTarget()
        {
            // デフォルトは同階層にPresetのトグルを作る。
            // Todo: option to override
            var menuTarget = new GameObject();
            menuTarget.transform.SetParent(transform.parent);
            return menuTarget;
        }
    }
}