using UnityEditor.IMGUI.Controls;
using nadena.dev.ndmf.preview;
using aoyon.facetune.preview;
using System.Text.RegularExpressions;

namespace aoyon.facetune.ui;

internal class FacialShapesEditor : EditorWindow
{
    public SkinnedMeshRenderer Renderer = null!;
    public Mesh Mesh = null!;
    public HashSet<string> AllKeys = null!;
    public string[] AllKeysArray = null!; // パフォーマンス向上のためのキャッシュ
    public BlendShapeSet? FacialStyleSet = null; // スタイル参照用
    [SerializeField] private bool[] _overrideFlags = null!;
    [SerializeField] private float[] _overrideWeights = null!;

    private BlendShapeSelectorManager _selector = null!;
    private BlendShapeGrouping _grouping = null!;
    private GroupSelectionUI _groupSelectionUI = null!;

    [SerializeField] private bool _applyGroupFilterToSelected = true;
    [SerializeField] private bool _applyGroupFilterToUnselected = true;

    private bool _previewOnHover = true;
    public bool PreviewOnHover
    {
        get => _previewOnHover;
        set
        {
            if (_previewOnHover == value) return;
            _previewOnHover = value;
            var current = _previewShapes.Value;
            _previewShapes.Value = _emptySet;
            if (value)
            {
                GetResult(current);
                _previewShapes.Value = current;
            }
            else
            {
                current.Clear();
                _previewShapes.Value = current;
            }
        }
    }
    private bool _highlightOnHover = false;
    public bool HighlightOnHover
    {
        get => _highlightOnHover;
        set
        {
            if (_highlightOnHover == value) return;
            _highlightOnHover = value;
            if (!value) _highlightBlendShapeProcessor.ClearHighlight();
        }
    }

    private HighlightBlendShapeProcessor _highlightBlendShapeProcessor = null!;

    private readonly EditorGUISplitHelper _horizontalSplitView = new(EditorGUISplitHelper.Direction.Horizontal);
    private readonly EditorGUISplitHelper _verticalSplitView = new(EditorGUISplitHelper.Direction.Vertical);

    private Action<BlendShapeSet>? _onApply = null;

    private SerializedObject _serializedObject = null!;
    private SerializedProperty _overrideFlagsProperty = null!;
    private SerializedProperty _overrideWeightsProperty = null!;

    private BlendShapeSet _emptySet = new();
    private readonly PublishedValue<BlendShapeSet> _previewShapes = new(new());

    private AnimationClip? _sourceClip = null;
    private ClipExcludeOption _clipExcludeOption = ClipExcludeOption.ExcludeZeroWeight;

    private int _previousUndoGroup = -1;

    public static FacialShapesEditor? OpenEditor(SkinnedMeshRenderer renderer, Mesh mesh, HashSet<string> allKeys, BlendShapeSet defaultOverrides, BlendShapeSet? facialStyleSet = null)
    {
        if (HasOpenInstances<FacialShapesEditor>())
        {
            var existingWindow = GetWindow<FacialShapesEditor>();
            if (existingWindow.hasUnsavedChanges)
            {
                var result = EditorUtility.DisplayDialogComplex("Unsaved Changes", "This window may have unsaved changes. Would you like to save?", "Save", "Unsave", "Cancel");
                if (result == 0)
                {
                    existingWindow.SaveChanges();
                    existingWindow.Close();
                }
                else if (result == 1)
                {
                    existingWindow.ForceClose();
                }
                else
                {
                    return null;
                }
            }
        }

        var window = GetWindow<FacialShapesEditor>();
        window.Init(renderer, mesh, allKeys, defaultOverrides, facialStyleSet);
        return window;
    }

