using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using nadena.dev.ndmf.preview;
using aoyon.facetune.preview;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace aoyon.facetune.ui
{
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
}

internal class BlendShapeOverrideManager
{
    private readonly SerializedObject _serializedObject;
    private readonly SerializedProperty _overrideFlagsProperty;
    private readonly SerializedProperty _overrideWeightsProperty;
    private readonly string[] _allKeysArray;
    private readonly StyleShapeManager _styleManager;

    public BlendShapeOverrideManager(SerializedObject serializedObject, string[] allKeysArray, StyleShapeManager styleManager)
    {
        _serializedObject = serializedObject;
        _allKeysArray = allKeysArray;
        _styleManager = styleManager;
        
        _overrideFlagsProperty = serializedObject.FindProperty("_overrideFlags");
        _overrideWeightsProperty = serializedObject.FindProperty("_overrideWeights");
    }

    public bool IsShapeOverridden(string shapeName)
    {
        var index = Array.IndexOf(_allKeysArray, shapeName);
        if (index < 0) return false;

        if (_styleManager.IsStyleShape(shapeName))
        {
            var currentWeight = GetShapeWeight(shapeName);
            return _styleManager.IsEditedFromStyle(shapeName, currentWeight);
        }
        else
        {
            return _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue;
        }
    }

    public float GetShapeWeight(string shapeName)
    {
        var index = Array.IndexOf(_allKeysArray, shapeName);
        if (index >= 0)
        {
            return _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue;
        }
        return 0f;
    }

    public void SetShapeWeight(string shapeName, float weight)
    {
        var index = Array.IndexOf(_allKeysArray, shapeName);
        if (index < 0) return;

        _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue = weight;
        
        if (_styleManager.IsStyleShape(shapeName))
        {
            // Facial style: flagは編集されているかどうかで決まる
            var isEdited = _styleManager.IsEditedFromStyle(shapeName, weight);
            _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = isEdited;
        }
        else
        {
            // Custom: 明示的に選択されている場合のみflagを立てる
            _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = true;
        }
        
        _serializedObject.ApplyModifiedProperties();
    }

    public void AddCustomShape(string shapeName, float weight)
    {
        var index = Array.IndexOf(_allKeysArray, shapeName);
        if (index < 0) return;

        _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = true;
        _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue = weight;
        _serializedObject.ApplyModifiedProperties();
    }

    public void RemoveCustomShape(string shapeName)
    {
        var index = Array.IndexOf(_allKeysArray, shapeName);
        if (index < 0) return;

        _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = false;
        _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue = 0f;
        _serializedObject.ApplyModifiedProperties();
    }

    public void ResetStyleShape(string shapeName)
    {
        if (!_styleManager.IsStyleShape(shapeName)) return;
        
        var styleWeight = _styleManager.GetStyleWeight(shapeName);
        SetShapeWeight(shapeName, styleWeight);
    }

    public BlendShapeSet GetOverrideResult()
    {
        _serializedObject.ApplyModifiedProperties();
        _serializedObject.Update();
        
        var result = new BlendShapeSet();
        
        for (var i = 0; i < _overrideWeightsProperty.arraySize; i++)
        {
            var weight = _overrideWeightsProperty.GetArrayElementAtIndex(i).floatValue;
            var blendShapeName = _allKeysArray[i];
            var overrideFlag = _overrideFlagsProperty.GetArrayElementAtIndex(i).boolValue;
            
            if (overrideFlag)
            {
                result.Add(new BlendShape(blendShapeName, weight));
            }
        }
        
        return result;
    }

    public List<BlendShape> GetSelectedStyleShapes()
    {
        var result = new List<BlendShape>();
        
        for (var i = 0; i < _allKeysArray.Length; i++)
        {
            var shapeName = _allKeysArray[i];
            if (_styleManager.IsStyleShape(shapeName) && IsShapeOverridden(shapeName))
            {
                var weight = GetShapeWeight(shapeName);
                result.Add(new BlendShape(shapeName, weight));
            }
        }
        
        return result;
    }

    public List<BlendShape> GetSelectedCustomShapes()
    {
        var result = new List<BlendShape>();
        
        for (var i = 0; i < _allKeysArray.Length; i++)
        {
            var shapeName = _allKeysArray[i];
            if (!_styleManager.IsStyleShape(shapeName) && IsShapeOverridden(shapeName))
            {
                var weight = GetShapeWeight(shapeName);
                result.Add(new BlendShape(shapeName, weight));
            }
        }
        
        return result;
    }
}

internal class BlendShapePreviewManager
{
    private readonly SkinnedMeshRenderer _renderer;
    private readonly Mesh _mesh;
    private readonly HighlightBlendShapeProcessor _highlightProcessor;
    private readonly PublishedValue<BlendShapeSet> _previewShapes;
    private int _hoveredIndex = -1;
    private bool _previewOnHover = true;

