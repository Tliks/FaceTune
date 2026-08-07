namespace Aoyon.FaceTune.Gui;

/// <summary>Mutating operations for a vertical IMGUI layout cursor.</summary>
internal static partial class GUIHelper
{
    public const float ContentPadding = 4f;
    public const float IndentWidth = 15f;
    public static float LineHeight => EditorGUIUtility.singleLineHeight;
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

    public static Rect Indent(this ref Rect rect, int count = 1)
    {
        var offset = IndentWidth * count;
        rect.x += offset;
        rect.width = Mathf.Max(0f, rect.width - offset);
        return rect;
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
        var rightX = Mathf.Min(source.xMax, left.xMax + VerticalSpacing);
        return (left, new Rect(rightX, source.y, Mathf.Max(0f, source.xMax - rightX), source.height));
    }

    public static (Rect left, Rect right) SplitRight(this Rect source, float width)
    {
        width = Mathf.Clamp(width, 0f, source.width);
        var right = new Rect(source.xMax - width, source.y, width, source.height);
        var leftWidth = Mathf.Max(0f, right.x - VerticalSpacing - source.x);
        return (new Rect(source.x, source.y, leftWidth, source.height), right);
    }

    public static (Rect left, Rect right) SplitRatio(this Rect source, float ratio)
        => source.SplitLeft(source.width * Mathf.Clamp01(ratio));
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

    public static bool DrawFoldout(Rect position, SerializedProperty property, GUIContent label, bool toggleOnLabelClick = true)
    {
        property.isExpanded = DrawFoldout(position, property.isExpanded, label, toggleOnLabelClick);
        return property.isExpanded;
    }
}

internal static partial class GUIHelper
{
    internal const float ShurikenHeaderHeight = 22f;
    private const int HeaderVerticalMargin = 0;
    private const float HeaderContentOffsetX = 20f;
    private const float HeaderToggleInset = 4f;

    // Heuristic optical correction for the standard Toggle drawn inside a Shuriken header.
    // IMGUI does not expose the visual bounds of the Toggle glyph within its control Rect.
    private static readonly Vector2 HeaderToggleVisualOffset = new(-1f, -1f);
    private const float HeaderMenuIconVisualOffsetY = -1f;
    private static GUIStyle? _style;
    private static GUIStyle? _toggleAndFoldStyle;
    private static GUIStyle? _shurikenLayoutStyle;
    private static GUIStyle? _simpleToggleStyle;
    internal static GUIStyle ShurikenLayoutStyle => _shurikenLayoutStyle ??= new GUIStyle
    {
        margin = new RectOffset(0, 0, HeaderVerticalMargin, HeaderVerticalMargin)
    };
    internal static GUIStyle ShurikenStyle => _style ??= new GUIStyle("ShurikenModuleTitle")
    {
        font = EditorStyles.label.font,
        border = new RectOffset(15, 7, 4, 4),
        margin = new RectOffset(0, 0, HeaderVerticalMargin, HeaderVerticalMargin),
        fixedHeight = ShurikenHeaderHeight,
        contentOffset = new Vector2(HeaderContentOffsetX, -2f),
        fontSize = 12,
        normal = { textColor = new Color(1f, 1f, 1f, 0.9f) }
    };

    public static bool DrawShuriken(Rect position, bool expanded, GUIContent label)
    {
        GUI.Box(position, label, ShurikenStyle);
        return HandleFoldout(position, expanded);
    }

    public static bool DrawSimpleToggle(Rect position, bool value, GUIContent label)
    {
        _simpleToggleStyle ??= new GUIStyle(EditorStyles.miniButton)
        {
            alignment = TextAnchor.MiddleCenter,
            contentOffset = Vector2.zero,
            padding = new RectOffset()
        };
        var previousContentColor = GUI.contentColor;
        GUI.contentColor = value
            ? new Color(0.55f, 0.75f, 0.9f)
            : new Color(1f, 1f, 1f, 0.7f);
        if (GUI.Button(position, label, _simpleToggleStyle)) value = !value;
        GUI.contentColor = previousContentColor;
        return value;
    }

    public static float GetShurikenSectionHeight(bool expanded, float contentHeight)
        => ShurikenHeaderHeight
         + (expanded
             ? ContentSpacing + ContentBottomSpacing + ContentPadding * 2f + contentHeight
             : 0f);

    public static bool DrawShurikenSection(
        Rect position,
        ref bool expanded,
        GUIContent label,
        float contentHeight,
        out Rect content,
        Func<GenericMenu>? createHeaderMenu = null)
    {
        var header = new Rect(position.x, position.y, position.width, ShurikenHeaderHeight);
        GUI.Box(header, label, ShurikenStyle);
        var menuButton = DrawHeaderMenu(header, createHeaderMenu);
        expanded = HandleFoldout(header, expanded, menuButton);
        return DrawShurikenSectionContent(position, header, expanded, contentHeight, out content);
    }