    public void Init(SkinnedMeshRenderer renderer, Mesh mesh, HashSet<string> allKeys, BlendShapeSet defaultOverrides, BlendShapeSet? facialStyleSet = null)
    {
        Renderer = renderer;
        Mesh = mesh;

        AllKeys = allKeys;
        AllKeysArray = allKeys.ToArray(); // パフォーマンス向上のため配列キャッシュを作成
        FacialStyleSet = facialStyleSet;
        _overrideFlags = AllKeysArray.Select(x => defaultOverrides.Contains(x)).ToArray();
        _overrideWeights = AllKeysArray.Select(x => defaultOverrides.TryGetValue(x, out var value) ? value.Weight : -1).ToArray();

        _serializedObject = new SerializedObject(this);
        _overrideFlagsProperty = _serializedObject.FindProperty(nameof(_overrideFlags));
        _overrideWeightsProperty = _serializedObject.FindProperty(nameof(_overrideWeights));

        _grouping = new BlendShapeGrouping(AllKeys);
        _groupSelectionUI = new GroupSelectionUI(_grouping.Groups, OnGroupSelectionChanged);
        _selector = new BlendShapeSelectorManager(AllKeysArray, _overrideFlagsProperty, _overrideWeightsProperty, _grouping, FacialStyleSet, 
            () => _applyGroupFilterToSelected, () => _applyGroupFilterToUnselected, OnAnyChangeCallback, OnHoveredCallback);
        _highlightBlendShapeProcessor = new HighlightBlendShapeProcessor(renderer, mesh);

        saveChangesMessage = "This window may have unsaved changes. Would you like to save?";
        hasUnsavedChanges = false;

        _previousUndoGroup = Undo.GetCurrentGroup();
        Undo.IncrementCurrentGroup();

        _previewShapes.Value = new();
        EditingShapesPreview.Start(renderer, _previewShapes!);
    }

    private void OnGroupSelectionChanged()
    {
        _selector.Reload();
    }

    private void OnAnyChangeCallback()
    {
        hasUnsavedChanges = true;
        RefreshPreview();
    }

    private void OnHoveredCallback(int index)
    {
        if (PreviewOnHover)
        {
            var current = _previewShapes.Value;
            _previewShapes.Value = _emptySet;
            GetResult(current);
            if (index == -1)
            {
                _previewShapes.Value = current;
            }
            else
            {
                current.Add(new BlendShape(AllKeysArray[index], 100f));
                _previewShapes.Value = current;
            }
        }
        if (HighlightOnHover)
        {
            if (index == -1) _highlightBlendShapeProcessor.ClearHighlight();
            else _highlightBlendShapeProcessor.HilightBlendShapeFor(index);
        }
    }

    private void EditedAndRefresh()
    {
        hasUnsavedChanges = true;
        RefreshPreview();
        _selector.Reload();
    }

    private void RefreshPreview()
    {
        if (PreviewOnHover)
        {
            var current = _previewShapes.Value;
            _previewShapes.Value = _emptySet;
            GetResult(current);
            _previewShapes.Value = current;
        }
    }

    public void RegisterApplyCallback(Action<BlendShapeSet> onApply)
    {
        _onApply = onApply;
    }

    public override void SaveChanges()
    {
        var result = new BlendShapeSet();
        GetResult(result);
        _onApply?.Invoke(result);
        hasUnsavedChanges = false;
        base.SaveChanges();
    }

    public void GetResult(BlendShapeSet result)
    {
        result.Clear();
        _serializedObject.ApplyModifiedProperties();
        _serializedObject.Update();
        for (var i = 0; i < _overrideWeightsProperty.arraySize; i++)
        {
            var overrideFlag = _overrideFlagsProperty.GetArrayElementAtIndex(i).boolValue;
            if (!overrideFlag) continue;

            var weight = _overrideWeightsProperty.GetArrayElementAtIndex(i).floatValue;
            result.Add(new BlendShape(AllKeysArray[i], weight));
        }
    }

    public void ForceClose()
    {
        hasUnsavedChanges = false;
        Close();
    }

    void OnDisable()
    {
        _selector?.Dispose();
        _highlightBlendShapeProcessor?.Dispose();
        EditingShapesPreview.Stop();
        CollapseUndoGroup();
    }

    private void CollapseUndoGroup()
    {
        if (_previousUndoGroup != -1)
        {
            Undo.CollapseUndoOperations(_previousUndoGroup);
            _previousUndoGroup = -1;
        }
    }

    public virtual void OnGUI()
    {
        if (HandleKeyboardEvents()) return;

        _serializedObject.Update();

        _horizontalSplitView.BeginSplitView();

        _verticalSplitView.BeginSplitView();
        DrawSettingsView();
        _verticalSplitView.Split();
        DrawSelectedBlendShapes();
        _verticalSplitView.EndSplitView();

        _horizontalSplitView.Split();

        EditorGUILayout.BeginVertical();
        DrawUnSelectedBlendShapes();
        EditorGUILayout.EndVertical();

        _horizontalSplitView.EndSplitView();
        Repaint();

        _serializedObject.ApplyModifiedProperties();
    }