    public BlendShapePreviewManager(SkinnedMeshRenderer renderer, Mesh mesh)
    {
        _renderer = renderer;
        _mesh = mesh;
        _highlightProcessor = new HighlightBlendShapeProcessor(renderer, mesh);
        _previewShapes = new PublishedValue<BlendShapeSet>(new());
        
        EditingShapesPreview.Start(renderer, _previewShapes);
    }

    public bool PreviewOnHover
    {
        get => _previewOnHover;
        set
        {
            if (_previewOnHover == value) return;
            _previewOnHover = value;
            UpdatePreview();
        }
    }

    public void SetHoveredIndex(int index)
    {
        _hoveredIndex = index;
        UpdatePreview();
    }

    public void SetHighlight(int index)
    {
        _highlightProcessor.HilightBlendShapeFor(index);
    }

    public void ClearHighlight()
    {
        _highlightProcessor.ClearHighlight();
    }

    public void UpdatePreview(BlendShapeSet overrideResult = null)
    {
        if (!_previewOnHover)
        {
            _previewShapes.Value = overrideResult ?? new BlendShapeSet();
            return;
        }

        var combined = new BlendShapeSet();
        
        // 現在のオーバーライド結果を追加
        if (overrideResult != null)
        {
            foreach (var shape in overrideResult)
            {
                combined.Add(shape);
            }
        }
        
        // ホバー中のシェイプを追加
        if (_hoveredIndex >= 0 && _hoveredIndex < _mesh.blendShapeCount)
        {
            var shapeName = _mesh.GetBlendShapeName(_hoveredIndex);
            combined.Add(new BlendShape(shapeName, 100f));
        }
        
        _previewShapes.Value = combined;
    }

    public void Dispose()
    {
        EditingShapesPreview.Stop();
        _highlightProcessor?.Dispose();
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

    private StyleShapeManager _styleShapeManager = null!;
    private BlendShapeOverrideManager _overrideManager = null!;
    private BlendShapePreviewManager _previewManager = null!;
    private FacialShapeData _data;
    private FacialShapeUI _ui;
    private Action<BlendShapeSet> _onApply;
    private int _previousUndoGroup = -1;

    private SerializedObject _serializedObject = null!;

    public bool PreviewOnHover
    {
        get => _previewManager?.PreviewOnHover ?? true;
        set
        {
            if (_previewManager != null)
            {
                _previewManager.PreviewOnHover = value;
                OnDataChanged();
            }
        }
    }

    public static FacialShapesEditor OpenEditor(SkinnedMeshRenderer renderer, Mesh mesh, HashSet<string> allKeys, BlendShapeSet defaultOverrides, BlendShapeSet? facialStyleSet = null)
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
        
        _overrideFlags = AllKeysArray.Select(x => defaultOverrides.Contains(x)).ToArray();
        _overrideWeights = AllKeysArray.Select(x =>
        {
            if (defaultOverrides.TryGetValue(x, out var defaultValue))
                return defaultValue.Weight;
            if (FacialStyleSet.TryGetValue(x, out var styleValue))
                return styleValue.Weight;
            return -1f;
        }).ToArray();

        _serializedObject = new SerializedObject(this);
        
        _styleShapeManager = new StyleShapeManager(FacialStyleSet);
        _overrideManager = new BlendShapeOverrideManager(_serializedObject, AllKeysArray, _styleShapeManager);
        _previewManager = new BlendShapePreviewManager(renderer, mesh);
        
        _data = new FacialShapeData(renderer, mesh, allKeys, defaultOverrides, facialStyleSet ?? new(), this);
        
        _ui = new FacialShapeUI(_data, _previewManager, rootVisualElement, this);
        _ui.OnDataChanged += () => {
            hasUnsavedChanges = true;
            OnDataChanged();
        };
        
        saveChangesMessage = "This window may have unsaved changes. Would you like to save?";
        hasUnsavedChanges = false;
        
        _previousUndoGroup = Undo.GetCurrentGroup();
        Undo.IncrementCurrentGroup();

        OnDataChanged();
    }
    
    public BlendShapeSet GetResult()
    {
        return _overrideManager.GetOverrideResult();
    }

    public void RegisterApplyCallback(Action<BlendShapeSet> onApply)
    {
        _onApply = onApply;
    }

    public override void SaveChanges()
    {
        var result = GetResult();
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
        _previewManager?.Dispose();
        CollapseUndoGroup();
    }

    private void CollapseUndoGroup()
    {
        if (Undo.GetCurrentGroup() != _previousUndoGroup)
        {
            Undo.CollapseUndoOperations(_previousUndoGroup);
        }
    }

    private void OnDataChanged()
    {
        var result = GetResult();
        _previewManager?.UpdatePreview(result);
    }

    public void SetShapeWeight(string shapeName, float weight)
    {
        _overrideManager.SetShapeWeight(shapeName, weight);
        OnDataChanged();
    }

    public void AddShape(string shapeName, float weight)
    {
        _overrideManager.AddCustomShape(shapeName, weight);
        OnDataChanged();
    }

    public void RemoveShape(string shapeName)
    {
        _overrideManager.RemoveCustomShape(shapeName);
        OnDataChanged();
    }

