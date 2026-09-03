namespace Aoyon.FaceTune.Gui;

/// <summary>Mutating operations for a vertical IMGUI layout cursor.</summary>
internal static partial class GUIHelper
{
    public const float ContentPadding = 4f;
    public const float IndentWidth = 15f;
    public const float NestedSectionIndent = 0.25f;
    public static float LineHeight => EditorGUIUtility.singleLineHeight;
    public static float HorizontalSpacing => EditorGUIUtility.standardVerticalSpacing;
    public static float VerticalSpacing => EditorGUIUtility.standardVerticalSpacing;

    public static float GetLinesHeight(int count)
        => count <= 0 ? 0f : LineHeight * count + VerticalSpacing * (count - 1);

    private const float DefaultSpace = 6f;

    public static Rect SetSingleHeight(this ref Rect rect)
    {
        rect.height = LineHeight;
        return rect;
    }

    public static Rect SetHeight(this ref Rect rect, float height)
    {
        rect.height = height;
        return rect;
    }

    public static Rect NewLine(this ref Rect rect)
    {
        rect.y = rect.yMax + VerticalSpacing;
        return rect;
    }

    public static Rect Space(this ref Rect rect, float amount = DefaultSpace)
    {
        rect.y += amount + VerticalSpacing;
        return rect;
    }

    public static Rect Indent(this ref Rect rect, float count = 1f)
    {
        var offset = IndentWidth * count;
        rect.x += offset;
        rect.width = Mathf.Max(0f, rect.width - offset);
        return rect;
    }

    // Unity's IMGUI buttons and sliders consume right-button MouseDown events.
    // Keep their control IDs, but let the following ContextClick reach PropertyScope.
    // Prefab overrideの対象範囲だけを登録し、後続の独自UIへPropertyScopeの表示状態を持ち込まない。
    internal static void RegisterPropertyRegion(Rect position, SerializedProperty property)
    {
        using var scope = new EditorGUI.PropertyScope(position, GUIContent.none, property);
    }

    internal readonly struct RightClickPassthroughScope : IDisposable
    {
        private readonly Event _event;
        private readonly EventType _originalType;
        private readonly bool _changed;

        internal RightClickPassthroughScope(Rect position)
        {
            _event = Event.current;
            _originalType = _event.type;
            _changed = _event.type == EventType.MouseDown
                       && (_event.button == 1
                           || (Application.platform == RuntimePlatform.OSXEditor
                               && _event.button == 0
                               && _event.control))
                       && position.Contains(_event.mousePosition);
            if (_changed) _event.type = EventType.Ignore;
        }

        public void Dispose()
        {
            if (_changed && _event.type == EventType.Ignore)
                _event.type = _originalType;
        }
    }

    public static Rect Back(this ref Rect rect, int count = 1)
    {
        var offset = IndentWidth * count;
        rect.x -= offset;
        rect.width += offset;
        return rect;
    }

    public static (Rect left, Rect right) SplitLeft(this Rect source, float width)
    {
        width = Mathf.Clamp(width, 0f, source.width);
        var left = new Rect(source.x, source.y, width, source.height);
        var rightX = Mathf.Min(source.xMax, left.xMax + HorizontalSpacing);
        return (left, new Rect(rightX, source.y, Mathf.Max(0f, source.xMax - rightX), source.height));
    }

    public static (Rect left, Rect right) SplitRight(this Rect source, float width)
    {
        width = Mathf.Clamp(width, 0f, source.width);
        var right = new Rect(source.xMax - width, source.y, width, source.height);
        var leftWidth = Mathf.Max(0f, right.x - HorizontalSpacing - source.x);
        return (new Rect(source.x, source.y, leftWidth, source.height), right);
    }

    public static (Rect left, Rect right) SplitRatio(this Rect source, float ratio)
        => source.SplitLeft(source.width * Mathf.Clamp01(ratio));

