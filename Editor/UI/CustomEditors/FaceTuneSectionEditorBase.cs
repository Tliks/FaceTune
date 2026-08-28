namespace Aoyon.FaceTune.Gui;

internal interface ISectionActionProvider
{
    SectionActionSet Actions { get; }
}

internal interface ISectionDrawer : ISectionActionProvider
{
    float GetHeight();
    void Draw(Rect position);
}

internal interface ISectionHeaderDrawer
{
    float GetHeaderWidth();
    void DrawHeader(Rect position);
}

internal interface ISectionHeaderMenuDrawer
{
    void PopulateHeaderMenu(GenericMenu menu);
}

internal interface ICollapsedSectionHeaderDrawer : ISectionHeaderDrawer
{
    void DrawCollapsedHeader(Rect position);
}

internal static class SectionHeaderGUI
{
    public static Action<Rect>? GetDrawAction(ISectionHeaderDrawer? drawer, bool expanded)
    {
        if (drawer == null) return null;
        if (expanded) return drawer.DrawHeader;
        return drawer is ICollapsedSectionHeaderDrawer collapsed
            ? collapsed.DrawCollapsedHeader
            : null;
    }
}

internal sealed class PropertiesSectionDrawer : ISectionDrawer
{
    internal readonly record struct Entry(
        SerializedProperty Property,
        string? LabelKey,
        Func<object?> CreateDefaultValue);

    private readonly Entry[] _entries;

    public PropertiesSectionDrawer(params Entry[] entries)
    {
        if (entries.Length == 0)
            throw new ArgumentException("At least one property is required.", nameof(entries));

        _entries = entries;
        Actions = new SectionActionSet(
            entries[0].Property.serializedObject,
            entries.Select(entry => SectionActionField.From(
                entry.Property,
                entry.CreateDefaultValue)));
    }

    public SectionActionSet Actions { get; }

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
    SectionActionSet Actions,
    Func<GenericMenu> CreateHeaderMenu,
    SerializedProperty? EnabledProperty = null,
    ISectionHeaderDrawer? HeaderDrawer = null,
    Func<bool>? IsVisible = null,
    int SpacingGroup = 0)
{
    public FoldoutState Foldout { get; } = new(DefaultExpanded);
    public bool Visible => IsVisible?.Invoke() ?? true;
}

internal abstract class FaceTuneSectionEditorBase<T> : FaceTuneEditorBase<T> where T : FaceTuneTagComponent
{
    private IReadOnlyList<FaceTuneSection>? _sections;
    private bool[]? _visibleSections;

    protected abstract IReadOnlyList<FaceTuneSection> CreateSections();

    protected virtual float GetFooterHeight() => 0f;

    protected virtual void DrawFooter(Rect position)
    {
    }

    protected FaceTuneSection CreateSection(
        string labelKey,
        ISectionDrawer drawer,
        bool defaultExpanded,
        SerializedProperty? enabledProperty = null,
        Action<GenericMenu>? populateHeaderMenu = null,
        Func<bool>? isVisible = null,
        int spacingGroup = 0)
    {
        var actions = drawer.Actions.WithKey(labelKey);
        return new(
            () => labelKey.LG(),
            drawer.GetHeight,
            drawer.Draw,
            defaultExpanded,
            actions,
            () => SectionHeaderMenu.Create(actions, populateHeaderMenu),
            enabledProperty,
            drawer as ISectionHeaderDrawer,
            isVisible,
            spacingGroup);
    }

    protected sealed override float GetInspectorHeight()
    {
        var sections = Sections;
        if (_visibleSections == null || _visibleSections.Length != sections.Count)
            _visibleSections = new bool[sections.Count];

        var height = 0f;
        height += HeaderSpacing;

        FaceTuneSection? previous = null;
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            _visibleSections[i] = section.Visible;
            if (!_visibleSections[i]) continue;
            if (previous != null)
            {
                height += GUIHelper.HeaderSpacing;
                if (section.SpacingGroup != previous.SpacingGroup) height += SectionGroupSpacing;
            }
            height += GetSectionHeight(section);
            previous = section;
        }
        var footerHeight = GetFooterHeight();
        if (footerHeight > 0f)
        {
            if (previous != null) height += SectionGroupSpacing;
            height += footerHeight;
        }
        return height;
    }

    protected sealed override void DrawInspector(Rect position)
    {
        position.Space(HeaderSpacing);

        var sections = Sections;
        FaceTuneSection? previous = null;
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            if (_visibleSections == null || !_visibleSections[i]) continue;
            if (previous != null)
            {
                position.y += GUIHelper.HeaderSpacing;
                if (section.SpacingGroup != previous.SpacingGroup) position.y += SectionGroupSpacing;
            }

            var contentHeight = section.GetContentHeight();
            var sectionPosition = new Rect(
                position.x,
                position.y,
                position.width,
                GUIHelper.GetShurikenSectionHeight(section.Foldout, contentHeight));
            Rect content;
            bool drawn;
            var drawHeader = SectionHeaderGUI.GetDrawAction(
                section.HeaderDrawer,
                section.Foldout.Expanded);
            var headerWidth = drawHeader == null
                ? 0f
                : section.HeaderDrawer!.GetHeaderWidth();
            if (section.EnabledProperty == null)
            {
                drawn = GUIHelper.DrawShurikenSection(
                    sectionPosition,
                    section.Foldout,
                    section.GetLabel(),
                    contentHeight,
                    out content,
                    section.CreateHeaderMenu,
                    drawHeader,
                    headerWidth);
            }
            else
            {
                drawn = GUIHelper.DrawShurikenToggleSection(
                    sectionPosition,
                    section.Foldout,
                    section.EnabledProperty,
                    section.GetLabel(),
                    contentHeight,
                    out content,
                    section.CreateHeaderMenu,
                    drawHeader,
                    headerWidth);
            }
            if (drawn)
            {
                content.height = GUIHelper.LineHeight;
                using var disabled = new EditorGUI.DisabledScope(
                    section.EnabledProperty != null
                    && (!section.EnabledProperty.boolValue || section.EnabledProperty.hasMultipleDifferentValues));
                section.DrawContent(content);
            }
            position.y = sectionPosition.yMax;
            previous = section;
        }

        var footerHeight = GetFooterHeight();
        if (footerHeight <= 0f) return;
        if (previous != null) position.y += SectionGroupSpacing;
        position.height = footerHeight;
        DrawFooter(position);
    }

    private const float HeaderSpacing = 3f;
    private const float SectionGroupSpacing = 6f;

    private IReadOnlyList<FaceTuneSection> Sections
        => _sections ??= CreateSections();

    private static float GetSectionHeight(FaceTuneSection section)
        => GUIHelper.GetShurikenSectionHeight(section.Foldout, section.GetContentHeight());
}