    public void ResetStyleShape(string shapeName)
    {
        _overrideManager.ResetStyleShape(shapeName);
        OnDataChanged();
    }

    public void SetHoveredIndex(int index)
    {
        _previewManager?.SetHoveredIndex(index);
    }

    public void SetHighlight(int index)
    {
        _previewManager?.SetHighlight(index);
    }

    public void ClearHighlight()
    {
        _previewManager?.ClearHighlight();
    }

    public bool IsShapeSelected(string shapeName)
    {
        return _overrideManager.IsShapeOverridden(shapeName);
    }

    public float GetShapeWeight(string shapeName)
    {
        return _overrideManager.GetShapeWeight(shapeName);
    }

    public List<BlendShape> GetSelectedStyleShapes()
    {
        return _overrideManager.GetSelectedStyleShapes();
    }

    public List<BlendShape> GetSelectedCustomShapes()
    {
        return _overrideManager.GetSelectedCustomShapes();
    }

    public bool IsStyleShape(string shapeName)
    {
        return _styleShapeManager.IsStyleShape(shapeName);
    }
}

internal class FacialShapeData
{
    public SkinnedMeshRenderer Renderer { get; private set; }
    public Mesh Mesh { get; private set; }
    public HashSet<string> AllKeys { get; private set; }
    public BlendShapeSet DefaultOverrides { get; private set; }
    public BlendShapeSet FacialStyleSet { get; private set; }
    public FacialShapeGrouping Grouping { get; private set; }
    private readonly FacialShapesEditor _editor;

    public bool HighlightOnHover { get; set; } = false;
    public bool ApplyGroupFilterToSelected { get; set; } = false;
    public bool ApplyGroupFilterToUnselected { get; set; } = true;
    public float AddWeight { get; set; } = 100f;
    public AnimationClip SourceClip { get; set; }
    public ClipExcludeOption ClipExcludeOption { get; set; } = ClipExcludeOption.ExcludeZeroWeight;

    public FacialShapeData(SkinnedMeshRenderer renderer, Mesh mesh, HashSet<string> allKeys, BlendShapeSet defaultOverrides, BlendShapeSet facialStyleSet, FacialShapesEditor editor)
    {
        Renderer = renderer;
        Mesh = mesh;
        AllKeys = allKeys;
        DefaultOverrides = defaultOverrides;
        FacialStyleSet = facialStyleSet;
        Grouping = new FacialShapeGrouping(allKeys);
        _editor = editor;
    }

    public List<BlendShape> GetSelectedStyleShapes()
    {
        var allShapes = _editor.GetSelectedStyleShapes();
        return allShapes.Where(shape => ShouldShowSelected(shape.Name)).ToList();
    }

    public List<BlendShape> GetSelectedCustomShapes()
    {
        var allShapes = _editor.GetSelectedCustomShapes();
        return allShapes.Where(shape => ShouldShowSelected(shape.Name)).ToList();
    }

    public List<string> GetUnselectedBlendShapes()
    {
        var result = new List<string>();
        foreach (var key in AllKeys)
        {
            if (!_editor.IsShapeSelected(key) && ShouldShowUnselected(key))
            {
                result.Add(key);
            }
        }
        return result;
    }

    public bool ShouldShowSelected(string shapeName)
    {
        return !ApplyGroupFilterToSelected || Grouping.IsBlendShapeVisible(GetBlendShapeIndex(shapeName));
    }

    public bool ShouldShowUnselected(string shapeName)
    {
        return !ApplyGroupFilterToUnselected || Grouping.IsBlendShapeVisible(GetBlendShapeIndex(shapeName));
    }

    public int GetBlendShapeIndex(string shapeName)
    {
        for (int i = 0; i < Mesh.blendShapeCount; i++)
        {
            if (Mesh.GetBlendShapeName(i) == shapeName)
            {
                return i;
            }
        }
        return -1;
    }

    public void ExtractFromClip()
    {
        if (SourceClip == null) return;

        var bindings = AnimationUtility.GetCurveBindings(SourceClip);

        foreach (var binding in bindings)
        {
            if (binding.propertyName.StartsWith("blendShape."))
            {
                var shapeName = binding.propertyName.Substring("blendShape.".Length);
                if (AllKeys.Contains(shapeName))
                {
                    var curve = AnimationUtility.GetEditorCurve(SourceClip, binding);
                    if (curve.keys.Length > 0)
                    {
                        var value = curve.keys[0].value;
                        if (ClipExcludeOption != ClipExcludeOption.ExcludeZeroWeight || value > 0f)
                        {
                            _editor.AddShape(shapeName, value);
                        }
                    }
                }
            }
        }
    }
}

internal class FacialShapeUI
{
    private readonly FacialShapeData _data;
    private readonly BlendShapePreviewManager _previewManager;
    private readonly VisualElement _root;
    private readonly FacialShapesEditor _editor;

