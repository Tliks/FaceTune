namespace com.aoyon.facetune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPath)]
    public class PresetComponent : FaceTuneTagComponent
    {
        internal const string MenuPath = BasePath + "/" + ExpressionPattern + "/" + ComponentName;
        internal const string ComponentName = "FT Preset";

        internal Preset? GetPreset(SessionContext sessionContext)
        {
            var patterns = gameObject.GetComponentsInChildren<PatternComponent>(true)
                .Select(c => c.GetPattern(sessionContext))
                .OfType<ExpressionPattern>()
                .ToList();
            if (patterns.Count == 0) return null;
            return new Preset(gameObject.name, patterns);
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