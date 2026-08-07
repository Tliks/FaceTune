using UnityEditorInternal;

namespace Aoyon.FaceTune.Gui;

internal sealed record ReorderableListOptions(
    ReorderableListOptions.HeaderMode Header = ReorderableListOptions.HeaderMode.Foldout,
    ReorderableListOptions.ControlsPlacement Controls = ReorderableListOptions.ControlsPlacement.Header,
    float? MaxVisibleHeight = 126f,
    bool NestContent = true,
    float EmptyContentHeight = 0f,
    float HeaderContentHeight = 0f,
    Action<Rect, SerializedProperty>? DrawHeaderContent = null,
    Action<Rect, SerializedProperty>? DrawEmptyOverride = null,
    Action<Rect, SerializedProperty>? DrawElementOverride = null,
    Action<Rect>? DrawElementSeparator = null,
    Action<SerializedProperty>? InitializeElement = null,
    Action<SerializedProperty>? AddElementOverride = null)
{
    internal enum HeaderMode
    {
        None,
        Label,
        Foldout
    }

    internal enum ControlsPlacement
    {
        Manual,
        Header,
        Footer
    }
}

/// <summary>Draws a reorderable array strictly inside a caller-owned rectangle.</summary>
internal static partial class GUIHelper
{
    private const float ButtonWidth = 24f;
    private const float ButtonIconSize = HeaderHeight;
    private const float HeaderHeight = 16f;
    internal const float ListControlsWidth = ButtonWidth * 2f;
    private static GUIStyle? _listButtonStyle;
    private static readonly Dictionary<string, State> States = new();

    private static GUIStyle ListButtonStyle => _listButtonStyle ??= new GUIStyle(EditorStyles.miniButton)
    {
        margin = new RectOffset(),
        overflow = new RectOffset(),
        fixedWidth = 0f,
        fixedHeight = 0f,
        stretchWidth = true,
        stretchHeight = true
    };

    public static void DrawList(Rect position, SerializedProperty property, GUIContent label, ReorderableListOptions? options = null)
    {
        options ??= new ReorderableListOptions();
        var state = GetState(property);
        var list = CreateList(property, state, options);

        var bodyY = position.y;
        var hasHeader = options.Header != ReorderableListOptions.HeaderMode.None;
        if (hasHeader)
        {
            var header = new Rect(position.x, position.y, position.width, HeaderHeight);
            DrawHeader(header, property, label, options, list);
            bodyY = header.yMax + GUIHelper.VerticalSpacing;
        }
        else if (options.Controls == ReorderableListOptions.ControlsPlacement.Header)
        {
            var controls = new Rect(position.x, bodyY, position.width, HeaderHeight);
            DrawControls(controls, _ => { }, property, list, options);
            bodyY = controls.yMax + GUIHelper.VerticalSpacing;
        }
        if (options.Header == ReorderableListOptions.HeaderMode.Foldout && !property.isExpanded) return;
        if (options.DrawHeaderContent != null && options.HeaderContentHeight > 0f)
        {
            var headerContent = new Rect(position.x, bodyY, position.width, options.HeaderContentHeight);
            if (options.NestContent) headerContent.Indent();
            options.DrawHeaderContent(headerContent, property);
            bodyY = headerContent.yMax + GUIHelper.VerticalSpacing;
        }
        if (property.arraySize == 0)
        {
            var emptyHeight = GetEmptyContentHeight(list, options);
            if (emptyHeight > 0f)
            {
                var empty = new Rect(position.x, bodyY, position.width, emptyHeight);
                if (options.NestContent) empty.Indent();
                if (options.DrawEmptyOverride != null)
                    options.DrawEmptyOverride(empty, property);
                else
                    list.DoList(empty);
                bodyY = empty.yMax + GUIHelper.VerticalSpacing;
            }
            DrawFooterControls(position.xMax, bodyY, property, list, options);
            return;
        }
        var fullHeight = list.GetHeight();
        var visibleHeight = options.MaxVisibleHeight.HasValue ? Mathf.Min(fullHeight, options.MaxVisibleHeight.Value) : fullHeight;
        var body = new Rect(position.x, bodyY, position.width, visibleHeight);
        if (options.NestContent) body.Indent();

        if (visibleHeight < fullHeight)
        {
            var view = new Rect(0f, 0f, Mathf.Max(0f, body.width - 16f), fullHeight);
            state.Scroll = GUI.BeginScrollView(body, state.Scroll, view, false, true);
            list.DoList(view);
            GUI.EndScrollView();
        }
        else
        {
            list.DoList(body);
        }

        state.Index = list.index;
        bodyY += visibleHeight + GUIHelper.VerticalSpacing;
        DrawFooterControls(position.xMax, bodyY, property, list, options);
    }