    /// <summary>
    /// Preferred widthsを基準に、全要素を同じ比率で伸縮して横一列へ配置する。
    /// </summary>
    public static Rect[] FlexHorizontal(this Rect source, params float[] preferredWidths)
        => FlexHorizontal(source, 0f, preferredWidths);

    public static Rect[] FlexHorizontalSpaced(
        this Rect source,
        float spacing,
        params float[] preferredWidths)
        => FlexHorizontal(source, Mathf.Max(0f, spacing), preferredWidths);

    private static Rect[] FlexHorizontal(Rect source, float spacing, float[] preferredWidths)
    {
        if (preferredWidths.Length == 0) return Array.Empty<Rect>();

        var sourceWidth = Mathf.Max(0f, source.width);
        var gapCount = preferredWidths.Length - 1;
        if (gapCount > 0) spacing = Mathf.Min(spacing, sourceWidth / gapCount);
        var availableWidth = Mathf.Max(0f, sourceWidth - spacing * gapCount);
        var totalPreferredWidth = preferredWidths.Sum(width => Mathf.Max(0f, width));
        var scale = totalPreferredWidth > 0f ? availableWidth / totalPreferredWidth : 0f;
        var result = new Rect[preferredWidths.Length];
        var x = source.x;
        for (var i = 0; i < result.Length; i++)
        {
            var width = i == result.Length - 1
                ? Mathf.Max(0f, source.x + sourceWidth - x)
                : totalPreferredWidth > 0f
                    ? Mathf.Max(0f, preferredWidths[i]) * scale
                    : availableWidth / result.Length;
            result[i] = new Rect(x, source.y, width, source.height);
            x += width + spacing;
        }
        return result;
    }

}

/// <summary>Foldouts whose drawing and hit area stay inside the supplied rectangle.</summary>
internal static partial class GUIHelper
{
    public static bool DrawFoldout(Rect position, bool expanded, GUIContent label, bool toggleOnLabelClick = true)
    {
        if (Event.current.type == EventType.Repaint)
            EditorStyles.foldout.Draw(position, label, false, false, expanded, false);

        var arrowWidth = EditorStyles.foldout.CalcSize(GUIContent.none).x;
        var hitRect = position;
        if (!toggleOnLabelClick) hitRect.width = arrowWidth;

        var current = Event.current;
        if (current.type == EventType.MouseDown && current.button == 0 && hitRect.Contains(current.mousePosition))
        {
            expanded = !expanded;
            current.Use();
            GUI.changed = true;
        }

        return expanded;
    }

}

internal static partial class GUIHelper
{
    internal const float ShurikenHeaderHeight = 22f;
    internal const int SectionHeaderVerticalMargin = 0;
    internal const float SectionHeaderContentOffsetX = 20f;
    private const float SectionHeaderArrowSize = 13f;
    private const float SectionHeaderArrowInsetX = 4f;
    private const float SectionHeaderArrowInsetY = 2f;
    private const float SectionHeaderControlEdgeMargin = 2f;

    // Heuristic optical correction for the standard Toggle drawn inside a Shuriken header.
    // IMGUI does not expose the visual bounds of the Toggle glyph within its control Rect.
    private static readonly Vector2 HeaderToggleVisualOffset = Vector2.zero;
    private const float HeaderMenuIconVisualOffsetY = -1f;
    internal static GUIStyle ShurikenLayoutStyle => GUIStyles.SectionLayout;
    internal static GUIStyle ShurikenStyle => GUIStyles.SectionHeader;

    public static bool DrawShuriken(Rect position, bool expanded, GUIContent label)
    {
        GUI.Box(position, label, ShurikenStyle);
        return HandleFoldout(position, expanded);
    }

    public static bool DrawSimpleToggle(Rect position, bool value, GUIContent label)
    {
        var previousBackgroundColor = GUI.backgroundColor;
        if (value) GUI.backgroundColor = new Color(0.55f, 0.75f, 0.9f);
        if (GUI.Button(position, label, GUIStyles.SimpleToggle)) value = !value;
        GUI.backgroundColor = previousBackgroundColor;
        return value;
    }

