namespace com.aoyon.facetune;

[Serializable]
public struct PresetInfo
{
    public string PresetName;
    public bool AutoCrateMenuItemFromChildren;
    public List<ExpressionComponentBase> ExcludeExpressions;
    public List<ExpressionComponentBase> IncludeExpressions;

    public PresetInfo()
    {
        PresetName = string.Empty;
        AutoCrateMenuItemFromChildren = true;
        ExcludeExpressions = new();
        IncludeExpressions = new();
    }
}