    private VisualElement _selectedContainer;
    private VisualElement _unselectedContainer;
    private VisualElement _groupContainer;
    private VisualElement _facialStyleContainer;
    private readonly Dictionary<string, VisualElement> _selectedElements = new();
    private readonly Dictionary<string, VisualElement> _unselectedElements = new();
    private readonly Dictionary<string, VisualElement> _facialStyleElements = new();
    private readonly Dictionary<string, (Slider slider, Label valueLabel, VisualElement colorBar, Button removeButton)> _selectedElementData = new();
    private readonly Dictionary<string, (Slider slider, Label valueLabel, VisualElement colorBar, Button resetButton)> _facialStyleElementData = new();

    public event Action OnDataChanged;

    public FacialShapeUI(FacialShapeData data, BlendShapePreviewManager previewManager, VisualElement root, FacialShapesEditor editor)
    {
        _data = data;
        _previewManager = previewManager;
        _root = root;
        _editor = editor;
        CreateUI();
    }

    private void CreateUI()
    {
        var mainContainer = new VisualElement
        {
            style = {
                flexDirection = FlexDirection.Row,
                height = Length.Percent(100)
            }
        };
        _root.Add(mainContainer);

        CreateLeftPanel(mainContainer);
        CreateRightPanel(mainContainer);
        RefreshAll();
    }

    private void CreateLeftPanel(VisualElement parent)
    {
        var leftPanel = new VisualElement
        {
            style = {
                flexGrow = 0.6f,
                minWidth = 400,
                maxWidth = 600,
                paddingLeft = 5,
                paddingRight = 5,
                paddingTop = 5,
                paddingBottom = 5
            }
        };
        parent.Add(leftPanel);

        CreateSettingsPanel(leftPanel);
        CreateSelectedPanel(leftPanel);
    }