    public static float GetShurikenSectionHeight(bool expanded, float contentHeight)
        => ShurikenHeaderHeight
         + (expanded
             ? ContentSpacing + ContentBottomSpacing + ContentPadding * 2f + contentHeight
             : 0f);

    public static float GetShurikenSectionHeight(FoldoutState state, float contentHeight)
        => GetShurikenSectionHeight(state.Expanded, contentHeight);

    public static bool DrawShurikenSection(
        Rect position,
        FoldoutState state,
        GUIContent label,
        float contentHeight,
        out Rect content,
        Func<GenericMenu>? createHeaderMenu = null,
        Action<Rect>? drawHeader = null,
        float headerWidth = 0f,
        SerializedProperty? propertyScope = null)
        => DrawShurikenSection(position, ref state.Expanded, label, contentHeight, out content, createHeaderMenu, drawHeader, headerWidth, propertyScope);

    public static bool DrawShurikenSection(
        Rect position,
        ref bool expanded,
        GUIContent label,
        float contentHeight,
        out Rect content,
        Func<GenericMenu>? createHeaderMenu = null,
        Action<Rect>? drawHeader = null,
        float headerWidth = 0f,
        SerializedProperty? propertyScope = null)
    {
        var header = new Rect(position.x, position.y, position.width, ShurikenHeaderHeight);
        DrawShurikenHeader(
            header,
            label,
            ShurikenStyle,
            createHeaderMenu,
            drawHeader,
            headerWidth,
            propertyScope,
            out var menuButton,
            out var headerControl);
        expanded = HandleFoldout(header, expanded, menuButton, headerControl);
        return DrawShurikenSectionContent(position, header, expanded, contentHeight, out content);
    }

    internal static bool DrawShurikenToggleSection(
        Rect position,
        FoldoutState state,
        SectionToggle toggle,
        GUIContent label,
        float contentHeight,
        out Rect content,
        Func<GenericMenu>? createHeaderMenu = null,
        Action<Rect>? drawHeader = null,
        float headerWidth = 0f,
        SerializedProperty? propertyScope = null)
    {
        var header = new Rect(position.x, position.y, position.width, ShurikenHeaderHeight);
        state.Expanded = DrawShurikenToggleAndFold(
            header,
            state.Expanded,
            toggle,
            label,
            createHeaderMenu,
            drawHeader,
            headerWidth,
            propertyScope);
        return DrawShurikenSectionContent(
            position,
            header,
            state.Expanded,
            contentHeight,
            out content);
    }

    private static bool DrawShurikenSectionContent(
        Rect position,
        Rect header,
        bool expanded,
        float contentHeight,
        out Rect content)
    {
        if (!expanded)
        {
            content = Rect.zero;
            return false;
        }

        var region = new Rect(
            position.x,
            header.yMax + ContentSpacing,
            position.width,
            ContentPadding * 2f + contentHeight);
        if (Event.current.type == EventType.Repaint) DrawRegion(region);
        content = new Rect(
            region.x + ContentPadding,
            region.y + ContentPadding,
            region.width - ContentPadding * 2f,
            contentHeight);
        return true;
    }


    private static bool DrawShurikenToggleAndFold(
        Rect position,
        bool expanded,
        SectionToggle toggle,
        GUIContent label,
        Func<GenericMenu>? createHeaderMenu,
        Action<Rect>? drawHeader,
        float headerWidth,
        SerializedProperty? propertyScope)
    {
        DrawShurikenHeader(
            position,
            label,
            GUIStyles.ToggleSectionHeader,
            createHeaderMenu,
            drawHeader,
            headerWidth,
            propertyScope,
            out var menuButton,
            out var headerControl);

        var toggleRect = new Rect(
            position.x + SectionHeaderContentOffsetX + HeaderToggleVisualOffset.x,
            position.center.y - LineHeight * .5f + HeaderToggleVisualOffset.y,
            LineHeight,
            LineHeight);
        var state = toggle.GetState();
        var previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = state.Mixed;
        using (new EditorGUI.DisabledScope(!toggle.Editable))
        {
            EditorGUI.BeginChangeCheck();
            var enabled = EditorGUI.Toggle(toggleRect, state.Enabled);
            if (EditorGUI.EndChangeCheck()) toggle.SetEnabled(enabled);
        }
        EditorGUI.showMixedValue = previousMixed;
        return HandleFoldout(position, expanded, toggleRect, menuButton, headerControl);
    }


