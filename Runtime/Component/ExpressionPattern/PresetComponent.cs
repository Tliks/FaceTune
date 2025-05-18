namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class PresetComponent : FaceTuneTagComponent
    {
        internal const string MenuPath = FaceTuneTagComponent.FTName + "/" + ComponentName;
        internal const string ComponentName = "FT Preset";

        public string PresetName = string.Empty;

        internal Preset? GetPreset(SessionContext context)
        {
            var patterns = gameObject.GetComponentsInChildren<PatternComponent>(false)
                .Select(c => c.GetPatternWithPriority(context))
                .ToList();
            if (patterns.Count == 0) return null;
            return new Preset(PresetName, new SortedExpressionPatterns(patterns));
        }
    }
}