    private void CreateRightPanel(VisualElement parent)
    {
        var rightPanel = new Box
        {
            style = {
                flexGrow = 1,
                minWidth = 250,
                paddingLeft = 5,
                paddingRight = 5,
                paddingTop = 5,
                paddingBottom = 5
            }
        };
        parent.Add(rightPanel);

        var label = new Label("Unselected Blend Shapes") 
        { 
            style = { unityFontStyleAndWeight = FontStyle.Bold } 
        };
        rightPanel.Add(label);

        var controlRow = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, marginTop = 5, marginBottom = 5 }
        };
        rightPanel.Add(controlRow);

        var batchLabel = new Label("Batch:") { style = { width = 40 } };
        controlRow.Add(batchLabel);

        var addAllButton = new Button(() => AddAllVisibleShapes()) 
        { 
            text = "Add All",
            style = { width = 60 }
        };
        controlRow.Add(addAllButton);

        var weightLabel = new Label("Weight:") { style = { width = 45, marginLeft = 5 } };
        controlRow.Add(weightLabel);

        var weightField = new FloatField 
        { 
            value = _data.AddWeight,
            style = { width = 50 }
        };
        weightField.RegisterValueChangedCallback(evt => _data.AddWeight = Mathf.Clamp(evt.newValue, 0f, 100f));
        controlRow.Add(weightField);

        var unselectedScrollView = new ScrollView(ScrollViewMode.Vertical)
        {
            style = { 
                flexGrow = 1,
                minHeight = 300
            }
        };
        rightPanel.Add(unselectedScrollView);

        _unselectedContainer = unselectedScrollView.contentContainer;
    }

    private void CreateSettingsPanel(VisualElement parent)
    {
        var settingsBox = new Box
        {
            style = { marginBottom = 10, paddingLeft = 5, paddingRight = 5, paddingTop = 5, paddingBottom = 5 }
        };
        parent.Add(settingsBox);

        var previewLabel = new Label("Preview Options") { style = { unityFontStyleAndWeight = FontStyle.Bold } };
        settingsBox.Add(previewLabel);

        var previewToggle = new Toggle("Preview On Hover") { value = _editor.PreviewOnHover };
        previewToggle.RegisterValueChangedCallback(evt => {
            _editor.PreviewOnHover = evt.newValue;
            OnDataChanged?.Invoke();
        });
        settingsBox.Add(previewToggle);

        var highlightToggle = new Toggle("Highlight On Hover") { value = _data.HighlightOnHover };
        highlightToggle.RegisterValueChangedCallback(evt => {
            _data.HighlightOnHover = evt.newValue;
            if (!_data.HighlightOnHover)
            {
                _previewManager.ClearHighlight();
            }
        });
        settingsBox.Add(highlightToggle);

        var groupLabel = new Label("Group Filter") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10 } };
        settingsBox.Add(groupLabel);

        _groupContainer = new VisualElement();
        settingsBox.Add(_groupContainer);
        CreateGroupFilter();

        var clipLabel = new Label("Clip Extraction") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10 } };
        settingsBox.Add(clipLabel);

        var clipField = new ObjectField("Source Clip") { objectType = typeof(AnimationClip) };
        clipField.RegisterValueChangedCallback(evt => _data.SourceClip = evt.newValue as AnimationClip);
        settingsBox.Add(clipField);

        var excludeField = new EnumField("Exclude Option", _data.ClipExcludeOption);
        excludeField.RegisterValueChangedCallback(evt => _data.ClipExcludeOption = (ClipExcludeOption)evt.newValue);
        settingsBox.Add(excludeField);

        var extractButton = new Button(() => {
            _data.ExtractFromClip();
            RefreshAll();
            OnDataChanged?.Invoke();
        }) { text = "Extract from Clip" };
        settingsBox.Add(extractButton);
    }

    private void CreateSelectedPanel(VisualElement parent)
    {
        var selectedBox = new Box
        {
            style = { 
                flexGrow = 1, 
                minHeight = 300,
                paddingLeft = 5, 
                paddingRight = 5, 
                paddingTop = 5, 
                paddingBottom = 5 
            }
        };
        parent.Add(selectedBox);

        var label = new Label("Selected Blend Shapes") 
        { 
            style = { unityFontStyleAndWeight = FontStyle.Bold } 
        };
        selectedBox.Add(label);

        var batchRow = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, marginTop = 5, marginBottom = 5 }
        };
        selectedBox.Add(batchRow);

        var batchLabel = new Label("Batch:") { style = { width = 40 } };
        batchRow.Add(batchLabel);

        var all0Button = new Button(() => SetAllSelectedWeights(0f)) 
        { 
            text = "All 0",
            style = { width = 50 }
        };
        batchRow.Add(all0Button);

        var all100Button = new Button(() => SetAllSelectedWeights(100f)) 
        { 
            text = "All 100",
            style = { width = 60, marginLeft = 2 }
        };
        batchRow.Add(all100Button);

        var removeAllButton = new Button(() => RemoveAllCustomShapes()) 
        { 
            text = "Remove All",
            style = { width = 80, marginLeft = 2 }
        };
        batchRow.Add(removeAllButton);

        // Facial Style Shapes Section
        var facialStyleLabel = new Label("Facial Style Shapes") 
        { 
            style = { 
                unityFontStyleAndWeight = FontStyle.Bold,
                marginTop = 10,
                marginBottom = 5
            } 
        };
        selectedBox.Add(facialStyleLabel);

        var facialStyleScrollView = new ScrollView(ScrollViewMode.Vertical)
        {
            style = { 
                flexGrow = 0,
                minHeight = 100,
                maxHeight = 200,
                marginBottom = 10
            }
        };
        selectedBox.Add(facialStyleScrollView);

        _facialStyleContainer = facialStyleScrollView.contentContainer;

        // Custom Selected Shapes Section
        var customLabel = new Label("Custom Selected Shapes") 
        { 
            style = { 
                unityFontStyleAndWeight = FontStyle.Bold,
                marginBottom = 5
            } 
        };
        selectedBox.Add(customLabel);

        var selectedScrollView = new ScrollView(ScrollViewMode.Vertical)
        {
            style = { 
                flexGrow = 1,
                minHeight = 200
            }
        };
        selectedBox.Add(selectedScrollView);

        _selectedContainer = selectedScrollView.contentContainer;
    }

    private void CreateGroupFilter()
    {
        _groupContainer.Clear();

        var groupsPerRow = 2;
        VisualElement currentRow = null;

        for (int i = 0; i < _data.Grouping.Groups.Count; i++)
        {
            if (i % groupsPerRow == 0)
            {
                currentRow = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row }
                };
                _groupContainer.Add(currentRow);
            }

            var group = _data.Grouping.Groups[i];
            var displayName = $"{group.Name}({group.BlendShapeIndices.Count})";
            var toggle = new Toggle(displayName) 
            { 
                value = group.IsSelected,
                style = { 
                    flexGrow = 1,
                    minWidth = 120,
                    maxWidth = 200
                }
            };

            var groupRef = group;
            toggle.RegisterValueChangedCallback(evt => {
                groupRef.IsSelected = evt.newValue;
                RefreshAll();
            });

            currentRow.Add(toggle);
        }

        var buttonRow = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, marginTop = 5 }
        };
        _groupContainer.Add(buttonRow);

        var allButton = new Button(() => SetAllGroups(true)) 
        { 
            text = "All",
            style = { width = 60 }
        };
        buttonRow.Add(allButton);

        var noneButton = new Button(() => SetAllGroups(false)) 
        { 
            text = "None",
            style = { width = 60, marginLeft = 5 }
        };
        buttonRow.Add(noneButton);

        var filterRow = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, marginTop = 5 }
        };
        _groupContainer.Add(filterRow);

        var selectedFilterToggle = new Toggle("Selected") 
        { 
            value = _data.ApplyGroupFilterToSelected,
            style = { 
                flexGrow = 1,
                minWidth = 80,
                maxWidth = 150
            }
        };
        selectedFilterToggle.RegisterValueChangedCallback(evt => {
            _data.ApplyGroupFilterToSelected = evt.newValue;
            if (_data.ApplyGroupFilterToSelected)
            {
                RefreshSelectedList();
            }
        });
        filterRow.Add(selectedFilterToggle);

        var unselectedFilterToggle = new Toggle("Unselected") 
        { 
            value = _data.ApplyGroupFilterToUnselected,
            style = { 
                flexGrow = 1,
                minWidth = 80,
                maxWidth = 150
            }
        };
        unselectedFilterToggle.RegisterValueChangedCallback(evt => {
            _data.ApplyGroupFilterToUnselected = evt.newValue;
            if (_data.ApplyGroupFilterToUnselected)
            {
                RefreshUnselectedList();
            }
        });
        filterRow.Add(unselectedFilterToggle);
    }

    private void RefreshAll()
    {
        RefreshSelectedList();
        RefreshUnselectedList();
    }

    private void RefreshSelectedList()
    {
        _selectedContainer.Clear();
        _selectedElements.Clear();
        _selectedElementData.Clear();
        
        _facialStyleContainer.Clear();
        _facialStyleElements.Clear();
        _facialStyleElementData.Clear();

        var styleShapes = _data.GetSelectedStyleShapes();
        var customShapes = _data.GetSelectedCustomShapes();
        
        foreach (var shape in styleShapes)
        {
            CreateFacialStyleShapeElement(shape);
        }
        
        foreach (var shape in customShapes)
        {
            CreateSelectedShapeElement(shape);
        }
    }

    private void RefreshUnselectedList()
    {
        _unselectedContainer.Clear();
        _unselectedElements.Clear();

        var unselectedShapes = _data.GetUnselectedBlendShapes();
        foreach (var shapeName in unselectedShapes)
        {
            CreateUnselectedShapeElement(shapeName);
        }
    }

    private void CreateSelectedShapeElement(BlendShape shape)
    {
        var container = new VisualElement
        {
            style = {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                paddingLeft = 5,
                paddingTop = 2,
                paddingBottom = 2,
                marginBottom = 1
            }
        };

        var nameLabel = new Label(shape.Name)
        {
            style = { 
                width = 120,
                minWidth = 120,
                maxWidth = 180
            }
        };
        container.Add(nameLabel);

        var slider = new Slider(0f, 100f)
        {
            value = shape.Weight,
            style = { 
                width = 150,
                minWidth = 100,
                maxWidth = 200
            }
        };
        slider.RegisterValueChangedCallback(evt => SetSelectedShapeWeight(shape.Name, evt.newValue));
        container.Add(slider);

        var valueLabel = new Label(shape.Weight.ToString("F1"))
        {
            style = { width = 30 }
        };
        container.Add(valueLabel);

        var colorBar = new VisualElement
        {
            style = {
                width = 3,
                height = 20,
                marginLeft = 2
            }
        };
        UpdateSelectedShapeColorBar(shape.Name, shape.Weight, colorBar);
        container.Add(colorBar);

        var removeButton = new Button(() => RemoveSelectedShape(shape.Name))
        {
            text = "X",
            style = { width = 20, height = 20, marginLeft = 2 }
        };
        container.Add(removeButton);

        container.RegisterCallback<MouseOverEvent>(evt => {
            var index = _data.GetBlendShapeIndex(shape.Name);
            _editor.SetHoveredIndex(index);
            if (_data.HighlightOnHover)
            {
                _previewManager.SetHighlight(index);
            }
            OnDataChanged?.Invoke();
        });

        container.RegisterCallback<MouseOutEvent>(evt => {
            _editor.SetHoveredIndex(-1);
            if (_data.HighlightOnHover)
            {
                _previewManager.ClearHighlight();
            }
            OnDataChanged?.Invoke();
        });

        _selectedContainer.Add(container);
        _selectedElements[shape.Name] = container;
        _selectedElementData[shape.Name] = (slider, valueLabel, colorBar, removeButton);
    }

    private void CreateFacialStyleShapeElement(BlendShape shape)
    {
        var container = new VisualElement
        {
            style = {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                paddingLeft = 5,
                paddingTop = 2,
                paddingBottom = 2,
                marginBottom = 1,
                backgroundColor = new Color(1f, 1f, 0.8f, 0.1f) // Light yellow background
            }
        };

        var nameLabel = new Label(shape.Name)
        {
            style = { 
                width = 120,
                minWidth = 120,
                maxWidth = 180
            }
        };
        container.Add(nameLabel);

        var slider = new Slider(0f, 100f)
        {
            value = shape.Weight,
            style = { 
                width = 150,
                minWidth = 100,
                maxWidth = 200
            }
        };
        slider.RegisterValueChangedCallback(evt => SetFacialStyleShapeWeight(shape.Name, evt.newValue));
        container.Add(slider);

        var valueLabel = new Label(shape.Weight.ToString("F1"))
        {
            style = { width = 30 }
        };
        container.Add(valueLabel);

        var colorBar = new VisualElement
        {
            style = {
                width = 3,
                height = 20,
                marginLeft = 2,
                backgroundColor = Color.yellow
            }
        };
        UpdateFacialStyleShapeColorBar(shape.Name, shape.Weight, colorBar);
        container.Add(colorBar);

        var resetButton = new Button(() => {
            _editor.ResetStyleShape(shape.Name);
            RefreshSelectedList();
        })
        {
            text = "R",
            style = { width = 20, height = 20, marginLeft = 2 }
        };
        container.Add(resetButton);

        container.RegisterCallback<MouseOverEvent>(evt => {
            var index = _data.GetBlendShapeIndex(shape.Name);
            _editor.SetHoveredIndex(index);
            if (_data.HighlightOnHover)
            {
                _previewManager.SetHighlight(index);
            }
            OnDataChanged?.Invoke();
        });

        container.RegisterCallback<MouseOutEvent>(evt => {
            _editor.SetHoveredIndex(-1);
            if (_data.HighlightOnHover)
            {
                _previewManager.ClearHighlight();
            }
            OnDataChanged?.Invoke();
        });

        _facialStyleContainer.Add(container);
        _facialStyleElements[shape.Name] = container;
        _facialStyleElementData[shape.Name] = (slider, valueLabel, colorBar, resetButton);
    }

    private void CreateUnselectedShapeElement(string shapeName)
    {
        var container = new VisualElement
        {
            style = {
                paddingLeft = 8,
                paddingTop = 2,
                paddingBottom = 2,
                marginBottom = 1
            }
        };

        var label = new Label(shapeName)
        {
            style = { 
                flexGrow = 1,
                minWidth = 150
            }
        };
        container.Add(label);

        container.RegisterCallback<MouseOverEvent>(evt => {
            var index = _data.GetBlendShapeIndex(shapeName);
            _editor.SetHoveredIndex(index);
            if (_data.HighlightOnHover)
            {
                _previewManager.SetHighlight(index);
            }
            OnDataChanged?.Invoke();
        });

        container.RegisterCallback<MouseOutEvent>(evt => {
            _editor.SetHoveredIndex(-1);
            if (_data.HighlightOnHover)
            {
                _previewManager.ClearHighlight();
            }
            OnDataChanged?.Invoke();
        });

        container.RegisterCallback<ClickEvent>(evt => AddSelectedShape(shapeName, _data.AddWeight));

        _unselectedContainer.Add(container);
        _unselectedElements[shapeName] = container;
    }

    private void SetSelectedShapeWeight(string shapeName, float weight)
    {
        _editor.SetShapeWeight(shapeName, weight);
        
        if (_selectedElementData.TryGetValue(shapeName, out var elementData))
        {
            elementData.valueLabel.text = weight.ToString("F0");
            UpdateSelectedShapeColorBar(shapeName, weight, elementData.colorBar);
        }
        OnDataChanged?.Invoke();
    }

    private void UpdateSelectedShapeColorBar(string shapeName, float weight, VisualElement colorBar)
    {
        var color = _data.FacialStyleSet.Contains(shapeName) ? Color.yellow : Color.white;
        
        var intensity = Mathf.Clamp01(weight / 100f);
        color.a = 0.3f + intensity * 0.7f;
        colorBar.style.backgroundColor = color;
    }

    private void SetFacialStyleShapeWeight(string shapeName, float weight)
    {
        _editor.SetShapeWeight(shapeName, weight);
        
        if (_facialStyleElementData.TryGetValue(shapeName, out var elementData))
        {
            elementData.valueLabel.text = weight.ToString("F0");
            UpdateFacialStyleShapeColorBar(shapeName, weight, elementData.colorBar);
        }
        OnDataChanged?.Invoke();
    }

    private void UpdateFacialStyleShapeColorBar(string shapeName, float weight, VisualElement colorBar)
    {
        var color = Color.yellow;
        var intensity = Mathf.Clamp01(weight / 100f);
        color.a = 0.3f + intensity * 0.7f;
        colorBar.style.backgroundColor = color;
    }

    private void RemoveSelectedShape(string shapeName)
    {
        var styleShapeManager = new StyleShapeManager(_data.FacialStyleSet);
        
        // Facial styleシェイプは削除できない
        if (styleShapeManager.IsStyleShape(shapeName))
        {
            return;
        }
        
        _editor.RemoveShape(shapeName);
        
        if (_selectedElements.TryGetValue(shapeName, out var element))
        {
            _selectedContainer.Remove(element);
            _selectedElements.Remove(shapeName);
            _selectedElementData.Remove(shapeName);
        }
        
        if (!_unselectedElements.ContainsKey(shapeName) && _data.ShouldShowUnselected(shapeName))
        {
            CreateUnselectedShapeElement(shapeName);
        }
        OnDataChanged?.Invoke();
    }

    private void AddSelectedShape(string shapeName, float weight)
    {
        _editor.AddShape(shapeName, weight);
        
        if (_unselectedElements.TryGetValue(shapeName, out var element))
        {
            _unselectedContainer.Remove(element);
            _unselectedElements.Remove(shapeName);
        }
        
        var styleShapeManager = new StyleShapeManager(_data.FacialStyleSet);
        
        if (styleShapeManager.IsStyleShape(shapeName))
        {
            if (!_facialStyleElements.ContainsKey(shapeName))
            {
                CreateFacialStyleShapeElement(new BlendShape(shapeName, weight));
            }
        }
        else
        {
            if (!_selectedElements.ContainsKey(shapeName))
            {
                CreateSelectedShapeElement(new BlendShape(shapeName, weight));
            }
        }
        OnDataChanged?.Invoke();
    }

    private void SetAllGroups(bool selected)
    {
        foreach (var group in _data.Grouping.Groups)
        {
            group.IsSelected = selected;
        }
        CreateGroupFilter();
        RefreshSelectedList();
        RefreshUnselectedList();
    }

    private void SetAllSelectedWeights(float weight)
    {
        // Update facial style shapes
        foreach (var shape in _data.GetSelectedStyleShapes())
        {
            _editor.SetShapeWeight(shape.Name, weight);
            if (_facialStyleElementData.TryGetValue(shape.Name, out var facialStyleElementData))
            {
                facialStyleElementData.slider.value = weight;
                facialStyleElementData.valueLabel.text = weight.ToString("F0");
                UpdateFacialStyleShapeColorBar(shape.Name, weight, facialStyleElementData.colorBar);
            }
        }
        
        // Update custom shapes
        foreach (var shape in _data.GetSelectedCustomShapes())
        {
            _editor.SetShapeWeight(shape.Name, weight);
            if (_selectedElementData.TryGetValue(shape.Name, out var elementData))
            {
                elementData.slider.value = weight;
                elementData.valueLabel.text = weight.ToString("F0");
                UpdateSelectedShapeColorBar(shape.Name, weight, elementData.colorBar);
            }
        }
        OnDataChanged?.Invoke();
    }

    private void RemoveAllCustomShapes()
    {
        var shapesToRemove = _data.GetSelectedCustomShapes().Select(s => s.Name).ToList();
        
        foreach (var shapeName in shapesToRemove)
        {
            _editor.RemoveShape(shapeName);
            if (_selectedElements.TryGetValue(shapeName, out var element))
            {
                _selectedContainer.Remove(element);
                _selectedElements.Remove(shapeName);
                _selectedElementData.Remove(shapeName);
            }
            
            if (!_unselectedElements.ContainsKey(shapeName) && _data.ShouldShowUnselected(shapeName))
            {
                CreateUnselectedShapeElement(shapeName);
            }
        }
        OnDataChanged?.Invoke();
    }

    private void AddAllVisibleShapes()
    {
        foreach (var shapeName in _data.GetUnselectedBlendShapes())
        {
            _editor.AddShape(shapeName, _data.AddWeight);
            if (_unselectedElements.TryGetValue(shapeName, out var element))
            {
                _unselectedContainer.Remove(element);
                _unselectedElements.Remove(shapeName);
            }
            
            if (!_selectedElements.ContainsKey(shapeName))
            {
                CreateSelectedShapeElement(new BlendShape(shapeName, _data.AddWeight));
            }
        }
        OnDataChanged?.Invoke();
    }
}