    private static void DrawShurikenHeader(
        Rect position,
        GUIContent label,
        GUIStyle style,
        Func<GenericMenu>? createHeaderMenu,
        Action<Rect>? drawHeader,
        float headerWidth,
        SerializedProperty? propertyScope,
        out Rect menuButton,
        out Rect headerControl)
    {
        if (propertyScope == null)
        {
            DrawShurikenHeaderContent(
                position,
                label,
                style,
                createHeaderMenu,
                drawHeader,
                headerWidth,
                out menuButton,
                out headerControl);
            return;
        }

        GUIHelper.RegisterPropertyRegion(position, propertyScope);
        DrawShurikenHeaderContent(
            position,
            label,
            style,
            createHeaderMenu,
            drawHeader,
            headerWidth,
            out menuButton,
            out headerControl);
    }

    private static void DrawShurikenHeaderContent(
        Rect position,
        GUIContent label,
        GUIStyle style,
        Func<GenericMenu>? createHeaderMenu,
        Action<Rect>? drawHeader,
        float headerWidth,
        out Rect menuButton,
        out Rect headerControl)
    {
        using var rightClick = new RightClickPassthroughScope(position);
        GUI.Box(position, label, style);
        menuButton = DrawHeaderMenu(position, createHeaderMenu);
        headerControl = DrawHeaderControl(position, menuButton, drawHeader, headerWidth);
    }

    private static Rect DrawHeaderMenu(Rect header, Func<GenericMenu>? createHeaderMenu)
    {
        if (createHeaderMenu == null) return Rect.zero;
        var button = new Rect(header.xMax - ShurikenHeaderHeight, header.y, ShurikenHeaderHeight, header.height);
        var content = EditorGUIUtility.IconContent("_Menu");
        content.tooltip = "common.menu.tooltip".LS();
        var image = content.image;
        var pixelsPerPoint = Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
        var iconSize = new Vector2(
            image.width / pixelsPerPoint,
            image.height / pixelsPerPoint);
        var scale = Mathf.Min(1f, LineHeight / Mathf.Max(iconSize.x, iconSize.y));
        iconSize *= scale;
        var icon = new Rect(
            button.center.x - iconSize.x * .5f,
            button.center.y - iconSize.y * .5f + HeaderMenuIconVisualOffsetY,
            iconSize.x,
            iconSize.y);
        if (GUI.Button(button, GUIContent.none, GUIStyle.none)) createHeaderMenu().DropDown(button);
        GUI.DrawTexture(icon, image);
        return button;
    }

    private static Rect DrawHeaderControl(Rect header, Rect menuButton, Action<Rect>? drawHeader, float width)
    {
        if (drawHeader == null || width <= 0f) return Rect.zero;
        var hasMenu = menuButton.width > 0f;
        var right = hasMenu ? menuButton.x : header.xMax;
        var rightMargin = hasMenu ? 0f : SectionHeaderControlEdgeMargin;
        var control = new Rect(
            right - rightMargin - width,
            header.y,
            width,
            header.height);
        drawHeader(control);
        return control;
    }

    private static bool HandleFoldout(Rect position, bool expanded, params Rect[] excluded)
    {
        var arrow = new Rect(
            position.x + SectionHeaderArrowInsetX,
            position.y + SectionHeaderArrowInsetY,
            SectionHeaderArrowSize,
            SectionHeaderArrowSize);
        DrawFoldoutArrow(arrow, expanded);

        var current = Event.current;
        if (current.type == EventType.MouseDown && current.button == 0
            && position.Contains(current.mousePosition)
            && !excluded.Any(rect => rect.Contains(current.mousePosition)))
        {
            expanded = !expanded;
            current.Use();
        }
        return expanded;
    }