    private bool HandleKeyboardEvents()
    {
        if (Event.current.type != EventType.KeyDown) return false;

        // Ctrl+S（WindowsまたはLinux）またはCmd+S（Mac）で保存
        if (Event.current.keyCode == KeyCode.S)
        {
            if ((Event.current.control && Application.platform != RuntimePlatform.OSXEditor) || 
                (Event.current.command && Application.platform == RuntimePlatform.OSXEditor))
            {
                SaveChanges();
                Event.current.Use();
                return true;
            }
        }

        return false;
    }

    private void DrawSettingsView()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Preview Options", EditorStyles.boldLabel);
        PreviewOnHover = EditorGUILayout.Toggle("Preview On Hover", PreviewOnHover);
        HighlightOnHover = EditorGUILayout.Toggle("Highlight On Hover", HighlightOnHover);
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Group Filter", EditorStyles.boldLabel);
        _groupSelectionUI.OnGUI();
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Apply Filter To:", EditorStyles.miniLabel);
        EditorGUI.BeginChangeCheck();
        _applyGroupFilterToSelected = EditorGUILayout.Toggle("Selected List", _applyGroupFilterToSelected);
        _applyGroupFilterToUnselected = EditorGUILayout.Toggle("Unselected List", _applyGroupFilterToUnselected);
        if (EditorGUI.EndChangeCheck())
        {
            _selector.Reload();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Clip Extraction", EditorStyles.boldLabel);
        _sourceClip = EditorGUILayout.ObjectField("Source Clip", _sourceClip, typeof(AnimationClip), false) as AnimationClip;
        _clipExcludeOption = (ClipExcludeOption)EditorGUILayout.EnumPopup("Exclude Option", _clipExcludeOption);
        
        EditorGUILayout.Space(5);
        if (GUILayout.Button("Extract from Clip"))
        {
            if (_sourceClip == null) return;
            Undo.RecordObject(this, "Extract from Clip");
            ExtractFromClip();
            EditorApplication.delayCall += () => EditedAndRefresh();
        }
        EditorGUILayout.EndVertical();
    }

    private void ExtractFromClip()
    {
        if (_sourceClip == null) return;

        var newBlendShapes = new BlendShapeSet();
        var baseShapeSet = new BlendShapeSet();
        foreach (var key in AllKeysArray)
        {
            baseShapeSet.Add(new BlendShape(key, 0f));
        }
        _sourceClip.GetFirstFrameBlendShapes(newBlendShapes, _clipExcludeOption, baseShapeSet);

        foreach (var blendShape in newBlendShapes)
        {
            var index = Array.IndexOf(AllKeysArray, blendShape.Name);
            if (index >= 0)
            {
                // if (!_includeEqualOverride && _overrideWeights[index] == blendShape.Weight) continue;
                _overrideFlags[index] = true;
                _overrideWeights[index] = blendShape.Weight;
            }
        }
    }

    private void DrawSelectedBlendShapes()
    {
        _selector.DrawSelected();
    }

    private void DrawUnSelectedBlendShapes()
    {
        _selector.DrawUnSelected();
    }
}

internal class BlendShapeGroup
{
    public readonly string Name;
    public readonly List<int> BlendShapeIndices;
    public bool IsSelected { get; set; } = true;

    public BlendShapeGroup(string name)
    {
        Name = name;
        BlendShapeIndices = new List<int>();
    }
}

internal class BlendShapeGrouping
{
    private const string DefaultGroupName = "Default";
    
    private static readonly string GroupNameSymbolPattern = string.Join("|", new[]
    {
        @"\W",     // Non-Word Characters
        @"\p{Pc}", // Connector Punctuations
        @"ー",     // Katakana-Hiragana Prolonged Sound Mark
        @"ｰ",      // Halfwidth Katakana-Hiragana Prolonged Sound Mark
    });
    
    private static readonly string GroupNamePattern = string.Join("|", new[]
    {
        $"^(?:(?:{GroupNameSymbolPattern}){{3,}})(.*?)(?:(?:{GroupNameSymbolPattern}){{3,}})?$",
        $"^(?:(?:{GroupNameSymbolPattern}){{3,}})?(.*?)(?:(?:{GroupNameSymbolPattern}){{3,}})$",
    });

    public readonly List<BlendShapeGroup> Groups;

    public BlendShapeGrouping(HashSet<string> allKeys)
    {
        Groups = new List<BlendShapeGroup>();
        BuildGroups(allKeys);
    }

