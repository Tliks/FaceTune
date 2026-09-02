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
    Action<SerializedProperty>? AddElementOverride = null,
    Action<Rect, SerializedProperty>? DrawHeaderAction = null,
    float HeaderActionWidth = 60f,
    float? ElementHeight = null,
    bool Reorderable = true,
    bool SingleLineWhenEmpty = false)
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
    private const float VisualHandleWidth = 10f;
    private const float VisualHandleHeight = 6f;
    private const float VisualHandleLeftOffset = 2f;
    internal const float ListControlsWidth = ButtonWidth * 2f;

    public static float GetOptionalListHeight(
        SerializedProperty property,
        ReorderableListOptions options)
        => ShouldDrawOptionalList(property)
            ? GetListHeight(property, options)
            : LineHeight;

    public static void DrawLocalizedOptionalList(
        Rect position,
        SerializedProperty property,
        GUIContent label,
        string disabledOptionKey,
        string enabledOptionKey,
        ReorderableListOptions options)
    {
        if (ShouldDrawOptionalList(property))
        {
            DrawList(position, property, label, options);
            return;
        }

        LocalizedOptionalListPopup(
            position,
            property,
            label,
            disabledOptionKey,
            enabledOptionKey,
            options.InitializeElement ?? (_ => { }));
    }

    private static bool ShouldDrawOptionalList(SerializedProperty property)
        => !property.hasMultipleDifferentValues && property.arraySize > 0;

    public static void DrawList(Rect position, SerializedProperty property, GUIContent label, ReorderableListOptions? options = null)
    {
        options ??= new ReorderableListOptions();
        var state = GetState(property);
        var list = GetList(property, state, options);

        var bodyY = position.y;
        var hasHeader = options.Header != ReorderableListOptions.HeaderMode.None;
        if (hasHeader)
        {
            var header = new Rect(position.x, position.y, position.width, HeaderHeight);
            DrawHeader(header, property, label, options, list, state);
            bodyY = header.yMax + GUIHelper.VerticalSpacing;
        }
        else if (options.Controls == ReorderableListOptions.ControlsPlacement.Header)
        {
            var controls = new Rect(position.x, bodyY, position.width, HeaderHeight);
            using var scope = new EditorGUI.PropertyScope(controls, GUIContent.none, property);
            using var rightClick = new RightClickPassthroughScope(controls);
            DrawControls(controls, _ => { }, property, list, options);
            bodyY = controls.yMax + GUIHelper.VerticalSpacing;
        }
        if (options.Header == ReorderableListOptions.HeaderMode.Foldout && !state.Foldout.Expanded) return;
        if (options.DrawHeaderContent != null && options.HeaderContentHeight > 0f)
        {
            var headerContent = new Rect(position.x, bodyY, position.width, options.HeaderContentHeight);
            if (options.NestContent) headerContent.Indent();
            using var headerScope = new EditorGUI.PropertyScope(headerContent, GUIContent.none, property);
            using var rightClick = new RightClickPassthroughScope(headerContent);
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
                using var emptyScope = new EditorGUI.PropertyScope(empty, GUIContent.none, property);
                using var rightClick = new RightClickPassthroughScope(empty);
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
            var visibleRect = new Rect(state.Scroll.x, state.Scroll.y, body.width, body.height);
            list.DoList(view, visibleRect);
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
        using var scope = new EditorGUI.PropertyScope(footer, GUIContent.none, property);
        using var rightClick = new RightClickPassthroughScope(footer);
        DrawControls(footer, _ => { }, property, list, options);
    }

    private static float GetEmptyContentHeight(ReorderableList list, ReorderableListOptions options)
    {
        if (options.SingleLineWhenEmpty) return 0f;
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
        var state = GetState(property);
        if (options.Header == ReorderableListOptions.HeaderMode.Foldout && !state.Foldout.Expanded)
            return headerHeight;
        var list = GetList(property, state, options);
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

    private static ReorderableList GetList(SerializedProperty property, State state, ReorderableListOptions options)
    {
        var serializedObject = property.serializedObject;
        if (!ReferenceEquals(state.ListSerializedObject, serializedObject))
        {
            state.List = new ReorderableList(
                serializedObject,
                property.Copy(),
                options.Reorderable,
                false,
                false,
                false);
            state.ListSerializedObject = serializedObject;
        }
        var list = state.List!;
        list.headerHeight = 0f;
        list.footerHeight = 0f;
        list.index = Mathf.Min(state.Index, property.arraySize - 1);
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
            rect.height = options.ElementHeight
                       ?? EditorGUI.GetPropertyHeight(element, GUIContent.none, true);
            if (options.DrawElementOverride != null)
            {
                using var elementScope = new EditorGUI.PropertyScope(rect, GUIContent.none, element);
                using var rightClick = new RightClickPassthroughScope(rect);
                options.DrawElementOverride(rect, element);
            }
            else
                EditorGUI.PropertyField(rect, element, GUIContent.none, true);
        };
        if (options.ElementHeight.HasValue)
        {
            list.elementHeight = options.ElementHeight.Value;
            list.elementHeightCallback = null;
        }
        else
        {
            list.elementHeightCallback = index => index < 0 || index >= list.serializedProperty.arraySize
                ? GUIHelper.LineHeight
                : EditorGUI.GetPropertyHeight(list.serializedProperty.GetArrayElementAtIndex(index), GUIContent.none, true);
        }
        return list;
    }

    public static void DrawVirtualList<T>(
        Rect position,
        SerializedProperty stateProperty,
        IList<T> elements,
        GUIContent label,
        Func<int, float> getElementHeight,
        Action<Rect, int> drawElement,
        Action add,
        Action<int> remove,
        Action<Rect>? drawEmpty = null,
        float emptyHeight = 30f,
        float? maxVisibleHeight = 126f)
    {
        var state = GetState(stateProperty);
        state.Index = Mathf.Min(state.Index, elements.Count - 1);
        var structureChanged = false;
        var header = new Rect(position.x, position.y, position.width, HeaderHeight);
        DrawListHeaderControls(
            header,
            label,
            add,
            elements.Count > 0,
            () =>
            {
                var index = state.Index >= 0 && state.Index < elements.Count ? state.Index : elements.Count - 1;
                remove(index);
                state.Index = Mathf.Min(index, elements.Count - 2);
                structureChanged = true;
            });
        if (structureChanged) return;

        var bodyHeight = GetVirtualListBodyHeight(elements, getElementHeight, emptyHeight, maxVisibleHeight);
        var body = new Rect(position.x, header.yMax + VerticalSpacing, position.width, bodyHeight);
        if (elements.Count == 0)
        {
            drawEmpty?.Invoke(body);
            return;
        }

        var list = CreateVirtualList(elements, state, getElementHeight, drawElement);
        var fullHeight = list.GetHeight();
        if (body.height < fullHeight)
        {
            var view = new Rect(0f, 0f, Mathf.Max(0f, body.width - 16f), fullHeight);
            state.Scroll = GUI.BeginScrollView(body, state.Scroll, view, false, true);
            var visibleRect = new Rect(state.Scroll.x, state.Scroll.y, body.width, body.height);
            list.DoList(view, visibleRect);
            GUI.EndScrollView();
        }
        else
        {
            list.DoList(body);
        }
        state.Index = list.index;
    }

    public static float GetVirtualListHeight<T>(
        IList<T> elements,
        Func<int, float> getElementHeight,
        float emptyHeight = 30f,
        float? maxVisibleHeight = 126f)
        => HeaderHeight + VerticalSpacing
         + GetVirtualListBodyHeight(elements, getElementHeight, emptyHeight, maxVisibleHeight);

    public static void SetVirtualListIndex(SerializedProperty stateProperty, int index)
        => GetState(stateProperty).Index = index;

    public static void DrawListElementHandle(Rect position)
    {
        if (Event.current.type != EventType.Repaint) return;
        var handle = new Rect(
            position.x + VisualHandleLeftOffset,
            position.center.y - VisualHandleHeight * .5f,
            VisualHandleWidth,
            VisualHandleHeight);
        GUI.skin.FindStyle("RL DragHandle")?.Draw(handle, false, false, false, false);
    }

    private static ReorderableList CreateVirtualList<T>(
        IList<T> elements,
        State state,
        Func<int, float> getElementHeight,
        Action<Rect, int> drawElement)
    {
        var list = new ReorderableList((IList)elements, typeof(T), false, false, false, false)
        {
            headerHeight = 0f,
            footerHeight = 0f,
            index = state.Index,
            elementHeightCallback = index => getElementHeight(index)
        };
        list.drawElementCallback = (rect, index, _, _) =>
        {
            rect.y += VerticalSpacing * .5f;
            DrawListElementHandle(rect);
            drawElement(rect, index);
        };
        return list;
    }

    private static float GetVirtualListBodyHeight<T>(
        IList<T> elements,
        Func<int, float> getElementHeight,
        float emptyHeight,
        float? maxVisibleHeight)
    {
        if (elements.Count == 0) return emptyHeight;
        var list = new ReorderableList((IList)elements, typeof(T), false, false, false, false)
        {
            headerHeight = 0f,
            footerHeight = 0f,
            elementHeightCallback = index => getElementHeight(index)
        };
        var height = list.GetHeight();
        return maxVisibleHeight.HasValue ? Mathf.Min(height, maxVisibleHeight.Value) : height;
    }

    public static void DrawListHeaderControls(
        Rect position,
        GUIContent label,
        Action add,
        bool canRemove,
        Action remove)
    {
        using var rightClick = new RightClickPassthroughScope(position);
        DrawHeader(position, rect => EditorGUI.LabelField(rect, label), add, canRemove, remove);
    }

    public static void DrawListControls(Rect position, SerializedProperty property, ReorderableListOptions? options = null)
    {
        options ??= new ReorderableListOptions();
        var state = GetState(property);
        var list = GetList(property, state, options);
        using var scope = new EditorGUI.PropertyScope(position, GUIContent.none, property);
        using var rightClick = new RightClickPassthroughScope(position);
        DrawControls(position, _ => { }, property, list, options);
    }

    private static void DrawHeader(
        Rect position,
        SerializedProperty property,
        GUIContent label,
        ReorderableListOptions options,
        ReorderableList list,
        State state)
    {
        // The built-in ReorderableList header is disabled, so reproduce its list-level scope here.
        using var scope = new EditorGUI.PropertyScope(position, label, property);
        using var rightClick = new RightClickPassthroughScope(position);
        var headerLabel = scope.content;
        Action<Rect> drawLabel = options.Header == ReorderableListOptions.HeaderMode.Foldout
            ? rect => state.Foldout.Expanded = GUIHelper.DrawFoldout(rect, state.Foldout.Expanded, headerLabel)
            : rect => EditorGUI.LabelField(rect, headerLabel);
        var showHeaderControls = options.Controls == ReorderableListOptions.ControlsPlacement.Header
            && (options.Header != ReorderableListOptions.HeaderMode.Foldout || state.Foldout.Expanded);
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
            },
            options.DrawHeaderAction == null
                ? null
                : rect => options.DrawHeaderAction(rect, property),
            options.HeaderActionWidth);
    }

    private static void DrawHeader(
        Rect position,
        Action<Rect> drawLabel,
        Action add,
        bool canRemove,
        Action remove,
        Action<Rect>? drawAction = null,
        float actionWidth = 0f)
    {
        actionWidth = drawAction == null ? 0f : Mathf.Max(0f, actionWidth);
        var controlsWidth = ButtonWidth * 2f + actionWidth;
        var controlsY = position.center.y - HeaderHeight * .5f;
        var labelRect = new Rect(
            position.x,
            position.y,
            Mathf.Max(0f, position.width - controlsWidth),
            position.height);
        var actionRect = new Rect(labelRect.xMax, controlsY, actionWidth, HeaderHeight);
        var addRect = new Rect(actionRect.xMax, controlsY, ButtonWidth, HeaderHeight);
        var removeRect = new Rect(addRect.xMax, controlsY, ButtonWidth, HeaderHeight);

        drawLabel(labelRect);
        drawAction?.Invoke(actionRect);
        if (DrawListButton(addRect, "Toolbar Plus")) add();
        using var disabled = new EditorGUI.DisabledScope(!canRemove);
        if (DrawListButton(removeRect, "Toolbar Minus")) remove();
    }

    private static bool DrawListButton(Rect position, string iconName)
    {
        var clicked = GUI.Button(position, GUIContent.none, GUIStyles.ListButton);
        var iconRect = new Rect(
            position.center.x - ButtonIconSize * .5f,
            position.center.y - ButtonIconSize * .5f,
            ButtonIconSize,
            ButtonIconSize);
        GUI.Label(iconRect, EditorGUIUtility.IconContent(iconName), GUIStyle.none);
        return clicked;
    }

    private static State GetState(SerializedProperty property)
        => GUIState.Get(property, "reorderableList", () => new State());

    private sealed class State
    {
        public ReorderableList? List;
        public SerializedObject? ListSerializedObject;
        public Vector2 Scroll;
        public int Index = -1;
        public FoldoutState Foldout { get; } = new(true);
    }
}
