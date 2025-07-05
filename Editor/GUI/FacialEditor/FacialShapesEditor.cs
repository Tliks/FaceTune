using UnityEditor.IMGUI.Controls;
using nadena.dev.ndmf.preview;
using aoyon.facetune.preview;
using System.Text.RegularExpressions;

namespace aoyon.facetune.ui;

internal class StyleShapeManager
{
    private readonly BlendShapeSet _facialStyleSet;
    
    public StyleShapeManager(BlendShapeSet facialStyleSet)
    {
        _facialStyleSet = facialStyleSet;
    }
    
    public bool IsStyleShape(string name)
    {
        return _facialStyleSet.Contains(name);
    }
    
    public float GetStyleWeight(string name)
    {
        return _facialStyleSet.TryGetValue(name, out var shape) ? shape.Weight : 0f;
    }
    
    public bool IsEditedFromStyle(string name, float currentWeight)
    {
        if (!IsStyleShape(name)) return false;
        var styleWeight = GetStyleWeight(name);
        return Math.Abs(currentWeight - styleWeight) > 0.01f;
    }
    
    public IEnumerable<string> GetStyleShapeNames(string[] allKeys)
    {
        return allKeys.Where(IsStyleShape).OrderBy(x => x);
    }
}

internal class FacialShapesEditor : EditorWindow
{
    public SkinnedMeshRenderer Renderer = null!;
    public Mesh Mesh = null!;
    public HashSet<string> AllKeys = null!;
    public string[] AllKeysArray = null!; 
    public BlendShapeSet FacialStyleSet = new();
    [SerializeField] private bool[] _overrideFlags = null!;
    [SerializeField] private float[] _overrideWeights = null!;

    private BlendShapeSelectorManager _selector = null!;
    private BlendShapeGrouping _grouping = null!;
    private GroupSelectionUI _groupSelectionUI = null!;
    private StyleShapeManager _styleShapeManager = null!;

    [SerializeField] private bool _applyGroupFilterToSelected = false;
    [SerializeField] private bool _applyGroupFilterToUnselected = true;
    [SerializeField] private int _latestAddedIndex = -1;

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
    private SerializedProperty _latestAddedIndexProperty = null!;

    private BlendShapeSet _emptySet = new();
    private readonly PublishedValue<BlendShapeSet> _previewShapes = new(new());
    private int _hoveredIndex = -1;

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
        AllKeysArray = allKeys.ToArray();
        FacialStyleSet = facialStyleSet ?? new();
        
        // Style shapeは初期状態では未選択、defaultOverridesで明示的に指定されたもののみ選択状態
        _overrideFlags = AllKeysArray.Select(x => defaultOverrides.Contains(x)).ToArray();
        
        // weight値の優先順位：defaultOverrides > facialStyle > -1f
        _overrideWeights = AllKeysArray.Select(x =>
        {
            if (defaultOverrides.TryGetValue(x, out var defaultValue))
                return defaultValue.Weight;
            if (FacialStyleSet.TryGetValue(x, out var styleValue))
                return styleValue.Weight;
            return -1f;
        }).ToArray();

        _serializedObject = new SerializedObject(this);
        _overrideFlagsProperty = _serializedObject.FindProperty(nameof(_overrideFlags));
        _overrideWeightsProperty = _serializedObject.FindProperty(nameof(_overrideWeights));
        _latestAddedIndexProperty = _serializedObject.FindProperty(nameof(_latestAddedIndex));

        _applyGroupFilterToSelected = false;
        _applyGroupFilterToUnselected = true;
        
        _grouping = new BlendShapeGrouping(AllKeys);
        _groupSelectionUI = new GroupSelectionUI(_grouping.Groups, OnGroupSelectionChanged);
        _styleShapeManager = new StyleShapeManager(FacialStyleSet);
                
        _selector = new BlendShapeSelectorManager(AllKeysArray, _overrideFlagsProperty, _overrideWeightsProperty, _latestAddedIndexProperty, _grouping, _styleShapeManager, 
            () => _applyGroupFilterToSelected, () => _applyGroupFilterToUnselected, OnAnyChangeCallback, OnHoveredCallback);
        _highlightBlendShapeProcessor = new HighlightBlendShapeProcessor(renderer, mesh);
    