    private void BuildGroups(HashSet<string> allKeys)
    {
        Groups.Clear();
        Groups.Add(new BlendShapeGroup(DefaultGroupName));

        var allKeysArray = allKeys.ToArray();
        for (var i = 0; i < allKeysArray.Length; i++)
        {
            var shapeName = allKeysArray[i];
            var match = Regex.Match(shapeName, GroupNamePattern);
            if (match.Success)
            {
                var groupName = match.Groups.Cast<Group>().Skip(1).First(x => x.Success).Value;
                Groups.Add(new BlendShapeGroup(groupName));
            }

            Groups.Last().BlendShapeIndices.Add(i);
        }
    }

    public bool IsBlendShapeVisible(int index)
    {
        return Groups.Any(group => group.IsSelected && group.BlendShapeIndices.Contains(index));
    }
}

internal class GroupSelectionUI
{
    private readonly List<BlendShapeGroup> _groups;
    private readonly Action _onSelectionChanged;

    public GroupSelectionUI(List<BlendShapeGroup> groups, Action onSelectionChanged)
    {
        _groups = groups;
        _onSelectionChanged = onSelectionChanged;
    }

    public void OnGUI()
    {
        EditorGUI.BeginChangeCheck();

        foreach (var group in _groups)
        {
            var displayName = $"{group.Name} ({group.BlendShapeIndices.Count})";
            var newSelection = EditorGUILayout.Toggle(displayName, group.IsSelected);
            if (newSelection != group.IsSelected)
            {
                group.IsSelected = newSelection;
            }
        }

        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("All"))
        {
            SetAllGroups(true);
        }
        if (GUILayout.Button("None"))
        {
            SetAllGroups(false);
        }
        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck())
        {
            _onSelectionChanged?.Invoke();
        }
    }

    private void SetAllGroups(bool selected)
    {
        foreach (var group in _groups)
        {
            group.IsSelected = selected;
        }
        _onSelectionChanged?.Invoke();
    }
}

internal class BlendShapeSelectorManager : IDisposable
{
    public readonly string[] AllKeysArray;
    public readonly BlendShapeSet? FacialStyleSet;
    private readonly SerializedProperty _overrideFlagsProperty;
    private readonly SerializedProperty _overrideWeightsProperty;
    private readonly BlendShapeGrouping _grouping;
    private readonly Func<bool> _getApplyGroupFilterToSelected;
    private readonly Func<bool> _getApplyGroupFilterToUnselected;

    private readonly BlendShapeSelectorBase _selectedBlendShapeSelector;
    private readonly BlendShapeSelectorBase _unSelectedBlendShapeSelector;

    private readonly Action? _anyChangeCallback;
    private readonly Action<int>? _hoveredCallback;
    public BlendShapeSelectorManager(string[] allKeysArray, SerializedProperty overrideFlagsProperty, SerializedProperty overrideWeightsProperty, 
        BlendShapeGrouping grouping, BlendShapeSet? facialStyleSet, Func<bool> getApplyGroupFilterToSelected, Func<bool> getApplyGroupFilterToUnselected,
        Action? anyChangeCallback = null, Action<int>? hoveredCallback = null)
    {
        AllKeysArray = allKeysArray;
        FacialStyleSet = facialStyleSet;
        _overrideFlagsProperty = overrideFlagsProperty;
        _overrideWeightsProperty = overrideWeightsProperty;
        _grouping = grouping;
        _getApplyGroupFilterToSelected = getApplyGroupFilterToSelected;
        _getApplyGroupFilterToUnselected = getApplyGroupFilterToUnselected;
        _selectedBlendShapeSelector = new SelectedBlendShapeSelector(this);
        _unSelectedBlendShapeSelector = new UnSelectedBlendShapeSelector(this);

        _anyChangeCallback = anyChangeCallback;
        _hoveredCallback = hoveredCallback;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
    }