    private static void DrawFoldoutArrow(Rect position, bool expanded)
    {
        if (Event.current.type == EventType.Repaint)
            EditorStyles.foldout.Draw(position, false, false, expanded, false);
    }
}

internal static partial class GUIHelper
{
    internal const float HeaderSpacing = 0f;
    internal const float ContentSpacing = 0f;

    // Heuristic compensation for the visually heavier bottom edge of ShurikenModuleTitle.
    internal const float ContentBottomVisualCompensation = 3f;
    internal const float ContentBottomSpacing = ContentSpacing + ContentBottomVisualCompensation;
    private const float OutlineWidth = 1f;
    private static readonly Color DarkBackground = new(0.22f, 0.22f, 0.22f, 1f);
    private static readonly Color LightBackground = new(0.76f, 0.76f, 0.76f, 1f);
    private static readonly Color DarkOutline = new(0.36f, 0.36f, 0.36f, 1f);
    private static readonly Color LightOutline = new(0.52f, 0.52f, 0.52f, 1f);


    public static void DrawPlaceholderTextField(
        Rect position,
        SerializedProperty property,
        GUIContent label,
        GUIContent placeholder,
        bool indentLabel = false)
    {
        GUIHelper.RegisterPropertyRegion(position, property);
        using var rightClick = new RightClickPassthroughScope(position);
        var (labelPosition, valuePosition) = SplitIndentedLabel(position);
        var field = indentLabel ? valuePosition : EditorGUI.PrefixLabel(position, label);
        if (indentLabel) EditorGUI.LabelField(labelPosition, label);
        EditorGUI.PropertyField(field, property, GUIContent.none);
        if (!string.IsNullOrEmpty(property.stringValue)) return;
        GUI.Label(field, placeholder, GUIStyles.PlaceholderText);
    }

    public static void DrawPlaceholderObjectField(
        Rect position,
        SerializedProperty property,
        GUIContent label,
        GUIContent placeholder)
    {
        GUIHelper.RegisterPropertyRegion(position, property);
        using var rightClick = new RightClickPassthroughScope(position);
        var field = EditorGUI.PrefixLabel(position, label);
        EditorGUI.PropertyField(field, property, GUIContent.none);
        if (property.objectReferenceValue != null) return;
        DrawObjectPlaceholder(field, placeholder);
    }

    public static void DrawPlaceholderObjectLikeField(
        Rect position,
        SerializedProperty property,
        GUIContent label,
        GUIContent placeholder,
        bool isEmpty,
        bool indentLabel = false)
    {
        GUIHelper.RegisterPropertyRegion(position, property);
        using var rightClick = new RightClickPassthroughScope(position);
        var (labelPosition, valuePosition) = SplitIndentedLabel(position);
        var field = indentLabel ? valuePosition : EditorGUI.PrefixLabel(position, label, EditorStyles.label);
        if (indentLabel) EditorGUI.LabelField(labelPosition, label);
        EditorGUI.PropertyField(field, property, GUIContent.none, true);
        if (!isEmpty) return;
        DrawObjectPlaceholder(field, placeholder);
    }

    private static void DrawObjectPlaceholder(Rect field, GUIContent placeholder)
    {
        // Redraw only the null presentation. The original field remains responsible
        // for picking, drag-and-drop, keyboard focus, Undo, and prefab overrides.
        // ObjectFieldの背景は半透明なので、標準のNone表記を先に塗りつぶす。
        EditorGUI.DrawRect(field, EditorGUIUtility.isProSkin ? DarkBackground : LightBackground);
        GUI.Box(field, GUIContent.none, EditorStyles.objectField);
        var buttonStyle = GUI.skin.FindStyle("ObjectFieldButton") ?? EditorStyles.miniButton;
        var buttonWidth = buttonStyle.fixedWidth > 0f ? buttonStyle.fixedWidth : LineHeight;
        var button = new Rect(field.xMax - buttonWidth, field.y, buttonWidth, field.height);
        GUI.Box(button, GUIContent.none, buttonStyle);

        var text = new Rect(field.x, field.y, Mathf.Max(0f, field.width - buttonWidth), field.height);
        GUI.Label(text, placeholder, GUIStyles.PlaceholderObject);
    }