internal class FacialShapeGrouping
{
    private const string DefaultGroupName = "Default";
    
    private static readonly string GroupNameSymbolPattern = string.Join("|", new[]
    {
        @"\W",
        @"\p{Pc}",
        @"ー",
        @"ｰ",
    });
    
    private static readonly string GroupNamePattern = string.Join("|", new[]
    {
        $"^(?:(?:{GroupNameSymbolPattern}){{3,}})(.*?)(?:(?:{GroupNameSymbolPattern}){{3,}})?$",
        $"^(?:(?:{GroupNameSymbolPattern}){{3,}})?(.*?)(?:(?:{GroupNameSymbolPattern}){{3,}})$",
    });

    public readonly List<FacialShapeGroup> Groups;

    public FacialShapeGrouping(HashSet<string> allKeys)
    {
        Groups = new List<FacialShapeGroup>();
        BuildGroups(allKeys);
    }

    private void BuildGroups(HashSet<string> allKeys)
    {
        Groups.Add(new FacialShapeGroup(DefaultGroupName));

        var index = 0;
        foreach (var key in allKeys)
        {
            var match = Regex.Match(key, GroupNamePattern);
            if (match.Success)
            {
                var groupName = match.Groups.Cast<System.Text.RegularExpressions.Group>().Skip(1).First(x => x.Success).Value;
                if (!string.IsNullOrEmpty(groupName) && !Groups.Any(g => g.Name == groupName))
                {
                    Groups.Add(new FacialShapeGroup(groupName));
                }
            }

            var targetGroup = Groups.Last();
            targetGroup.BlendShapeIndices.Add(index);
            index++;
        }

        Groups.Sort((a, b) => {
            if (a.Name == DefaultGroupName) return 1;
            if (b.Name == DefaultGroupName) return -1;
            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });
    }

    public bool IsBlendShapeVisible(int index)
    {
        foreach (var group in Groups)
        {
            if (group.BlendShapeIndices.Contains(index))
            {
                return group.IsSelected;
            }
        }
        return false;
    }
}

internal class FacialShapeGroup
{
    public readonly string Name;
    public readonly List<int> BlendShapeIndices;
    public bool IsSelected { get; set; } = true;

    public FacialShapeGroup(string name)
    {
        Name = name;
        BlendShapeIndices = new List<int>();
    }
}
}