    public void Dispose()
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
    }

    public void DrawSelected()
    {
        _selectedBlendShapeSelector.OnGUI();
    }

    public void DrawUnSelected()
    {
        _unSelectedBlendShapeSelector.OnGUI();
    }

    public void Reload()
    {
        _selectedBlendShapeSelector.Reload();
        _unSelectedBlendShapeSelector.Reload();
    }

    public void ChangeWeight(int index, float weight)
    {
        _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue = weight;
        _anyChangeCallback?.Invoke();
    }

    public void Add(int index)
    {
        _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = true;
        _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue = 100f;
        _anyChangeCallback?.Invoke();
        Reload();   
    }

    public void AddWithWeight(int index, float weight)
    {
        _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = true;
        _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue = weight;
        _anyChangeCallback?.Invoke();
        Reload();
    }

    public void Remove(int index)
    {
        _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = false;
        _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue = 0f; // デフォルト値
        _anyChangeCallback?.Invoke();
        Reload();
    }

    public void Hovered(int index)
    {
        _hoveredCallback?.Invoke(index);
    }

    public bool ReadOverrideFlag(int index)
    {
        return _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue;
    }

    public float ReadWeight(int index)
    {
        return _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue;
    }

    public bool IsBlendShapeVisible(int index)
    {
        return _grouping.IsBlendShapeVisible(index);
    }

    public bool ShouldApplyGroupFilterToSelected()
    {
        return _getApplyGroupFilterToSelected();
    }

    public bool ShouldApplyGroupFilterToUnselected()
    {
        return _getApplyGroupFilterToUnselected();
    }

    private void OnUndoRedoPerformed()
    {
        EditorApplication.delayCall += () =>
        {
            _anyChangeCallback?.Invoke();
            Reload();
        };
    }
}

internal class SelectedBlendShapeSelector : BlendShapeSelectorBase
{
    private readonly BlendShapeSelectorManager _manager;
    public SelectedBlendShapeSelector(BlendShapeSelectorManager manager) : base(manager.AllKeysArray, index => 
        manager.ReadOverrideFlag(index) && (!manager.ShouldApplyGroupFilterToSelected() || manager.IsBlendShapeVisible(index)))
    {
        _manager = manager;
    }

    public override void DrawRow(BlendShapeTreeViewItem item, Rect rect, bool selected)
    {
        Profiler.BeginSample("SelectedBlendShapeSelector.DrawRow");

        // FacialStyleSetに含まれているかチェック
        var styleBlendShape = new BlendShape();
        var hasStyleValue = _manager.FacialStyleSet != null && _manager.FacialStyleSet.TryGetValue(item.BlendShapeName, out styleBlendShape);
        
        // スタイル値がある場合は背景を薄い色で強調
        if (hasStyleValue && Event.current.type == EventType.Repaint)
        {
            var bgColor = new Color(0.3f, 0.7f, 1f, 0.2f); // 薄い青色
            EditorGUI.DrawRect(rect, bgColor);
        }

        var blendShapeName = item.BlendShapeName;
        string displayText;
        GUIStyle labelStyle;
        if (hasStyleValue)
        {
            displayText = $"{blendShapeName} ({styleBlendShape.Weight:F1})";
            labelStyle = EditorStyles.boldLabel;
        }
        else
        {
            displayText = blendShapeName;
            labelStyle = EditorStyles.label;
        }

        var newWeight = EditorGUI.Slider(rect, displayText, _manager.ReadWeight(item.Index), 0f, 100f);
        if (newWeight != _manager.ReadWeight(item.Index))
        {
            _manager.ChangeWeight(item.Index, newWeight);
        }

        // クリックで削除
        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
            _manager.Remove(item.Index);
            Event.current.Use();
        }
        Profiler.EndSample();
    }

}

internal class UnSelectedBlendShapeSelector : BlendShapeSelectorBase
{
    private readonly BlendShapeSelectorManager _manager;
    public UnSelectedBlendShapeSelector(BlendShapeSelectorManager manager) : base(manager.AllKeysArray, index => 
        manager.ReadOverrideFlag(index) is false && (!manager.ShouldApplyGroupFilterToUnselected() || manager.IsBlendShapeVisible(index)))
    {
        _manager = manager;
    }

