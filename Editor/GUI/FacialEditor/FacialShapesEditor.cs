using UnityEditor.IMGUI.Controls;
using nadena.dev.ndmf.preview;
using com.aoyon.facetune.preview;

namespace com.aoyon.facetune.ui;

internal class FacialShapesEditor : EditorWindow
{
    public SkinnedMeshRenderer Renderer = null!;
    public Mesh Mesh = null!;
    public IReadOnlyList<BlendShape> BaseShapes = null!;
    [SerializeField] private bool[] _overrideFlags = null!;
    [SerializeField] private float[] _overrideWeights = null!;

    private BlendShapeSelectorManager _selector = null!;

    private bool _previewOnHover = true;
    public bool PreviewOnHover
    {
        get => _previewOnHover;
        set
        {
            if (_previewOnHover == value) return;
            _previewOnHover = value;
            if (value) _previewShapes.Value = GetResult();
            else _previewShapes.Value = new();
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

    private readonly PublishedValue<BlendShapeSet> _previewShapes = new(new());

    private AnimationClip? _sourceClip = null;
    private ClipExcludeOption _clipExcludeOption = ClipExcludeOption.ExcludeZeroWeight;

    private int _previousUndoGroup = -1;

    public static FacialShapesEditor? OpenEditor(SkinnedMeshRenderer renderer, Mesh mesh, IEnumerable<BlendShape> defaultShapes, BlendShapeSet defaultOverrides)
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
        window.Init(renderer, mesh, defaultShapes, defaultOverrides);
        return window;
    }

    public void Init(SkinnedMeshRenderer renderer, Mesh mesh, IEnumerable<BlendShape> defaultShapes, BlendShapeSet defaultOverrides)
    {
        Renderer = renderer;
        Mesh = mesh;

        BaseShapes = defaultShapes.ToList().AsReadOnly();
        var mapping = defaultOverrides.BlendShapes.ToDictionary(x => x.Name, x => x.Weight);
        _overrideFlags = BaseShapes.Select(x => mapping.ContainsKey(x.Name)).ToArray();
        _overrideWeights = BaseShapes.Select(x => mapping.ContainsKey(x.Name) ? mapping[x.Name] : -1).ToArray();

        _serializedObject = new SerializedObject(this);
        _overrideFlagsProperty = _serializedObject.FindProperty(nameof(_overrideFlags));
        _overrideWeightsProperty = _serializedObject.FindProperty(nameof(_overrideWeights));

        _selector = new BlendShapeSelectorManager(BaseShapes, _overrideFlagsProperty, _overrideWeightsProperty, OnAnyChangeCallback, OnHoveredCallback);
        _highlightBlendShapeProcessor = new HighlightBlendShapeProcessor(renderer, mesh);

        saveChangesMessage = "This window may have unsaved changes. Would you like to save?";
        hasUnsavedChanges = false;

        _previousUndoGroup = Undo.GetCurrentGroup();
        Undo.IncrementCurrentGroup();

        _previewShapes.Value = GetResult();
        EditingShapesPreview.Start(renderer, _previewShapes);
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
            if (index == -1) _previewShapes.Value = GetResult();
            else _previewShapes.Value = GetResult().Add(new BlendShape(BaseShapes[index].Name, 100f));
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
        if (PreviewOnHover) _previewShapes.Value = GetResult();
    }

    public void RegisterApplyCallback(Action<BlendShapeSet> onApply)
    {
        _onApply = onApply;
    }

    public override void SaveChanges()
    {
        _onApply?.Invoke(GetResult());
        hasUnsavedChanges = false;
        base.SaveChanges();
    }

    public BlendShapeSet GetResult()
    {
        _serializedObject.ApplyModifiedProperties();
        _serializedObject.Update();
        var result = new BlendShapeSet();
        for (var i = 0; i < _overrideWeightsProperty.arraySize; i++)
        {
            var overrideFlag = _overrideFlagsProperty.GetArrayElementAtIndex(i).boolValue;
            if (!overrideFlag) continue;

            var weight = _overrideWeightsProperty.GetArrayElementAtIndex(i).floatValue;
            result.Add(new BlendShape(BaseShapes[i].Name, weight));
        }
        return result;
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

        var newBlendShapes = BlendShapeUtility.GetBlendShapeSetFromClip(_sourceClip, _clipExcludeOption, new BlendShapeSet(BaseShapes));

        var mapping = new BlendShapeSet(BaseShapes).AddRange(GetResult()).BlendShapes.Select((x, i) => (x.Name, i)).ToDictionary(x => x.Name, x => x.i);
        foreach (var blendShape in newBlendShapes.BlendShapes)
        {
            if (mapping.TryGetValue(blendShape.Name, out var index))
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

internal class BlendShapeSelectorManager : IDisposable
{
    public readonly IReadOnlyList<BlendShape> BaseShapes;
    private readonly SerializedProperty _overrideFlagsProperty;
    private readonly SerializedProperty _overrideWeightsProperty;

    private readonly BlendShapeSelectorBase _selectedBlendShapeSelector;
    private readonly BlendShapeSelectorBase _unSelectedBlendShapeSelector;

    private readonly Action? _anyChangeCallback;
    private readonly Action<int>? _hoveredCallback;
    public BlendShapeSelectorManager(IReadOnlyList<BlendShape> baseShapes, SerializedProperty overrideFlagsProperty, SerializedProperty overrideWeightsProperty, 
        Action? anyChangeCallback = null, Action<int>? hoveredCallback = null)
    {
        BaseShapes = baseShapes;
        _overrideFlagsProperty = overrideFlagsProperty;
        _overrideWeightsProperty = overrideWeightsProperty;
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

    public void Remove(int index)
    {
        _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = false;
        _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue = BaseShapes[index].Weight;
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
    public SelectedBlendShapeSelector(BlendShapeSelectorManager manager) : base(manager.BaseShapes, index => manager.ReadOverrideFlag(index))
    {
        _manager = manager;
    }

    private const float ButtonWidth = 20f;
    public override void DrawRow(BlendShapeTreeViewItem item, Rect rect, bool selected)
    {
        Profiler.BeginSample("SelectedBlendShapeSelector.DrawRow");
        var sliderRect = new Rect(rect.x, rect.y, rect.width - ButtonWidth, rect.height);
        var buttonRect = new Rect(rect.x + rect.width - ButtonWidth, rect.y, ButtonWidth, rect.height);

        var blendShape = item.BlendShape;

        var newWeight = EditorGUI.Slider(sliderRect, blendShape.Name, _manager.ReadWeight(item.Index), 0f, 100f);
        if (newWeight != _manager.ReadWeight(item.Index))
        {
            _manager.ChangeWeight(item.Index, newWeight);
        }

        if (GUI.Button(buttonRect, "✕"))
        {
            _manager.Remove(item.Index);
        }

        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
            _manager.Remove(item.Index);
        }
        Profiler.EndSample();
    }

}

internal class UnSelectedBlendShapeSelector : BlendShapeSelectorBase
{
    private readonly BlendShapeSelectorManager _manager;
    public UnSelectedBlendShapeSelector(BlendShapeSelectorManager manager) : base(manager.BaseShapes, index => manager.ReadOverrideFlag(index) is false)
    {
        _manager = manager;
    }

    private const float ButtonWidth = 20f;
    public override void DrawRow(BlendShapeTreeViewItem item, Rect rect, bool selected)
    {
        Profiler.BeginSample("UnSelectedBlendShapeSelector.DrawRow");

        Profiler.BeginSample("UnSelectedBlendShapeSelector.DrawRow.Rect");
        var labelRect = new Rect(rect.x, rect.y, rect.width - ButtonWidth, rect.height);
        var buttonRect = new Rect(rect.x + rect.width - ButtonWidth, rect.y, ButtonWidth, rect.height);
        Profiler.EndSample();

        Profiler.BeginSample("UnSelectedBlendShapeSelector.DrawRow.LabelField");
        EditorGUI.LabelField(labelRect, item.BlendShape.Name);
        Profiler.EndSample();

        Profiler.BeginSample("UnSelectedBlendShapeSelector.DrawRow.Button");
        if (GUI.Button(buttonRect, "+"))
        {
            _manager.Add(item.Index);
        }
        Profiler.EndSample();

        Profiler.BeginSample("UnSelectedBlendShapeSelector.DrawRow.Contains");
        if (rect.Contains(Event.current.mousePosition))
        {
            _isHovered = true;
            _currentHoveredIndex = item.Index;

            if (Event.current.type == EventType.MouseDown)
            {
                _manager.Add(item.Index);
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

    public BlendShapeSelectorBase(IReadOnlyList<BlendShape> baseShapes, Func<int, bool> _display)
    {
        _searchField = new SearchField();
        _blendShapeTreeView = new BlendShapeTreeView(baseShapes, _display, DrawRow);
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
    private readonly IReadOnlyList<BlendShape> _blendShapes;
    private readonly Func<int, bool> _display;
    private readonly Action<BlendShapeTreeViewItem, Rect, bool> _drawRow;

    public BlendShapeTreeView(IReadOnlyList<BlendShape> blendShapes, Func<int, bool> display, Action<BlendShapeTreeViewItem, Rect, bool> drawRow) : base(new TreeViewState())
    {
        _blendShapes = blendShapes;
        _display = display;
        _drawRow = drawRow;
        Reload();
    }

    protected override TreeViewItem BuildRoot()
    {
        var root = new TreeViewItem(-1, -1, "Root");

        var items = new List<TreeViewItem>();
        for (var i = 0; i < _blendShapes.Count; i++)
        {
            if (_display(i) is false) continue;
            items.Add(new BlendShapeTreeViewItem(i, 0, _blendShapes[i], i));
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
    public readonly BlendShape BlendShape;
    public readonly int Index;

    public BlendShapeTreeViewItem(int id, int depth, BlendShape blendShape, int index) : base(id, depth, blendShape.Name)
    {
        BlendShape = blendShape;
        Index = index;
    }
}