    public static void DrawToggleLeft(Rect position, SerializedProperty property, GUIContent label)
    {
        GUIHelper.RegisterPropertyRegion(position, property);
        using var rightClick = new RightClickPassthroughScope(position);
        var previousMixedValue = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        var value = EditorGUI.ToggleLeft(position, label, property.boolValue);
        if (EditorGUI.EndChangeCheck()) property.boolValue = value;
        EditorGUI.showMixedValue = previousMixedValue;
    }

    public static bool DrawToggleLeft(Rect position, bool value, GUIContent label)
        => EditorGUI.ToggleLeft(position, label, value);

    public static void DrawRegion(Rect position)
    {
        var background = EditorGUIUtility.isProSkin ? DarkBackground : LightBackground;
        var outline = EditorGUIUtility.isProSkin ? DarkOutline : LightOutline;
        EditorGUI.DrawRect(position, background);
        EditorGUI.DrawRect(new Rect(position.x, position.y, position.width, OutlineWidth), outline);
        EditorGUI.DrawRect(new Rect(position.x, position.yMax - OutlineWidth, position.width, OutlineWidth), outline);
        EditorGUI.DrawRect(new Rect(position.x, position.y, OutlineWidth, position.height), outline);
        EditorGUI.DrawRect(new Rect(position.xMax - OutlineWidth, position.y, OutlineWidth, position.height), outline);
    }
}


internal enum PopupPresentation
{
    Standard,
    Compact
}

/// <summary>Stateless localized wrappers around Unity's IMGUI API.</summary>
internal static partial class GUIHelper
{
    private const float CompactPopupArrowSpacing = 4f;
    private const float CompactPopupArrowWidth = 8f;
    private const float CompactPopupArrowHeight = 5f;
    private const float CompactPopupArrowVisualOffsetY = -1f;
    private const float CompactPopupTrailingWidth = CompactPopupArrowWidth + CompactPopupArrowSpacing;
    private static readonly Color CompactPopupArrowColor = new(1f, 1f, 1f, 0.75f);
    private static readonly GUIContent IndentedLabelPlaceholder = new(" ");

    public static float PopupWidth(
        GUIContent content,
        PopupPresentation presentation = PopupPresentation.Standard)
        => presentation == PopupPresentation.Compact
            ? EditorStyles.label.CalcSize(content).x + CompactPopupTrailingWidth
            : EditorStyles.popup.CalcSize(content).x;

    public static float MaxPopupWidth(
        IEnumerable<GUIContent> contents,
        PopupPresentation presentation = PopupPresentation.Standard)
        => contents.Max(content => PopupWidth(content, presentation));

    public static float LocalizedPopupWidth(
        string optionKey,
        PopupPresentation presentation = PopupPresentation.Standard)
        => PopupWidth(optionKey.LG(), presentation);

    public static float MaxLocalizedPopupWidth(
        IEnumerable<string> optionKeys,
        PopupPresentation presentation = PopupPresentation.Standard)
        => MaxPopupWidth(optionKeys.Select(key => key.LG()), presentation);

    public static float LocalizedEnumPopupWidth(SerializedProperty property, string typeName)
    {
        var optionPrefix = char.ToLowerInvariant(typeName[0]) + typeName[1..];
        return MaxLocalizedPopupWidth(property.enumNames.Select(name =>
            $"{optionPrefix}.option.{char.ToLowerInvariant(name[0]) + name[1..]}"));
    }