    private static void DrawFooterControls(
        float right,
        float y,
        SerializedProperty property,
        ReorderableList list,
        ReorderableListOptions options)
    {
        if (options.Controls != ReorderableListOptions.ControlsPlacement.Footer) return;
        var footer = new Rect(right - ListControlsWidth, y, ListControlsWidth, HeaderHeight);
        DrawControls(footer, _ => { }, property, list, options);
    }

    private static float GetEmptyContentHeight(ReorderableList list, ReorderableListOptions options)
    {
        var height = options.DrawEmptyOverride == null
            ? list.GetHeight()
            : Mathf.Max(0f, options.EmptyContentHeight);
        return options.MaxVisibleHeight.HasValue
            ? Mathf.Min(height, options.MaxVisibleHeight.Value)
            : height;
    }

    public static float GetListHeight(SerializedProperty property, ReorderableListOptions? options = null)
    {
        options ??= new ReorderableListOptions();
        var hasHeader = options.Header != ReorderableListOptions.HeaderMode.None;
        var headerHeight = hasHeader ? HeaderHeight : 0f;
        if (options.Header == ReorderableListOptions.HeaderMode.Foldout && !property.isExpanded)
            return headerHeight;

        var list = CreateList(property, GetState(property), options);
        var listHeight = property.arraySize == 0 ? 0f : list.GetHeight();
        if (options.MaxVisibleHeight.HasValue) listHeight = Mathf.Min(listHeight, options.MaxVisibleHeight.Value);
        var emptyHeight = property.arraySize == 0
            ? GetEmptyContentHeight(list, options)
            : 0f;
        var hasHeaderContent = options.DrawHeaderContent != null && options.HeaderContentHeight > 0f;
        var hasBody = listHeight > 0f || emptyHeight > 0f;
        var controlsBeforeList = !hasHeader
            && options.Controls == ReorderableListOptions.ControlsPlacement.Header;
        var height = 0f;
        if (controlsBeforeList)
        {
            height += HeaderHeight;
            if (hasBody || hasHeaderContent) height += GUIHelper.VerticalSpacing;
        }
        else if (hasHeader)
        {
            height += headerHeight;
            if (hasBody || hasHeaderContent || options.Controls == ReorderableListOptions.ControlsPlacement.Footer)
                height += GUIHelper.VerticalSpacing;
        }

        if (hasHeaderContent)
        {
            height += options.HeaderContentHeight;
            if (hasBody || options.Controls == ReorderableListOptions.ControlsPlacement.Footer)
                height += GUIHelper.VerticalSpacing;
        }

        height += listHeight + emptyHeight;
        if (options.Controls == ReorderableListOptions.ControlsPlacement.Footer)
        {
            if (hasBody) height += GUIHelper.VerticalSpacing;
            height += HeaderHeight;
        }
        return height;
    }

    private static ReorderableList CreateList(SerializedProperty property, State state, ReorderableListOptions options)
    {
        var list = new ReorderableList(property.serializedObject, property.Copy(), true, false, false, false)
        {
            headerHeight = 0f,
            footerHeight = 0f,
            index = Mathf.Min(state.Index, property.arraySize - 1)
        };
        list.drawFooterCallback = _ => { };
        list.drawElementCallback = (rect, index, _, _) =>
        {
            if (index < 0 || index >= list.serializedProperty.arraySize) return;
            var element = list.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += GUIHelper.VerticalSpacing * .5f;
            if (index > 0 && options.DrawElementSeparator != null)
            {
                var boundary = new Rect(rect.x, rect.y, rect.width, 0f);
                options.DrawElementSeparator(boundary);
            }
            rect.height = EditorGUI.GetPropertyHeight(element, GUIContent.none, true);
            if (options.DrawElementOverride != null)
                options.DrawElementOverride(rect, element);
            else
                EditorGUI.PropertyField(rect, element, GUIContent.none, true);
        };
        list.elementHeightCallback = index => index < 0 || index >= list.serializedProperty.arraySize
            ? GUIHelper.LineHeight
            : EditorGUI.GetPropertyHeight(list.serializedProperty.GetArrayElementAtIndex(index), GUIContent.none, true);
        return list;
    }