        saveChangesMessage = "This window may have unsaved changes. Would you like to save?";
        hasUnsavedChanges = false;

        _previousUndoGroup = Undo.GetCurrentGroup();
        Undo.IncrementCurrentGroup();

        _previewShapes.Value = new();
        EditingShapesPreview.Start(renderer, _previewShapes!);
        ForceUpdatePreviewShapes();
    }

    private void OnGroupSelectionChanged()
    {
        _selector.Reload();
    }

    private void OnAnyChangeCallback()
    {
        hasUnsavedChanges = true;
        UpdatePreviewShapes();
    }

    private void OnHoveredCallback(int index)
    {
        _hoveredIndex = index;
        UpdatePreviewShapes();
        
        if (HighlightOnHover)
        {
            if (index == -1) _highlightBlendShapeProcessor.ClearHighlight();
            else _highlightBlendShapeProcessor.HilightBlendShapeFor(index);
        }
    }

    private void EditedAndRefresh()
    {
        hasUnsavedChanges = true;
        UpdatePreviewShapes();
        _selector.Reload();
    }
    
    private void UpdatePreviewShapes()
    {
        if (!PreviewOnHover) return;
        ForceUpdatePreviewShapes();
    }
    
    private void ForceUpdatePreviewShapes()
    {
        var current = _previewShapes.Value;
        current.Clear();

        var set = current;
        // 全てのKeyを0で初期化
        foreach (var key in AllKeys)
        {
            set.Add(new BlendShape(key, 0f));
        }        
        // 顔つきを反映
        foreach (var styleShape in FacialStyleSet)
        {
            set.Add(new BlendShape(styleShape.Name, styleShape.Weight));
        }
        // 結果を反映
        GetResult(set);
        
        // ホバーされたindexを上書き追加
        if (_hoveredIndex >= 0 && _hoveredIndex < AllKeysArray.Length)
        {
            var blendShapeName = AllKeysArray[_hoveredIndex];
            set.Add(new BlendShape(blendShapeName, 100f));
        }

        _previewShapes.Value = _emptySet;
        _previewShapes.Value = set;
    }
    
    public void GetResult(BlendShapeSet resultToAdd)
    {
        _serializedObject.ApplyModifiedProperties();
        _serializedObject.Update();
        
        for (var i = 0; i < _overrideWeightsProperty.arraySize; i++)
        {
            var weight = _overrideWeightsProperty.GetArrayElementAtIndex(i).floatValue;
            var blendShapeName = AllKeysArray[i];
            var overrideFlag = _overrideFlagsProperty.GetArrayElementAtIndex(i).boolValue;
            var isStyleShape = _styleShapeManager.IsStyleShape(blendShapeName); // Dictionary検索で高速
            
            // Style shapeは編集されている場合のみ、Custom shapeは選択されている場合のみ
            bool shouldInclude = isStyleShape 
                ? _styleShapeManager.IsEditedFromStyle(blendShapeName, weight) 
                : overrideFlag;
            
            if (shouldInclude)
            {
                resultToAdd.Add(new BlendShape(blendShapeName, weight));
            }
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
        
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        _applyGroupFilterToSelected = EditorGUILayout.Toggle("Selected", _applyGroupFilterToSelected);
        _applyGroupFilterToUnselected = EditorGUILayout.Toggle("Unselected", _applyGroupFilterToUnselected);
        if (EditorGUI.EndChangeCheck())
        {
            _selector.Reload();
        }
        EditorGUILayout.EndHorizontal();
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

        var groupsPerRow = 2;
        for (int i = 0; i < _groups.Count; i++)
        {
            if (i % groupsPerRow == 0)
            {
                if (i > 0) EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }

            var group = _groups[i];
            var displayName = $"{group.Name}({group.BlendShapeIndices.Count})";
            var newSelection = EditorGUILayout.Toggle(displayName, group.IsSelected, GUILayout.ExpandWidth(true));
            if (newSelection != group.IsSelected)
            {
                group.IsSelected = newSelection;
            }
        }
        
        if (_groups.Count % groupsPerRow != 0)
        {
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("All", GUILayout.Width(60)))
        {
            SetAllGroups(true);
        }
        if (GUILayout.Button("None", GUILayout.Width(60)))
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
    private readonly SerializedProperty _overrideFlagsProperty;
    private readonly SerializedProperty _overrideWeightsProperty;
    private readonly SerializedProperty _latestAddedIndexProperty;
    private readonly BlendShapeGrouping _grouping;
    private readonly StyleShapeManager _styleShapeManager;
    private readonly Func<bool> _getApplyGroupFilterToSelected;
    private readonly Func<bool> _getApplyGroupFilterToUnselected;

    private readonly BlendShapeSelectorBase _selectedBlendShapeSelector;
    private readonly BlendShapeSelectorBase _unSelectedBlendShapeSelector;

    private readonly Action? _anyChangeCallback;
    private readonly Action<int>? _hoveredCallback;
    
    public BlendShapeSelectorManager(
        string[] allKeysArray, 
        SerializedProperty overrideFlagsProperty, 
        SerializedProperty overrideWeightsProperty, 
        SerializedProperty latestAddedIndexProperty,
        BlendShapeGrouping grouping, 
        StyleShapeManager styleShapeManager,
        Func<bool> getApplyGroupFilterToSelected, 
        Func<bool> getApplyGroupFilterToUnselected,
        Action? anyChangeCallback = null, 
        Action<int>? hoveredCallback = null)
    {
        AllKeysArray = allKeysArray;
        _overrideFlagsProperty = overrideFlagsProperty;
        _overrideWeightsProperty = overrideWeightsProperty;
        _latestAddedIndexProperty = latestAddedIndexProperty;
        _grouping = grouping;
        _styleShapeManager = styleShapeManager;
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
        
        // Style shapeを編集した場合もoverrideFlagをtrueに設定
        var name = AllKeysArray[index];
        if (_styleShapeManager.IsStyleShape(name))
        {
            _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = true;
        }
        
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
        
        // Style shapeでない場合のみ最新追加要素として記録
        var name = AllKeysArray[index];
        if (!_styleShapeManager.IsStyleShape(name))
        {
            _latestAddedIndexProperty.intValue = index;
        }
        
        _anyChangeCallback?.Invoke();
        Reload();
    }

    public void Remove(int index)
    {
        _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = false;
        _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue = 0f; // デフォルト値
        
        // 削除された要素が最新追加要素だった場合はリセット
        if (_latestAddedIndexProperty.intValue == index)
        {
            _latestAddedIndexProperty.intValue = -1;
        }
        
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
    
    public StyleShapeManager GetStyleShapeManager()
    {
        return _styleShapeManager;
    }
    
    public bool IsLatestAdded(int index)
    {
        return _latestAddedIndexProperty.intValue == index;
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
    {
        var name = manager.AllKeysArray[index];
        var styleManager = manager.GetStyleShapeManager();
        
        // Style shapesは常に表示
        if (styleManager.IsStyleShape(name))
        {
            return !manager.ShouldApplyGroupFilterToSelected() || manager.IsBlendShapeVisible(index);
        }
        
        // Custom shapesはオーバーライドされている場合のみ表示
        return manager.ReadOverrideFlag(index) && (!manager.ShouldApplyGroupFilterToSelected() || manager.IsBlendShapeVisible(index));
    })
    {
        _manager = manager;
    }

    protected override void DrawBatchOperationButtons()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Batch:", GUILayout.Width(40));
        
        if (GUILayout.Button("All 0", GUILayout.Width(50)))
        {
            SetAllVisibleWeights(0f);
        }
        
        if (GUILayout.Button("All 100", GUILayout.Width(60)))
        {
            SetAllVisibleWeights(100f);
        }
        
        if (GUILayout.Button("Remove All", GUILayout.Width(80)))
        {
            RemoveAllCustomShapes();
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);
    }
    
    private void RemoveAllCustomShapes()
    {
        for (int i = 0; i < _manager.AllKeysArray.Length; i++)
        {
            var name = _manager.AllKeysArray[i];
            
            // Style shapesは除外、選択されているCustom shapesのみを削除
            if (_manager.GetStyleShapeManager().IsStyleShape(name)) continue;
            
            if (_manager.ReadOverrideFlag(i))
            {
                _manager.Remove(i);
            }
        }
    }
    
    private void SetAllVisibleWeights(float weight)
    {
        for (int i = 0; i < _manager.AllKeysArray.Length; i++)
        {
            var name = _manager.AllKeysArray[i];
            var styleManager = _manager.GetStyleShapeManager();
            
            // 表示条件をチェック
            bool shouldShow;
            if (styleManager.IsStyleShape(name))
            {
                shouldShow = !_manager.ShouldApplyGroupFilterToSelected() || _manager.IsBlendShapeVisible(i);
            }
            else
            {
                shouldShow = _manager.ReadOverrideFlag(i) && (!_manager.ShouldApplyGroupFilterToSelected() || _manager.IsBlendShapeVisible(i));
            }
            
            if (shouldShow)
            {
                _manager.ChangeWeight(i, weight);
            }
        }
    }

    public override void DrawRow(BlendShapeTreeViewItem item, Rect rect, bool selected)
    {
        Profiler.BeginSample("SelectedBlendShapeSelector.DrawRow");

        var styleManager = _manager.GetStyleShapeManager();
        var isStyleShape = styleManager.IsStyleShape(item.BlendShapeName);
        var isLatestAdded = _manager.IsLatestAdded(item.Index);
        var currentWeight = _manager.ReadWeight(item.Index);
        
        // 右側ボタン群のスペースを確保（0, 100, R/✕ボタン）
        var button0Width = 22f;
        var button100Width = 30f;
        var mainButtonWidth = 22f;
        var buttonSpacing = 2f;
        var totalButtonWidth = button0Width + button100Width + mainButtonWidth + buttonSpacing * 2;
        
        // カラーバー用のスペースを確保（Style shapeまたは最新追加要素の場合）
        var hasColorBar = isStyleShape || isLatestAdded;
        var leftMargin = hasColorBar ? 8f : 3f; // カラーバーがある場合は少し多めにマージン
        var sliderRect = new Rect(rect.x + leftMargin, rect.y, rect.width - totalButtonWidth - leftMargin - 5f, rect.height);
        var button0Rect = new Rect(rect.xMax - totalButtonWidth, rect.y, button0Width, rect.height);
        var button100Rect = new Rect(button0Rect.xMax + buttonSpacing, rect.y, button100Width, rect.height);
        var mainButtonRect = new Rect(button100Rect.xMax + buttonSpacing, rect.y, mainButtonWidth, rect.height);

        // カラーバーの表示（Style shapeまたは最新追加要素）
        if ((isStyleShape || isLatestAdded) && Event.current.type == EventType.Repaint)
        {
            Color barColor;
            if (isStyleShape)
            {
                barColor = styleManager.IsEditedFromStyle(item.BlendShapeName, currentWeight) 
                    ? new Color(0.2f, 0.6f, 1f, 0.8f)    // Style編集済み：落ち着いた青色
                    : new Color(0.9f, 0.7f, 0.2f, 0.8f); // Style未編集：落ち着いた金色
            }
            else
            {
                barColor = new Color(0.2f, 0.8f, 0.2f, 0.8f); // 最新追加：落ち着いた緑色
            }
            
            var barRect = new Rect(rect.x + 1f, rect.y + 1f, 2f, rect.height - 2f);
            EditorGUI.DrawRect(barRect, barColor);
        }

        var displayText = item.BlendShapeName;

        var newWeight = EditorGUI.Slider(sliderRect, displayText, currentWeight, 0f, 100f);
        if (newWeight != currentWeight)
        {
            _manager.ChangeWeight(item.Index, newWeight);
        }

        if (GUI.Button(button0Rect, "0"))
        {
            _manager.ChangeWeight(item.Index, 0f);
        }
        
        if (GUI.Button(button100Rect, "100"))
        {
            _manager.ChangeWeight(item.Index, 100f);
        }

        if (isStyleShape)
        {
            // Style shapeの場合：Resetボタン（編集されていない場合は無効化）
            var isEdited = styleManager.IsEditedFromStyle(item.BlendShapeName, currentWeight);
            EditorGUI.BeginDisabledGroup(!isEdited);
            if (GUI.Button(mainButtonRect, "R"))
            {
                var originalWeight = styleManager.GetStyleWeight(item.BlendShapeName);
                _manager.ChangeWeight(item.Index, originalWeight);
            }
            EditorGUI.EndDisabledGroup();
        }
        else
        {
            // Custom shapeの場合：Removeボタン
            if (GUI.Button(mainButtonRect, "✕"))
            {
                _manager.Remove(item.Index);
            }
        }
        
        Profiler.EndSample();
    }

}

internal class UnSelectedBlendShapeSelector : BlendShapeSelectorBase
{
    private readonly BlendShapeSelectorManager _manager;
    private float _addWeight = 100f; // 追加時のデフォルト重み
    
    public UnSelectedBlendShapeSelector(BlendShapeSelectorManager manager) : base(manager.AllKeysArray, index => 
    {
        var name = manager.AllKeysArray[index];
        if (manager.GetStyleShapeManager().IsStyleShape(name)) return false;
        
        // Custom shapesでオーバーライドされていないもののみ表示
        return !manager.ReadOverrideFlag(index) && (!manager.ShouldApplyGroupFilterToUnselected() || manager.IsBlendShapeVisible(index));
    })
    {
        _manager = manager;
    }

    protected override void DrawBatchOperationButtons()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Batch:", GUILayout.Width(40));
        
        if (GUILayout.Button("Add All", GUILayout.Width(60)))
        {
            AddAllVisibleShapes();
        }
        
        EditorGUILayout.LabelField("Weight:", GUILayout.Width(45));
        _addWeight = EditorGUILayout.FloatField(_addWeight, GUILayout.Width(50));
        _addWeight = Mathf.Clamp(_addWeight, 0f, 100f);
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);
    }
    
    private void AddAllVisibleShapes()
    {
        for (int i = 0; i < _manager.AllKeysArray.Length; i++)
        {
            var name = _manager.AllKeysArray[i];
            
            if (_manager.GetStyleShapeManager().IsStyleShape(name)) continue;
            
            bool shouldShow = !_manager.ReadOverrideFlag(i) && 
                             (!_manager.ShouldApplyGroupFilterToUnselected() || _manager.IsBlendShapeVisible(i));
            
            if (shouldShow)
            {
                _manager.AddWithWeight(i, _addWeight);
            }
        }
    }

    public override void DrawRow(BlendShapeTreeViewItem item, Rect rect, bool selected)
    {
        Profiler.BeginSample("UnSelectedBlendShapeSelector.DrawRow");

        var leftMargin = 8f;
        var labelRect = new Rect(rect.x + leftMargin, rect.y, rect.width - leftMargin - 5, rect.height);
        
        Profiler.BeginSample("UnSelectedBlendShapeSelector.DrawRow.LabelField");
        EditorGUI.LabelField(labelRect, item.BlendShapeName, EditorStyles.label);
        Profiler.EndSample();

        Profiler.BeginSample("UnSelectedBlendShapeSelector.DrawRow.MouseEvent");
        if (rect.Contains(Event.current.mousePosition))
        {
            _isHovered = true;
            _currentHoveredIndex = item.Index;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                _manager.AddWithWeight(item.Index, _addWeight);
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
        
        // サブクラスでのボタン表示
        DrawBatchOperationButtons();
        
        var treeViewRect = EditorGUILayout.GetControlRect(false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        _blendShapeTreeView.OnGUI(treeViewRect);
    }

    protected virtual void DrawBatchOperationButtons()
    {
        // サブクラスでオーバーライド
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
    
    // 交互の背景色
    private static readonly Color EvenRowColor = new Color(0.2f, 0.2f, 0.2f, 0.2f);
    private static readonly Color OddRowColor = new Color(0.25f, 0.25f, 0.25f, 0.2f);
    private static readonly Color HoverColor = new Color(0.3f, 0.5f, 0.8f, 0.3f);

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
        var item = args.item as BlendShapeTreeViewItem;
        if (item == null) return;
        
        // 背景色の描画（交互 + ホバー）
        if (Event.current.type == EventType.Repaint)
        {
            var isHovered = args.rowRect.Contains(Event.current.mousePosition);
            Color bgColor;
            
            if (isHovered)
            {
                bgColor = HoverColor;
            }
            else
            {
                bgColor = (args.row % 2 == 0) ? EvenRowColor : OddRowColor;
            }
            
            EditorGUI.DrawRect(args.rowRect, bgColor);
        }
        
        // ホバー状態の変化を検出（MouseMoveではRepaintを呼ぶだけ）
        if (Event.current.type == EventType.MouseMove && args.rowRect.Contains(Event.current.mousePosition))
        {
            Repaint();
        }
        
        _drawRow(item, args.rowRect, args.selected);
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