    public static void LocalizedPropertyField(Rect position, SerializedProperty property, string key, bool includeChildren = true)
        => EditorGUI.PropertyField(position, property, key.LG(), includeChildren);

    public static int LocalizedPopup(Rect position, int selectedIndex, string? labelKey, IEnumerable<string> optionKeys)
        => EditorGUI.Popup(
            position,
            labelKey == null ? GUIContent.none : labelKey.LG(),
            selectedIndex,
            optionKeys.Select(key => key.LG()).ToArray());

    public static bool OptionalListEnabled(SerializedProperty list)
        => list.hasMultipleDifferentValues || list.arraySize > 0;

    /// <summary>
    /// 空配列を無効、要素のある配列を有効として編集する二択Popup。
    /// 状態はUI上だけで表現し、シリアライズするフラグを増やさない。
    /// </summary>
    public static bool LocalizedOptionalListPopup(
        Rect position,
        SerializedProperty list,
        GUIContent label,
        string disabledOptionKey,
        string enabledOptionKey,
        Action<SerializedProperty> initializeElement)
    {
        GUIHelper.RegisterPropertyRegion(position, list);
        using var rightClick = new RightClickPassthroughScope(position);
        var enabled = OptionalListEnabled(list);
        var previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = list.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        var next = EditorGUI.Popup(
            position,
            label,
            enabled ? 1 : 0,
            new[] { disabledOptionKey.LG(), enabledOptionKey.LG() });
        var changed = EditorGUI.EndChangeCheck();
        EditorGUI.showMixedValue = previousMixed;
        if (changed)
        {
            if (next == 0)
                list.ClearArray();
            else if (list.arraySize == 0)
            {
                list.InsertArrayElementAtIndex(0);
                initializeElement(list.GetArrayElementAtIndex(0));
            }
        }
        return next != 0;
    }

    public static float CompactPopupWidth(IEnumerable<GUIContent> labels)
        => labels.Max(label => GUIStyles.SectionHeaderPopupLabel.CalcSize(label).x)
         + CompactPopupTrailingWidth;

    public static void CompactHeaderValue(
        Rect position,
        GUIContent value,
        bool mixed = false,
        bool centered = false,
        GUIStyle? labelStyle = null)
    {
        var previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = mixed;
        if (centered) position.width = Mathf.Max(0f, position.width - CompactPopupTrailingWidth);
        GUI.Label(
            position,
            value,
            labelStyle ?? (centered
                ? GUIStyles.SectionHeaderPopupCenteredLabel
                : GUIStyles.SectionHeaderPopupLabel));
        EditorGUI.showMixedValue = previousMixed;
    }

    public static void CompactPopup(
        Rect position,
        GUIContent current,
        IReadOnlyList<GUIContent> options,
        int selectedIndex,
        Action<int> select,
        bool mixed = false,
        int separatorBefore = -1,
        bool centered = false,
        GUIStyle? labelStyle = null)
    {
        var opened = GUI.Button(position, GUIContent.none, GUIStyle.none);
        CompactHeaderValue(position, current, mixed, centered, labelStyle);
        DrawCompactPopupArrow(position);
        if (!opened) return;

        var menu = new GenericMenu();
        for (var i = 0; i < options.Count; i++)
        {
            if (i == separatorBefore)
            {
                menu.AddSeparator(string.Empty);
            }

            var index = i;
            menu.AddItem(options[i], i == selectedIndex, () => select(index));
        }
        menu.DropDown(position);
    }

    private static void DrawCompactPopupArrow(Rect position)
    {
        if (Event.current.type != EventType.Repaint) return;

        var center = new Vector3(
            position.xMax - SectionHeaderArrowSize * .5f,
            position.center.y + CompactPopupArrowVisualOffsetY);
        var halfWidth = CompactPopupArrowWidth * .5f;
        var halfHeight = CompactPopupArrowHeight * .5f;
        var previousColor = Handles.color;

        Handles.BeginGUI();
        Handles.color = CompactPopupArrowColor;
        Handles.DrawAAConvexPolygon(
            center + new Vector3(-halfWidth, -halfHeight),
            center + new Vector3(halfWidth, -halfHeight),
            center + new Vector3(0f, halfHeight));
        Handles.color = previousColor;
        Handles.EndGUI();
    }

