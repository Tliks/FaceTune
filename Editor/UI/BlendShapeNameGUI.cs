using Aoyon.FaceTune.Gui.ShapesEditor;

namespace Aoyon.FaceTune.Gui;

internal static class BlendShapeNameGUI
{
    private const float PickerButtonWidth = 20f;

    public static void Draw(Rect position, SerializedProperty name)
    {
        var (field, button) = position.SplitRight(PickerButtonWidth);
        EditorGUI.PropertyField(field, name, GUIContent.none);

        if (!GUI.Button(button, GUIContent.none, EditorStyles.popup)
            || !TryGetNames(name, out var names)) return;
        OpenSinglePicker(button, name, names);
    }

    public static void DrawStringElement(Rect position, SerializedProperty element)
        => Draw(position, element);

    public static void DrawListPicker(
        Rect position,
        SerializedProperty list,
        Func<SerializedProperty, SerializedProperty> getName,
        Action<SerializedProperty, string> initialize)
    {
        var label = "expression.editor.button".LG();
        if (!GUI.Button(position, label, GUIStyles.ListButton)
            || !TryGetNames(list, out var names)) return;
        OpenMultiPicker(position, list, names, getName, initialize);
    }

    private static void OpenSinglePicker(Rect position, SerializedProperty name, IReadOnlyList<string> names)
    {
        var serializedObject = name.serializedObject;
        var path = name.propertyPath;
        BlendShapePickerPopup.Show(position, names, false, Array.Empty<string>(), selected =>
        {
            if (selected.Count == 0 || serializedObject.targetObject == null) return;
            serializedObject.UpdateIfRequiredOrScript();
            var current = serializedObject.FindProperty(path);
            if (current == null) return;
            current.stringValue = selected[0];
            serializedObject.ApplyModifiedProperties();
        });
    }

    private static void OpenMultiPicker(
        Rect position,
        SerializedProperty list,
        IReadOnlyList<string> names,
        Func<SerializedProperty, SerializedProperty> getName,
        Action<SerializedProperty, string> initialize)
    {
        var existing = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < list.arraySize; i++)
        {
            var value = getName(list.GetArrayElementAtIndex(i)).stringValue;
            if (!string.IsNullOrEmpty(value)) existing.Add(value);
        }

        var candidateNames = names.ToHashSet(StringComparer.Ordinal);
        var existingCandidates = existing.Where(candidateNames.Contains).ToArray();
        var retainedCustomNames = existing.Where(name => !candidateNames.Contains(name)).ToArray();
        var serializedObject = list.serializedObject;
        var path = list.propertyPath;
        BlendShapePickerPopup.Show(position, names, true, existingCandidates, selected =>
        {
            if (serializedObject.targetObject == null) return;
            serializedObject.UpdateIfRequiredOrScript();
            var current = serializedObject.FindProperty(path);
            if (current == null) return;

            current.SynchronizeArrayByKey(
                selected.Concat(retainedCustomNames),
                element => getName(element).stringValue,
                name => name,
                initialize);
            serializedObject.ApplyModifiedProperties();
        });
    }

    private static bool TryGetNames(SerializedProperty property, out IReadOnlyList<string> names)
    {
        names = Array.Empty<string>();
        if (property.serializedObject.targetObjects.Length != 1
            || property.serializedObject.targetObject is not Component component
            || !AvatarContext.TryGet(component.gameObject, out var avatar, out _)) return false;

        var mesh = avatar.FaceMesh;
        var values = new string[mesh.blendShapeCount];
        for (var i = 0; i < values.Length; i++) values[i] = mesh.GetBlendShapeName(i);
        names = values;
        return values.Length > 0;
    }
}

internal sealed class BlendShapePickerPopup : PopupWindowContent
{
    private static readonly Vector2 WindowSize = new(420f, 480f);
    private const float ItemHeight = 20f;
    private const float ScrollbarWidth = 16f;
    private readonly IReadOnlyList<string> _names;
    private readonly bool _multiple;
    private readonly HashSet<string> _initialSelection;
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private readonly Action<IReadOnlyList<string>> _onSelected;
    private readonly BlendShapeGroupCatalog _groups;
    private Vector2 _scroll;
    private string _search = string.Empty;
    private int _selectedGroup;
    private string? _visibleSearch;
    private int _visibleGroup = -1;
    private int[] _visibleIndices = Array.Empty<int>();

    private BlendShapePickerPopup(
        IReadOnlyList<string> names,
        bool multiple,
        IEnumerable<string> existing,
        Action<IReadOnlyList<string>> onSelected)
    {
        _names = names;
        _multiple = multiple;
        _initialSelection = existing.ToHashSet(StringComparer.Ordinal);
        _selected.UnionWith(_initialSelection);
        _onSelected = onSelected;
        _groups = new BlendShapeGroupCatalog(names);
    }

