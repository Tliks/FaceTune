namespace Aoyon.FaceTune.Gui;

internal interface ISectionActionProvider
{
    SectionActionSet Actions { get; }
}

internal interface ISectionActionAvailability
{
    bool ActionsEnabled { get; }
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

    public static Action<Rect>? Disable(Action<Rect>? draw, bool disabled)
    {
        if (draw == null || !disabled) return draw;
        return position =>
        {
            using var scope = new EditorGUI.DisabledScope(true);
            draw(position);
        };
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

internal readonly record struct SectionToggleState(bool Enabled, bool Mixed);

internal sealed record SectionToggle(
    Func<SectionToggleState> GetState,
    Action<bool> SetEnabled,
    bool Editable = true)
{
    public static SectionToggle From(SerializedProperty property)
        => new(
            () => new SectionToggleState(
                property.boolValue,
                property.hasMultipleDifferentValues),
            enabled => property.boolValue = enabled);
}

internal sealed record NestedSection(
    string LabelKey,
    ISectionDrawer Drawer,
    SectionToggle? Toggle = null,
    bool ShowHeader = true,
    bool DefaultExpanded = false)
{
    public FoldoutState Foldout { get; } = new(DefaultExpanded);
}

internal sealed class NestedSectionGroupDrawer : ISectionDrawer
{
    private readonly NestedSection[] _sections;
    private readonly float _headerWidth;
    private readonly bool _readOnly;

    public NestedSectionGroupDrawer(
        SerializedObject serializedObject,
        IEnumerable<NestedSection> sections,
        float headerWidth,
        bool readOnly = false)
    {
        _sections = sections.ToArray();
        _headerWidth = headerWidth;
        _readOnly = readOnly;
        Actions = new SectionActionSet(
            serializedObject,
            _sections.SelectMany(section => section.Drawer.Actions.Fields));
    }

    public SectionActionSet Actions { get; }

    public float GetHeight()
        => _sections.Sum(section => GUIHelper.GetShurikenSectionHeight(
            section.Foldout,
            section.Drawer.GetHeight()))
         + GUIHelper.VerticalSpacing * (_sections.Length - 1);

    public void Draw(Rect position)
    {
        position.Indent(GUIHelper.NestedSectionIndent);
        position.width += GUIHelper.ContentPadding;
        for (var i = 0; i < _sections.Length; i++)
        {
            var section = _sections[i];
            var contentHeight = section.Drawer.GetHeight();
            var rect = new Rect(
                position.x,
                position.y,
                position.width,
                GUIHelper.GetShurikenSectionHeight(section.Foldout, contentHeight));
            var toggle = _readOnly && section.Toggle != null
                ? section.Toggle with { Editable = false }
                : section.Toggle;
            var toggleState = toggle?.GetState();
            var disabled = _readOnly
                           || toggleState is { } state && (!state.Enabled || state.Mixed);
            var headerDrawer = section.ShowHeader
                ? section.Drawer as ISectionHeaderDrawer
                : null;
            var drawHeader = _readOnly && headerDrawer is ICollapsedSectionHeaderDrawer collapsed
                ? collapsed.DrawCollapsedHeader
                : SectionHeaderGUI.GetDrawAction(headerDrawer, section.Foldout.Expanded);
            drawHeader = SectionHeaderGUI.Disable(drawHeader, disabled);

            Func<GenericMenu>? createMenu = _readOnly
                ? null
                : () => SectionHeaderMenu.Create(
                    section.Drawer.Actions,
                    enabled: !disabled && SectionHeaderMenu.ActionsEnabled(section.Drawer));
            bool drawn;
            Rect content;
            if (toggle == null)
            {
                drawn = GUIHelper.DrawShurikenSection(
                    rect,
                    section.Foldout,
                    section.LabelKey.LG(),
                    contentHeight,
                    out content,
                    createMenu,
                    drawHeader,
                    drawHeader == null ? 0f : _headerWidth,
                    section.Drawer.Actions.ScopeProperty);
            }
            else
            {
                drawn = GUIHelper.DrawShurikenToggleSection(
                    rect,
                    section.Foldout,
                    toggle,
                    section.LabelKey.LG(),
                    contentHeight,
                    out content,
                    createMenu,
                    drawHeader,
                    drawHeader == null ? 0f : _headerWidth,
                    section.Drawer.Actions.ScopeProperty);
            }
            if (drawn)
            {
                content.height = GUIHelper.LineHeight;
                using var scope = new EditorGUI.DisabledScope(disabled);
                section.Drawer.Draw(content);
            }
            position.y = rect.yMax;
            if (i + 1 < _sections.Length)
                position.y += GUIHelper.VerticalSpacing;
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
    SectionToggle? Toggle = null,
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
        SectionToggle? customToggle = null,
        Action<GenericMenu>? populateHeaderMenu = null,
        Func<bool>? isVisible = null,
        int spacingGroup = 0)
    {
        var actions = drawer.Actions.WithKey(labelKey);
        var populateMenu = populateHeaderMenu;
        if (populateMenu == null && drawer is ISectionHeaderMenuDrawer menuDrawer)
            populateMenu = menuDrawer.PopulateHeaderMenu;

        return new(
            () => labelKey.LG(),
            drawer.GetHeight,
            drawer.Draw,
            defaultExpanded,
            actions,
            () => SectionHeaderMenu.Create(actions, populateMenu),
            customToggle ?? (enabledProperty == null ? null : SectionToggle.From(enabledProperty)),
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
            var toggleState = section.Toggle?.GetState();
            var sectionDisabled = toggleState is { } state
                                  && (!state.Enabled || state.Mixed);
            drawHeader = SectionHeaderGUI.Disable(drawHeader, sectionDisabled);
            var headerWidth = drawHeader == null
                ? 0f
                : section.HeaderDrawer!.GetHeaderWidth();
            Func<GenericMenu> createHeaderMenu = sectionDisabled
                ? () => SectionHeaderMenu.Create(section.Actions, enabled: false)
                : section.CreateHeaderMenu;
            if (section.Toggle == null)
            {
                drawn = GUIHelper.DrawShurikenSection(
                    sectionPosition,
                    section.Foldout,
                    section.GetLabel(),
                    contentHeight,
                    out content,
                    createHeaderMenu,
                    drawHeader,
                    headerWidth,
                    section.Actions.ScopeProperty);
            }
            else
            {
                drawn = GUIHelper.DrawShurikenToggleSection(
                    sectionPosition,
                    section.Foldout,
                    section.Toggle,
                    section.GetLabel(),
                    contentHeight,
                    out content,
                    createHeaderMenu,
                    drawHeader,
                    headerWidth,
                    section.Actions.ScopeProperty);
            }
            if (drawn)
            {
                content.height = GUIHelper.LineHeight;
                using var disabled = new EditorGUI.DisabledScope(sectionDisabled);
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
