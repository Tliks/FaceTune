namespace Aoyon.FaceTune.Gui;

internal abstract class FaceTuneSectionEditor<T> : FaceTuneEditor<T> where T : FaceTuneTagComponent
{
    private bool _expanded;
    private bool _expandedInitialized;

    protected virtual bool DefaultExpanded => true;
    protected abstract GUIContent SectionLabel { get; }
    protected abstract float GetSectionContentHeight();
    protected abstract void DrawSectionContent(Rect position);

    protected sealed override float GetInspectorHeight()
    {
        EnsureExpandedInitialized();
        return GUIHelper.GetShurikenSectionHeight(_expanded, GetSectionContentHeight());
    }

    protected sealed override void DrawInspector(Rect position)
    {
        EnsureExpandedInitialized();
        var contentHeight = GetSectionContentHeight();
        if (GUIHelper.DrawShurikenSection(
                position,
                ref _expanded,
                SectionLabel,
                contentHeight,
                out var content))
            DrawSectionContent(content);
    }

    private void EnsureExpandedInitialized()
    {
        if (_expandedInitialized) return;
        _expanded = DefaultExpanded;
        _expandedInitialized = true;
    }
}
