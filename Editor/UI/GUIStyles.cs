namespace Aoyon.FaceTune.Gui;

internal static class GUIStyles
{
    private static GUIStyle? _sectionLayout;
    private static GUIStyle? _sectionHeader;
    private static GUIStyle? _toggleSectionHeader;
    private static GUIStyle? _sectionHeaderPopupLabel;
    private static GUIStyle? _sectionHeaderPopupCenteredLabel;
    private static GUIStyle? _simpleToggle;
    private static GUIStyle? _listButton;
    private static GUIStyle? _placeholderText;
    private static GUIStyle? _placeholderObject;

    public static GUIStyle SectionLayout => _sectionLayout ??= new GUIStyle
    {
        margin = new RectOffset(0, 0, GUIHelper.SectionHeaderVerticalMargin, GUIHelper.SectionHeaderVerticalMargin)
    };

    public static GUIStyle SectionHeader => _sectionHeader ??= new GUIStyle("ShurikenModuleTitle")
    {
        font = EditorStyles.label.font,
        border = new RectOffset(15, 7, 4, 4),
        margin = new RectOffset(0, 0, GUIHelper.SectionHeaderVerticalMargin, GUIHelper.SectionHeaderVerticalMargin),
        fixedHeight = GUIHelper.ShurikenHeaderHeight,
        contentOffset = new Vector2(GUIHelper.SectionHeaderContentOffsetX, -2f),
        fontSize = 12,
        normal = { textColor = new Color(1f, 1f, 1f, 0.9f) }
    };

    public static GUIStyle ToggleSectionHeader => _toggleSectionHeader ??= new GUIStyle(SectionHeader)
    {
        contentOffset = new Vector2(GUIHelper.SectionHeaderContentOffsetX + GUIHelper.LineHeight, -2f)
    };

    public static GUIStyle SectionHeaderPopupLabel
    {
        get
        {
            if (_sectionHeaderPopupLabel != null) return _sectionHeaderPopupLabel;
            _sectionHeaderPopupLabel = new GUIStyle(SectionHeader)
            {
                border = new RectOffset(),
                margin = new RectOffset(),
                fixedHeight = 0f,
                contentOffset = new Vector2(0f, SectionHeader.contentOffset.y)
            };
            ClearBackgrounds(_sectionHeaderPopupLabel);
            return _sectionHeaderPopupLabel;
        }
    }

    public static GUIStyle SectionHeaderPopupCenteredLabel
        => _sectionHeaderPopupCenteredLabel ??= new GUIStyle(SectionHeaderPopupLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };

    public static GUIStyle SimpleToggle => _simpleToggle ??= new GUIStyle(EditorStyles.miniButton)
    {
        alignment = TextAnchor.MiddleCenter,
        contentOffset = Vector2.zero,
        padding = new RectOffset()
    };

    public static GUIStyle ListButton => _listButton ??= new GUIStyle(EditorStyles.miniButton)
    {
        margin = new RectOffset(),
        overflow = new RectOffset(),
        fixedWidth = 0f,
        fixedHeight = 0f,
        stretchWidth = true,
        stretchHeight = true
    };

    public static GUIStyle PlaceholderText
        => _placeholderText ??= CreatePlaceholder(EditorStyles.textField);

    public static GUIStyle PlaceholderObject
    {
        get
        {
            if (_placeholderObject != null) return _placeholderObject;
            var source = EditorStyles.objectField;
            _placeholderObject = CreatePlaceholder(source);
            _placeholderObject.padding = new RectOffset(
                source.padding.left,
                0,
                source.padding.top,
                source.padding.bottom);
            return _placeholderObject;
        }
    }

    private static GUIStyle CreatePlaceholder(GUIStyle source)
    {
        var color = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, .45f)
            : new Color(0f, 0f, 0f, .45f);
        var style = new GUIStyle(source);
        ClearBackgrounds(style);
        style.normal.textColor = color;
        style.hover.textColor = color;
        style.focused.textColor = color;
        style.active.textColor = color;
        return style;
    }

    private static void ClearBackgrounds(GUIStyle style)
    {
        style.normal.background = null;
        style.hover.background = null;
        style.active.background = null;
        style.focused.background = null;
        style.onNormal.background = null;
        style.onHover.background = null;
        style.onActive.background = null;
        style.onFocused.background = null;
    }
}

