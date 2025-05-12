namespace com.aoyon.facetune
{
    [AddComponentMenu(MenuPath)]
    public class PresetComponent : FaceTuneTagComponent
    {
        internal const string MenuPath = FaceTuneTagComponent.FTName + "/" + ComponentName;
        internal const string ComponentName = "FT Preset";

        public PresetInfo Info = new();

        internal Preset GetPreset(SessionContext context)
        {
            var patterns = gameObject.GetComponentsInChildren<PatternComponent>(false)
                .Select(c => c.GetPatternWithPriority(context))
                .ToList();
            return new Preset(Info, new SortedExpressionPatterns(patterns));
        }
    }
}

