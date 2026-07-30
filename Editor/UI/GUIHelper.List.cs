using UnityEditorInternal;

namespace Aoyon.FaceTune.Gui;

internal sealed record ReorderableListOptions(
    bool Foldout = true,
    bool Header = true,
    float? MaxVisibleHeight = null,
    Action<SerializedProperty>? InitializeElement = null,
    Action<SerializedProperty>? AddElement = null,
    Action<Rect>? DrawElementSeparator = null,
    Action<Rect, SerializedProperty>? DrawElement = null,
    bool NestContent = true);

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
        if (options.Header)
        {
            var header = new Rect(position.x, position.y, position.width, HeaderHeight);
            DrawHeader(header, property, label, options, list);
            bodyY = header.yMax + GUIHelper.VerticalSpacing;
        }
        if ((options.Foldout && !property.isExpanded) || property.arraySize == 0) return;
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
    }

    public static float GetListHeight(SerializedProperty property, ReorderableListOptions? options = null)
    {
        options ??= new ReorderableListOptions();
        var headerHeight = options.Header ? HeaderHeight : 0f;
        if ((options.Foldout && !property.isExpanded) || property.arraySize == 0) return headerHeight;

        var listHeight = CreateList(property, GetState(property), options).GetHeight();
        if (options.MaxVisibleHeight.HasValue) listHeight = Mathf.Min(listHeight, options.MaxVisibleHeight.Value);
        return headerHeight + (options.Header ? GUIHelper.VerticalSpacing : 0f) + listHeight;
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
            if (options.DrawElement != null)
                options.DrawElement(rect, element);
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
        DrawHeader(position, _ => { }, property, options, list);
    }

    private static void DrawHeader(Rect position, SerializedProperty property, GUIContent label, ReorderableListOptions options, ReorderableList list)
        => DrawHeader(position, rect =>
        {
            if (options.Foldout) GUIHelper.DrawFoldout(rect, property, label);
            else EditorGUI.LabelField(rect, label);
        }, property, options, list);

    private static void DrawHeader(Rect position, Action<Rect> drawLabel, SerializedProperty property, ReorderableListOptions options, ReorderableList list)
    {
        DrawHeader(
            position,
            drawLabel,
            () =>
            {
                if (options.AddElement != null)
                {
                    options.AddElement(property);
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
