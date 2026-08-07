namespace Aoyon.FaceTune.Gui;

internal interface ISectionDrawer
{
    float GetHeight();
    void Draw(Rect position);
}

internal sealed class PropertiesSectionDrawer : ISectionDrawer
{
    internal readonly record struct Entry(SerializedProperty Property, string? LabelKey = null);

    private readonly Entry[] _entries;

    public PropertiesSectionDrawer()
        : this(Array.Empty<Entry>())
    {
    }


    public PropertiesSectionDrawer(params Entry[] entries)
    {
        _entries = entries;
    }

    public float GetHeight()
        => _entries.Length == 0
            ? GUIHelper.LineHeight
            : _entries.Sum(entry => EditorGUI.GetPropertyHeight(entry.Property, GUIContent.none, true))
                + GUIHelper.VerticalSpacing * (_entries.Length - 1);

    public void Draw(Rect position)
    {
        if (_entries.Length == 0)
        {
            EditorGUI.LabelField(position, "section.empty.message".LG());
            return;
        }

        foreach (var entry in _entries)
        {
            position.height = EditorGUI.GetPropertyHeight(entry.Property, GUIContent.none, true);
            if (entry.LabelKey == null)
                EditorGUI.PropertyField(position, entry.Property, true);
            else
                EditorGUI.PropertyField(position, entry.Property, entry.LabelKey.LG(), true);
            position.NewLine();
        }
    }

}

internal sealed record FaceTuneSection(
    Func<GUIContent> GetLabel,
    Func<float> GetContentHeight,
    Action<Rect> DrawContent,
    bool DefaultExpanded,
    SerializedProperty? EnabledProperty = null,
    Func<GenericMenu>? CreateHeaderMenu = null)
{
    public bool Expanded = DefaultExpanded;
}

internal abstract class FaceTuneSectionEditorBase<T> : FaceTuneEditorBase<T> where T : FaceTuneTagComponent
{
    private IReadOnlyList<FaceTuneSection>? _sections;

    protected abstract IReadOnlyList<FaceTuneSection> CreateSections();

    protected virtual float GetAdditionalSectionSpacingBefore(int sectionIndex) => 0f;

    protected FaceTuneSection CreateSection(
        string labelKey,
        ISectionDrawer drawer,
        bool defaultExpanded,
        SerializedProperty? enabledProperty = null,
        Action<GenericMenu>? populateHeaderMenu = null)
        => new(
            () => labelKey.LG(),
            drawer.GetHeight,
            drawer.Draw,
            defaultExpanded,
            enabledProperty,
            populateHeaderMenu == null ? null : () => CreateHeaderMenu(populateHeaderMenu));

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
                    section.GetLabel(),
                    contentHeight,
                    out content,
                    section.CreateHeaderMenu);
            }
            else
            {
                drawn = GUIHelper.DrawShurikenToggleSection(
                    sectionPosition,
                    ref expanded,
                    section.EnabledProperty,
                    section.GetLabel(),
                    contentHeight,
                    out content,
                    section.CreateHeaderMenu);
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

    private static GenericMenu CreateHeaderMenu(Action<GenericMenu> populateHeaderMenu)
    {
        var menu = new GenericMenu();
        populateHeaderMenu(menu);
        return menu;
    }

    private IReadOnlyList<FaceTuneSection> Sections
        => _sections ??= CreateSections();

    private static float GetSectionHeight(FaceTuneSection section)
        => GUIHelper.GetShurikenSectionHeight(section.Expanded, section.GetContentHeight());
}