    public static void Show(
        Rect position,
        IReadOnlyList<string> names,
        bool multiple,
        IEnumerable<string> existing,
        Action<IReadOnlyList<string>> onSelected)
        => PopupWindow.Show(position, new BlendShapePickerPopup(names, multiple, existing, onSelected));

    public override Vector2 GetWindowSize() => WindowSize;

    public override void OnGUI(Rect rect)
    {
        const float padding = 4f;
        const float applyButtonWidth = 120f;
        var content = new Rect(
            rect.x + padding,
            rect.y + padding,
            Mathf.Max(0f, rect.width - padding * 2f),
            Mathf.Max(0f, rect.height - padding * 2f));
        var line = new Rect(content.x, content.y, content.width, EditorGUIUtility.singleLineHeight);
        _search = EditorGUI.TextField(
            line,
            _search,
            GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.textField);

        line.y = line.yMax + EditorGUIUtility.standardVerticalSpacing;
        var groupLabels = new[] { "blendShapePicker.group.all".LS() }
            .Concat(_groups.Groups.Select(group => group.Name))
            .ToArray();
        _selectedGroup = EditorGUI.Popup(line, _selectedGroup, groupLabels);

        var visible = GetVisibleIndices();
        line.y = line.yMax + EditorGUIUtility.standardVerticalSpacing;
        if (_multiple)
        {
            DrawSelectionToolbar(line, visible);
            line.y = line.yMax + EditorGUIUtility.standardVerticalSpacing;
        }

        var footerHeight = _multiple
            ? EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight
            : 0f;
        var scrollRect = new Rect(
            content.x,
            line.y,
            content.width,
            Mathf.Max(0f, content.yMax - line.y - footerHeight));
        DrawItems(scrollRect, visible);

        if (!_multiple) return;
        var apply = new Rect(
            content.xMax - applyButtonWidth,
            content.yMax - EditorGUIUtility.singleLineHeight,
            applyButtonWidth,
            EditorGUIUtility.singleLineHeight);
        using (new EditorGUI.DisabledScope(_selected.SetEquals(_initialSelection)))
        {
            if (GUI.Button(apply, "blendShapePicker.apply.button".LS())) CommitSelection();
        }
    }

    private IReadOnlyList<int> GetVisibleIndices()
    {
        if (_visibleGroup == _selectedGroup
            && string.Equals(_visibleSearch, _search, StringComparison.Ordinal)) return _visibleIndices;

        IEnumerable<int> indices = _selectedGroup == 0
            ? Enumerable.Range(0, _names.Count)
            : _groups.Groups[_selectedGroup - 1].BlendShapeIndices;
        _visibleIndices = indices.Where(index => string.IsNullOrWhiteSpace(_search)
                || _names[index].IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToArray();
        _visibleGroup = _selectedGroup;
        _visibleSearch = _search;
        _scroll = Vector2.zero;
        return _visibleIndices;
    }

    private void DrawSelectionToolbar(Rect position, IReadOnlyCollection<int> visible)
    {
        var (select, clear) = position.SplitRatio(.5f);
        if (GUI.Button(select, "blendShapePicker.selectVisible.button".LS(), EditorStyles.miniButton))
            foreach (var index in visible) _selected.Add(_names[index]);
        if (GUI.Button(clear, "blendShapePicker.clearVisible.button".LS(), EditorStyles.miniButton))
            foreach (var index in visible) _selected.Remove(_names[index]);
    }

    private void DrawItems(Rect position, IReadOnlyList<int> visible)
    {
        var contentHeight = visible.Count * ItemHeight;
        var view = new Rect(
            0f,
            0f,
            Mathf.Max(0f, position.width - ScrollbarWidth),
            Mathf.Max(position.height, contentHeight));
        _scroll = GUI.BeginScrollView(position, _scroll, view, false, true);

        var first = Mathf.Clamp(Mathf.FloorToInt(_scroll.y / ItemHeight), 0, visible.Count);
        var last = Mathf.Clamp(
            Mathf.CeilToInt((_scroll.y + position.height) / ItemHeight),
            first,
            visible.Count);
        for (var visibleIndex = first; visibleIndex < last; visibleIndex++)
        {
            var item = new Rect(0f, visibleIndex * ItemHeight, view.width, ItemHeight);
            DrawItem(item, visible[visibleIndex]);
        }
        GUI.EndScrollView();
    }

    private void DrawItem(Rect position, int index)
    {
        var name = _names[index];
        if (_multiple)
        {
            var selected = EditorGUI.ToggleLeft(position, name, _selected.Contains(name));
            if (selected) _selected.Add(name);
            else _selected.Remove(name);
            return;
        }

        if (GUI.Button(position, name, EditorStyles.label))
        {
            _onSelected(new[] { name });
            editorWindow.Close();
        }
    }

    private void CommitSelection()
    {
        _onSelected(_names.Where(_selected.Contains).ToArray());
        editorWindow.Close();
    }
}