    internal static bool DrawShurikenToggleSection(
        Rect position,
        ref bool expanded,
        SerializedProperty enabled,
        GUIContent label,
        float contentHeight,
        out Rect content,
        Func<GenericMenu>? createHeaderMenu = null)
    {
        var header = new Rect(position.x, position.y, position.width, ShurikenHeaderHeight);
        expanded = DrawShurikenToggleAndFold(header, expanded, enabled, label, createHeaderMenu);
        return DrawShurikenSectionContent(position, header, expanded, contentHeight, out content);
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


    public static bool DrawShurikenToggleAndFold(
        Rect position,
        bool expanded,
        SerializedProperty enabled,
        GUIContent label,
        Func<GenericMenu>? createHeaderMenu = null)
    {
        _toggleAndFoldStyle ??= new GUIStyle(ShurikenStyle)
        {
            contentOffset = new Vector2(HeaderContentOffsetX + LineHeight, -2f)
        };
        GUI.Box(position, label, _toggleAndFoldStyle);
        var menuButton = DrawHeaderMenu(position, createHeaderMenu);

        var toggleRect = new Rect(
            position.x + HeaderContentOffsetX + HeaderToggleVisualOffset.x,
            position.center.y - LineHeight * .5f + HeaderToggleVisualOffset.y,
            LineHeight,
            LineHeight);
        using (new EditorGUI.PropertyScope(position, label, enabled))
        {
            var previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = enabled.hasMultipleDifferentValues;
            enabled.boolValue = EditorGUI.Toggle(toggleRect, enabled.boolValue);
            EditorGUI.showMixedValue = previousMixed;
        }
        return HandleFoldout(position, expanded, toggleRect, menuButton);
    }

    public static bool DrawShurikenToggle(Rect position, SerializedProperty enabled, GUIContent label)
    {
        // Use the exact same style and content offset as the foldout header.
        GUI.Box(position, label, ShurikenStyle);

        var toggleRect = new Rect(
            position.x + HeaderToggleInset + HeaderToggleVisualOffset.x,
            position.center.y - LineHeight * .5f + HeaderToggleVisualOffset.y,
            LineHeight,
            LineHeight);

        using (new EditorGUI.PropertyScope(position, label, enabled))
        {
            var previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = enabled.hasMultipleDifferentValues;
            enabled.boolValue = EditorGUI.Toggle(toggleRect, enabled.boolValue);
            EditorGUI.showMixedValue = previousMixed;
        }

        var current = Event.current;
        if (current.type == EventType.MouseDown
            && current.button == 0
            && position.Contains(current.mousePosition)
            && !toggleRect.Contains(current.mousePosition))
        {
            enabled.boolValue = !enabled.boolValue;
            current.Use();
            GUI.changed = true;
        }

        return enabled.boolValue || enabled.hasMultipleDifferentValues;
    }


    private static Rect DrawHeaderMenu(Rect header, Func<GenericMenu>? createHeaderMenu)
    {
        if (createHeaderMenu == null) return Rect.zero;
        var button = new Rect(header.xMax - ShurikenHeaderHeight, header.y, ShurikenHeaderHeight, header.height);
        var content = EditorGUIUtility.IconContent("_Menu");
        content.tooltip = "Menu";
        var image = content.image;
        var icon = new Rect(
            button.center.x - image.width * .5f,
            button.center.y - image.height * .5f + HeaderMenuIconVisualOffsetY,
            image.width,
            image.height);
        if (GUI.Button(button, GUIContent.none, GUIStyle.none)) createHeaderMenu().DropDown(button);
        GUI.DrawTexture(icon, image);
        return button;
    }

    private static bool HandleFoldout(Rect position, bool expanded, params Rect[] excluded)
    {
        var arrow = new Rect(position.x + 4f, position.y + 2f, 13f, 13f);
        if (Event.current.type == EventType.Repaint)
            EditorStyles.foldout.Draw(arrow, false, false, expanded, false);

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


    private static GUIStyle? _placeholderTextStyle;
    private static GUIStyle? _placeholderObjectStyle;

    private static GUIStyle PlaceholderTextStyle => _placeholderTextStyle ??= CreatePlaceholderStyle(EditorStyles.textField);
    private static GUIStyle PlaceholderObjectStyle => _placeholderObjectStyle ??= CreateObjectPlaceholderStyle();

    public static void DrawPlaceholderTextField(
        Rect position,
        SerializedProperty property,
        GUIContent label,
        GUIContent placeholder,
        bool indentLabel = false)
    {
        using var scope = new EditorGUI.PropertyScope(position, label, property);
        var (labelPosition, valuePosition) = SplitIndentedLabel(position);
        var field = indentLabel ? valuePosition : EditorGUI.PrefixLabel(position, scope.content);
        if (indentLabel) EditorGUI.LabelField(labelPosition, scope.content);
        EditorGUI.PropertyField(field, property, GUIContent.none);
        if (!string.IsNullOrEmpty(property.stringValue)) return;
        GUI.Label(field, placeholder, PlaceholderTextStyle);
    }

    public static void DrawPlaceholderObjectField(
        Rect position,
        SerializedProperty property,
        GUIContent label,
        GUIContent placeholder)
    {
        using var scope = new EditorGUI.PropertyScope(position, label, property);
        var field = EditorGUI.PrefixLabel(position, scope.content);
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
        GUI.Box(field, GUIContent.none, EditorStyles.objectField);
        var buttonStyle = GUI.skin.FindStyle("ObjectFieldButton") ?? EditorStyles.miniButton;
        var buttonWidth = buttonStyle.fixedWidth > 0f ? buttonStyle.fixedWidth : LineHeight;
        var button = new Rect(field.xMax - buttonWidth, field.y, buttonWidth, field.height);
        GUI.Box(button, GUIContent.none, buttonStyle);

        var text = new Rect(field.x, field.y, Mathf.Max(0f, field.width - buttonWidth), field.height);
        GUI.Label(text, placeholder, PlaceholderObjectStyle);
    }

    private static GUIStyle CreateObjectPlaceholderStyle()
    {
        var source = EditorStyles.objectField;
        var style = CreatePlaceholderStyle(source);
        style.padding = new RectOffset(
            source.padding.left,
            0,
            source.padding.top,
            source.padding.bottom);
        return style;
    }

    private static GUIStyle CreatePlaceholderStyle(GUIStyle source)
    {
        var color = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, .45f)
            : new Color(0f, 0f, 0f, .45f);
        return new GUIStyle(source)
        {
            normal =
            {
                background = null,
                textColor = color
            },
            hover =
            {
                background = null,
                textColor = color
            },
            focused =
            {
                background = null,
                textColor = color
            },
            active =
            {
                background = null,
                textColor = color
            }
        };
    }

    public static void DrawToggleLeft(Rect position, SerializedProperty property, GUIContent label)
    {
        using var scope = new EditorGUI.PropertyScope(position, label, property);
        var previousMixedValue = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        var value = EditorGUI.ToggleLeft(position, scope.content, property.boolValue);
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


/// <summary>Stateless localized wrappers around Unity's IMGUI API.</summary>
internal static partial class GUIHelper
{
    private const float PopupHorizontalMargin = 6f;
    private static readonly GUIContent IndentedLabelPlaceholder = new(" ");

    public static float PopupWidth(IEnumerable<GUIContent> labels)
        => labels.Max(label => EditorStyles.popup.CalcSize(label).x) + PopupHorizontalMargin;

    public static float LocalizedPopupWidth(IEnumerable<string> optionKeys)
        => PopupWidth(optionKeys.Select(key => key.LG()));

    public static float LocalizedEnumPopupWidth(SerializedProperty property, string typeName)
    {
        var optionPrefix = char.ToLowerInvariant(typeName[0]) + typeName[1..];
        return LocalizedPopupWidth(property.enumNames.Select(name =>
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

    public static void DrawProperty(
        ref Rect position,
        SerializedProperty property,
        string labelKey,
        bool includeChildren = true)
    {
        position.height = EditorGUI.GetPropertyHeight(property, includeChildren);
        using (new EditorGUI.PropertyScope(position, labelKey.LG(), property))
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

    public static (Rect Label, Rect Value) SplitIndentedLabel(Rect position)
    {
        var value = EditorGUI.PrefixLabel(position, IndentedLabelPlaceholder);
        var labelX = position.x + IndentWidth;
        return (
            new Rect(labelX, position.y, Mathf.Max(0f, value.x - labelX), position.height),
            value);
    }

    public static void DrawPropertyWithIndentedLabel(
        ref Rect position,
        SerializedProperty property,
        string labelKey,
        bool includeChildren = true)
    {
        position.height = EditorGUI.GetPropertyHeight(property, GUIContent.none, includeChildren);
        var (label, value) = SplitIndentedLabel(position);
        EditorGUI.LabelField(label, labelKey.LG());
        EditorGUI.PropertyField(value, property, GUIContent.none, includeChildren);
        position.NewLine();
    }

    public static void LocalizedEnumPopup(
        Rect position,
        SerializedProperty property,
        string labelKey,
        IEnumerable<string> optionKeys)
    {
        var label = string.IsNullOrEmpty(labelKey) ? GUIContent.none : labelKey.LG();
        using var _ = new EditorGUI.PropertyScope(position, label, property);
        var previousMixedValue = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        var next = LocalizedPopup(position, property.enumValueIndex, string.IsNullOrEmpty(labelKey) ? null : labelKey, optionKeys);
        if (next != property.enumValueIndex) property.enumValueIndex = next;
        EditorGUI.showMixedValue = previousMixedValue;
    }
}