    public static void DrawListControls(Rect position, SerializedProperty property, ReorderableListOptions? options = null)
    {
        options ??= new ReorderableListOptions();
        var list = CreateList(property, GetState(property), options);
        DrawControls(position, _ => { }, property, list, options);
    }

    private static void DrawHeader(Rect position, SerializedProperty property, GUIContent label, ReorderableListOptions options, ReorderableList list)
    {
        Action<Rect> drawLabel = options.Header == ReorderableListOptions.HeaderMode.Foldout
            ? rect => GUIHelper.DrawFoldout(rect, property, label)
            : rect => EditorGUI.LabelField(rect, label);
        var showHeaderControls = options.Controls == ReorderableListOptions.ControlsPlacement.Header
            && (options.Header != ReorderableListOptions.HeaderMode.Foldout || property.isExpanded);
        if (showHeaderControls)
            DrawControls(position, drawLabel, property, list, options);
        else
            drawLabel(position);
    }

    private static void DrawControls(
        Rect position,
        Action<Rect> drawLabel,
        SerializedProperty property,
        ReorderableList list,
        ReorderableListOptions options)
    {
        DrawHeader(
            position,
            drawLabel,
            () =>
            {
                if (options.AddElementOverride != null)
                {
                    options.AddElementOverride(property);
                    return;
                }

                var index = property.arraySize;
                property.InsertArrayElementAtIndex(index);
                options.InitializeElement?.Invoke(property.GetArrayElementAtIndex(index));
                list.index = index;
            },
            property.arraySize > 0,
            () =>
            {
                var index = list.index >= 0 && list.index < property.arraySize ? list.index : property.arraySize - 1;
                property.DeleteArrayElementAtIndex(index);
                list.index = Mathf.Min(index, property.arraySize - 1);
            });
    }

    private static void DrawHeader(
        Rect position,
        Action<Rect> drawLabel,
        Action add,
        bool canRemove,
        Action remove)
    {
        var controlsWidth = ButtonWidth * 2f;
        var controlsY = position.center.y - HeaderHeight * .5f;
        var labelRect = new Rect(
            position.x,
            position.y,
            Mathf.Max(0f, position.width - controlsWidth),
            position.height);
        var addRect = new Rect(labelRect.xMax, controlsY, ButtonWidth, HeaderHeight);
        var removeRect = new Rect(addRect.xMax, controlsY, ButtonWidth, HeaderHeight);

        drawLabel(labelRect);
        if (DrawListButton(addRect, "Toolbar Plus")) add();
        using var disabled = new EditorGUI.DisabledScope(!canRemove);
        if (DrawListButton(removeRect, "Toolbar Minus")) remove();
    }

    private static bool DrawListButton(Rect position, string iconName)
    {
        var clicked = GUI.Button(position, GUIContent.none, ListButtonStyle);
        var iconRect = new Rect(
            position.center.x - ButtonIconSize * .5f,
            position.center.y - ButtonIconSize * .5f,
            ButtonIconSize,
            ButtonIconSize);
        GUI.Label(iconRect, EditorGUIUtility.IconContent(iconName), GUIStyle.none);
        return clicked;
    }

    private static State GetState(SerializedProperty property)
    {
        var key = string.Join(",", property.serializedObject.targetObjects.Select(target => target.GetInstanceID())) + ":" + property.propertyPath;
        if (!States.TryGetValue(key, out var state)) States[key] = state = new State();
        return state;
    }

    private sealed class State
    {
        public Vector2 Scroll;
        public int Index = -1;
    }
}
