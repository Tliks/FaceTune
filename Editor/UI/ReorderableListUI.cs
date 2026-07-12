using UnityEditorInternal;

namespace Aoyon.FaceTune.Gui;

internal sealed record ReorderableListOptions(
    bool Foldout = true,
    float? MaxVisibleHeight = null,
    Action<SerializedProperty>? InitializeElement = null);

/// <summary>Draws a reorderable array strictly inside a caller-owned rectangle.</summary>
internal static class ReorderableListUI
{
    private const float ButtonWidth = 20f;
    private static readonly Dictionary<string, State> States = new();

    public static void Draw(Rect position, SerializedProperty property, GUIContent label, ReorderableListOptions? options = null)
    {
        options ??= new ReorderableListOptions();
        var state = GetState(property);
        var list = CreateList(property, state);

        var header = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        DrawHeader(header, property, label, options, list);
        if (options.Foldout && !property.isExpanded) return;

        var bodyY = header.yMax + EditorGUIUtility.standardVerticalSpacing;
        var fullHeight = list.GetHeight();
        var visibleHeight = options.MaxVisibleHeight.HasValue ? Mathf.Min(fullHeight, options.MaxVisibleHeight.Value) : fullHeight;
        var body = new Rect(position.x, bodyY, position.width, visibleHeight);

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

    public static float GetHeight(SerializedProperty property, ReorderableListOptions? options = null)
    {
        options ??= new ReorderableListOptions();
        if (options.Foldout && !property.isExpanded) return EditorGUIUtility.singleLineHeight;

        var listHeight = CreateList(property, GetState(property)).GetHeight();
        if (options.MaxVisibleHeight.HasValue) listHeight = Mathf.Min(listHeight, options.MaxVisibleHeight.Value);
        return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + listHeight;
    }

    private static ReorderableList CreateList(SerializedProperty property, State state)
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
            rect.height = EditorGUI.GetPropertyHeight(element, GUIContent.none, true);
            EditorGUI.PropertyField(rect, element, GUIContent.none, true);
        };
        list.elementHeightCallback = index => index < 0 || index >= list.serializedProperty.arraySize
            ? EditorGUIUtility.singleLineHeight
            : EditorGUI.GetPropertyHeight(list.serializedProperty.GetArrayElementAtIndex(index), GUIContent.none, true);
        return list;
    }

    private static void DrawHeader(Rect position, SerializedProperty property, GUIContent label, ReorderableListOptions options, ReorderableList list)
    {
        var (contentRect, removeRect) = position.SplitRight(ButtonWidth);
        var (labelRect, addRect) = contentRect.SplitRight(ButtonWidth);
        if (options.Foldout) FoldoutUI.Draw(labelRect, property, label);
        else EditorGUI.LabelField(labelRect, label);

        if (GUI.Button(addRect, EditorGUIUtility.IconContent("Toolbar Plus"), EditorStyles.miniButtonLeft))
        {
            var index = property.arraySize;
            property.InsertArrayElementAtIndex(index);
            options.InitializeElement?.Invoke(property.GetArrayElementAtIndex(index));
            list.index = index;
        }

        using (new EditorGUI.DisabledScope(property.arraySize == 0))
        {
            if (!GUI.Button(removeRect, EditorGUIUtility.IconContent("Toolbar Minus"), EditorStyles.miniButtonRight)) return;
            var index = list.index >= 0 && list.index < property.arraySize ? list.index : property.arraySize - 1;
            property.DeleteArrayElementAtIndex(index);
            list.index = Mathf.Min(index, property.arraySize - 1);
        }
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
