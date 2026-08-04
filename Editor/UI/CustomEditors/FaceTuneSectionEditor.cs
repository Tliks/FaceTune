namespace Aoyon.FaceTune.Gui;

internal sealed record FaceTuneSection(
    GUIContent Label,
    Func<float> GetContentHeight,
    Action<Rect> DrawContent,
    bool DefaultExpanded,
    SerializedProperty? EnabledProperty = null)
{
    public bool Expanded = DefaultExpanded;
}

internal abstract class FaceTuneSectionEditor<T> : FaceTuneEditor<T> where T : FaceTuneTagComponent
{
    private IReadOnlyList<FaceTuneSection>? _sections;

    protected abstract IReadOnlyList<FaceTuneSection> CreateSections();

    protected virtual float GetAdditionalSectionSpacingBefore(int sectionIndex) => 0f;

    protected FaceTuneSection CreatePropertySection(GUIContent label, params string[] propertyNames)
        => new(
            label,
            () => propertyNames.Sum(propertyName => GetPropertyHeight(propertyName))
                + GUIHelper.VerticalSpacing * (propertyNames.Length - 1),
            position =>
            {
                foreach (var propertyName in propertyNames)
                {
                    DrawProperty(ref position, propertyName);
                }
            },
            true);

    protected sealed override float GetInspectorHeight()
    {
        var sections = Sections;
        var height = 0f;
        for (var i = 0; i < sections.Count; i++)
        {
            if (i > 0) height += GUIHelper.HeaderSpacing;
            height += GetAdditionalSectionSpacingBefore(i);
            height += GetSectionHeight(sections[i]);
        }
        return height;
    }

    protected sealed override void DrawInspector(Rect position)
    {
        var sections = Sections;
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            if (i > 0) position.y += GUIHelper.HeaderSpacing;
            position.y += GetAdditionalSectionSpacingBefore(i);

            var contentHeight = section.GetContentHeight();
            var sectionPosition = new Rect(
                position.x,
                position.y,
                position.width,
                GUIHelper.GetShurikenSectionHeight(section.Expanded, contentHeight));
            var expanded = section.Expanded;
            Rect content;
            bool drawn;
            if (section.EnabledProperty == null)
            {
                drawn = GUIHelper.DrawShurikenSection(
                    sectionPosition,
                    ref expanded,
                    section.Label,
                    contentHeight,
                    out content);
            }
            else
            {
                drawn = GUIHelper.DrawShurikenToggleSection(
                    sectionPosition,
                    ref expanded,
                    section.EnabledProperty,
                    section.Label,
                    contentHeight,
                    out content);
            }
            if (drawn)
            {
                content.height = GUIHelper.LineHeight;
                section.DrawContent(content);
            }
            section.Expanded = expanded;
            position.y = sectionPosition.yMax;
        }
    }

    private IReadOnlyList<FaceTuneSection> Sections
        => _sections ??= CreateSections();

    private static float GetSectionHeight(FaceTuneSection section)
        => GUIHelper.GetShurikenSectionHeight(section.Expanded, section.GetContentHeight());
}