    public static void DrawProperty(
        ref Rect position,
        SerializedProperty property,
        string labelKey,
        bool includeChildren = true)
    {
        position.height = EditorGUI.GetPropertyHeight(property, includeChildren);
        RegisterPropertyRegion(position, property);
        using (new RightClickPassthroughScope(position))
        {
            LocalizedPropertyField(position, property, labelKey, includeChildren);
        }
        position.NewLine();
    }

    public static float PropertyHeight(SerializedProperty property, bool includeChildren = true)
        => EditorGUI.GetPropertyHeight(property, includeChildren) + VerticalSpacing;

    public static void DrawLocalizedEnum(
        ref Rect position,
        SerializedProperty property,
        string labelKey,
        string typeName)
    {
        position.height = LineHeight;
        DrawLocalizedEnum(position, property, labelKey, typeName);
        position.NewLine();
    }

    public static void DrawLocalizedEnum(
        Rect position,
        SerializedProperty property,
        string labelKey,
        string typeName)
    {
        var optionPrefix = char.ToLowerInvariant(typeName[0]) + typeName[1..];
        var optionKeys = property.enumNames.Select(name =>
            $"{optionPrefix}.option.{char.ToLowerInvariant(name[0]) + name[1..]}");
        LocalizedEnumPopup(position, property, labelKey, optionKeys);
    }

    public static (Rect Label, Rect Value) SplitLabel(Rect position)
    {
        var value = EditorGUI.PrefixLabel(position, IndentedLabelPlaceholder);
        return (
            new Rect(position.x, position.y, Mathf.Max(0f, value.x - position.x), position.height),
            value);
    }

    public static (Rect Label, Rect Value) SplitIndentedLabel(Rect position)
    {
        var (label, value) = SplitLabel(position);
        var labelX = position.x + IndentWidth;
        return (
            new Rect(labelX, label.y, Mathf.Max(0f, value.x - labelX), label.height),
            value);
    }

    public static void DrawPropertyWithIndentedLabel(
        ref Rect position,
        SerializedProperty property,
        string labelKey,
        bool includeChildren = true)
    {
        position.height = EditorGUI.GetPropertyHeight(property, GUIContent.none, includeChildren);
        var content = labelKey.LG();
        GUIHelper.RegisterPropertyRegion(position, property);
        using var rightClick = new RightClickPassthroughScope(position);
        var (label, value) = SplitIndentedLabel(position);
        EditorGUI.LabelField(label, content);
        EditorGUI.PropertyField(value, property, GUIContent.none, includeChildren);
        position.NewLine();
    }

    public static void LocalizedEnumPopup(
        Rect position,
        SerializedProperty property,
        string labelKey,
        IEnumerable<string> optionKeys,
        PopupPresentation presentation = PopupPresentation.Standard)
    {
        var keys = optionKeys as IReadOnlyList<string> ?? optionKeys.ToArray();
        RegisterPropertyRegion(position, property);
        using var rightClick = new RightClickPassthroughScope(position);
        var previousMixedValue = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        int next;
        if (presentation == PopupPresentation.Compact)
        {
            next = EditorGUI.Popup(
                position,
                property.enumValueIndex,
                keys.Select(key => key.LG()).ToArray(),
                EditorStyles.label);
            DrawCompactPopupArrow(position);
        }
        else
        {
            next = LocalizedPopup(
                position,
                property.enumValueIndex,
                string.IsNullOrEmpty(labelKey) ? null : labelKey,
                keys);
        }
        if (next != property.enumValueIndex) property.enumValueIndex = next;
        EditorGUI.showMixedValue = previousMixedValue;
    }
}