    public override void DrawRow(BlendShapeTreeViewItem item, Rect rect, bool selected)
    {
        Profiler.BeginSample("UnSelectedBlendShapeSelector.DrawRow");

        // FacialStyleSetに含まれているかチェック
        var styleBlendShape = new BlendShape();
        var hasStyleValue = _manager.FacialStyleSet != null && _manager.FacialStyleSet.TryGetValue(item.BlendShapeName, out styleBlendShape);
        
        // スタイル値がある場合は背景を薄い色で強調
        if (hasStyleValue && Event.current.type == EventType.Repaint)
        {
            var bgColor = new Color(0.3f, 0.7f, 1f, 0.2f); // 薄い青色
            EditorGUI.DrawRect(rect, bgColor);
        }

        Profiler.BeginSample("UnSelectedBlendShapeSelector.DrawRow.LabelField");
        string displayText;
        GUIStyle labelStyle;
        if (hasStyleValue)
        {
            displayText = $"{item.BlendShapeName} ({styleBlendShape.Weight:F1})";
            labelStyle = EditorStyles.boldLabel;
        }
        else
        {
            displayText = item.BlendShapeName;
            labelStyle = EditorStyles.label;
        }
        EditorGUI.LabelField(rect, displayText, labelStyle);
        Profiler.EndSample();

        Profiler.BeginSample("UnSelectedBlendShapeSelector.DrawRow.Contains");
        if (rect.Contains(Event.current.mousePosition))
        {
            _isHovered = true;
            _currentHoveredIndex = item.Index;

            if (Event.current.type == EventType.MouseDown)
            {
                if (hasStyleValue)
                {
                    _manager.AddWithWeight(item.Index, styleBlendShape.Weight);
                }
                else
                {
                    _manager.Add(item.Index);
                }
                Event.current.Use();
            }
        }
        Profiler.EndSample();
        
        Profiler.EndSample();
    }

    private bool _isHovered = false;
    private int _previousHoveredIndex = -1;
    private int _currentHoveredIndex = 1;
    public override void OnGUI()
    {
        _isHovered = false;
        _previousHoveredIndex = _currentHoveredIndex;
        base.OnGUI();
        if (_isHovered)
        {
            if (_previousHoveredIndex == _currentHoveredIndex)
            {
                // 前フレームと同じ位置をホバー
            }
            else
            {
                // 前フレームと違う位置をホバー
                _manager.Hovered(_currentHoveredIndex);
            }
        }
        else
        {
            if (_previousHoveredIndex == -1)
            {
                // ホバーアウトを継続
            }
            else
            {
                // ホバーアウト
                _manager.Hovered(-1);
                _currentHoveredIndex = -1;
            }
        }
    }
}

internal abstract class BlendShapeSelectorBase
{
    private readonly SearchField _searchField;
    private readonly BlendShapeTreeView _blendShapeTreeView;

    public BlendShapeSelectorBase(string[] allKeysArray, Func<int, bool> _display)
    {
        _searchField = new SearchField();
        _blendShapeTreeView = new BlendShapeTreeView(allKeysArray, _display, DrawRow);
    }

    public virtual void OnGUI()
    {
        var searchRect = EditorGUILayout.GetControlRect(false, GUILayout.ExpandWidth(true), GUILayout.Height(EditorGUIUtility.singleLineHeight));
        _blendShapeTreeView.searchString = _searchField.OnGUI(searchRect, _blendShapeTreeView.searchString);
        var treeViewRect = EditorGUILayout.GetControlRect(false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        _blendShapeTreeView.OnGUI(treeViewRect);
    }

    public abstract void DrawRow(BlendShapeTreeViewItem item, Rect rect, bool selected);

    public void Reload()
    {
        _blendShapeTreeView.Reload();
    }
}

internal class BlendShapeTreeView : TreeView
{
    private readonly string[] _blendShapeNames;
    private readonly Func<int, bool> _display;
    private readonly Action<BlendShapeTreeViewItem, Rect, bool> _drawRow;

    public BlendShapeTreeView(string[] allKeysArray, Func<int, bool> display, Action<BlendShapeTreeViewItem, Rect, bool> drawRow) : base(new TreeViewState())
    {
        _blendShapeNames = allKeysArray;
        _display = display;
        _drawRow = drawRow;
        Reload();
    }

    protected override TreeViewItem BuildRoot()
    {
        var root = new TreeViewItem(-1, -1, "Root");

        var items = new List<TreeViewItem>();
        for (var i = 0; i < _blendShapeNames.Length; i++)
        {
            if (_display(i) is false) continue;
            items.Add(new BlendShapeTreeViewItem(i, 0, _blendShapeNames[i], i));
        }
        SetupParentsAndChildrenFromDepths(root, items);
        return root;
    }

    protected override void RowGUI(RowGUIArgs args)
    {
        _drawRow((args.item as BlendShapeTreeViewItem)!, args.rowRect, args.selected);
    }
}

internal class BlendShapeTreeViewItem : TreeViewItem
{
    public readonly string BlendShapeName;
    public readonly int Index;

    public BlendShapeTreeViewItem(int id, int depth, string blendShapeName, int index) : base(id, depth, blendShapeName)
    {
        BlendShapeName = blendShapeName;
        Index = index;
    }
}