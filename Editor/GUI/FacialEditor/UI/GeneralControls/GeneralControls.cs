using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal class GeneralControls : IDisposable
{
    private readonly TargetManager _targetManager;
    private readonly BlendShapeOverrideManager _blendShapeManager;
    private readonly BlendShapeGrouping _groupManager;
    private readonly PreviewManager _previewManager;

    private static VisualTreeAsset? _uxml;
    private static StyleSheet? _uss;
    private readonly VisualElement _element;
    public VisualElement Element => _element;

    private VisualElement _targetingOptionsContainer = null!;
    private VisualElement _groupTogglesContainer = null!;
    private readonly List<Toggle> _groupToggles = new();

    private VisualElement _targetingContent = null!;
    private VisualElement _clipContent = null!;
    private VisualElement _filterContent = null!;
    private readonly List<VisualElement> _contentElements = new();
    private readonly LocalizedToolbar _toolbar;
    private int _selectedToolbarIndex = 0;
    private readonly int _initialUndoGroup;

    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _restoreInitialOverridesButton = null!;
    private Button _restoreEditedOverridesButton = null!;

    private AnimationClip? _clip;
    private ClipImportOption _clipImportOption = ClipImportOption.NonZero;

    private static readonly Texture _selectAllIcon = EditorGUIUtility.IconContent("d_Toolbar Plus").image;
    private static readonly Texture _selectNoneIcon = EditorGUIUtility.IconContent("d_Toolbar Minus").image;
    private static readonly Texture _undoIcon = EditorGUIUtility.IconContent("d_StepLeftButton").image;
    private static readonly Texture _redoIcon = EditorGUIUtility.IconContent("d_StepButton").image;
    private static readonly Texture _restoreInitialOverridesIcon = EditorGUIUtility.IconContent("Animation.FirstKey@2x").image;
    private static readonly Texture _restoreEditedOverridesIcon = EditorGUIUtility.IconContent("Animation.LastKey@2x").image;

    public GeneralControls(TargetManager targetManager, BlendShapeOverrideManager blendShapeManager, BlendShapeGrouping groupManager, PreviewManager previewManager, int initialUndoGroup)
    {
        _targetManager = targetManager;
        _blendShapeManager = blendShapeManager;
        _groupManager = groupManager;
        _previewManager = previewManager;
        _initialUndoGroup = initialUndoGroup;

        var uxml = UIAssetHelper.EnsureUxmlWithGuid(ref _uxml, "41adb90607cdad24292515795aeb1680");
        var uss = UIAssetHelper.EnsureUssWithGuid(ref _uss, "d76d3f47e63003541b2f77817315d701");

        _element = uxml.CloneTree();
        _element.styleSheets.Add(uss);
        Localization.LocalizeUIElements(_element);

        _toolbar = new LocalizedToolbar(new[] { "FacialEditor:label:filter", "FacialEditor:label:ClipImport" });

        Undo.undoRedoPerformed += UpdateUndoRedoState;

        SetupControls();
    }

    private void UpdateUndoRedoState()
    {
        if (_undoButton == null || _redoButton == null) return;
        
        string undoName = Undo.GetCurrentGroupName();
        bool canUndo = !string.IsNullOrEmpty(undoName) && undoName != "Facial Shapes Editor: Window Opened";
        _undoButton.SetEnabled(canUndo);
        _redoButton.SetEnabled(true);
    }

    private static Dictionary<Type, Func<IShapesEditorTargeting>> _targetingTypes = new()
    {
        { typeof(AnimationClip), () => new AnimationClipTargeting() },
        { typeof(ExpressionDataComponent), () => new ExpressionDataTargeting() },
        { typeof(FacialStyleComponent), () => new FacialStyleTargeting() },
        { typeof(AdvancedEyeBlinkComponent), () => new AdvancedEyeBlinkTargeting() },
        { typeof(AdvancedLipSyncComponent), () => new AdvancedLipSyncTargeting() },
    };
    private static Dictionary<string, Type> _targetingTypeNames = _targetingTypes.ToDictionary(x => x.Key.Name, x => x.Key);

    private void SetupControls()
    {
        // Renderer Field (Always visible)
        var targetRendererField = _element.Q<ObjectField>("target-renderer-field");
        targetRendererField.objectType = typeof(SkinnedMeshRenderer);
        targetRendererField.RegisterValueChangedCallback(evt =>
        {
            _targetManager.TrySetTargetRenderer(evt.newValue as SkinnedMeshRenderer);
        });
        _targetManager.OnTargetRendererChanged += (renderer) =>
        {
            targetRendererField.SetValueWithoutNotify(renderer);
        };

        // Top Actions (Save, Undo, Redo)
        var saveButton = _element.Q<Button>("save-button");
        saveButton.clicked += () => _targetManager.Save();
        saveButton.SetEnabled(_targetManager.CanSave);
        _targetManager.OnCanSaveChanged += (canSave) => saveButton.SetEnabled(canSave);

        _undoButton = _element.Q<Button>("undo-button");
        _undoButton.Add(new Image { image = _undoIcon, scaleMode = ScaleMode.ScaleToFit });
        _undoButton.clicked += () => Undo.PerformUndo();

        _redoButton = _element.Q<Button>("redo-button");
        _redoButton.Add(new Image { image = _redoIcon, scaleMode = ScaleMode.ScaleToFit });
        _redoButton.clicked += () => Undo.PerformRedo();

        _restoreInitialOverridesButton = _element.Q<Button>("restore-initial-overrides-button");
        _restoreInitialOverridesButton.Add(new Image { image = _restoreInitialOverridesIcon, scaleMode = ScaleMode.ScaleToFit });
        _restoreInitialOverridesButton.clicked += () =>
        {
            _blendShapeManager.TryRestoreInitialOverrides();
            UpdateOverrideRestoreButtonsState();
        };

        _restoreEditedOverridesButton = _element.Q<Button>("restore-edited-overrides-button");
        _restoreEditedOverridesButton.Add(new Image { image = _restoreEditedOverridesIcon, scaleMode = ScaleMode.ScaleToFit });
        _restoreEditedOverridesButton.clicked += () =>
        {
            _blendShapeManager.TryRestoreEditedOverrides();
            UpdateOverrideRestoreButtonsState();
        };

        UpdateUndoRedoState();
        UpdateOverrideRestoreButtonsState();

        // Toolbar logic (IMGUI Container)
        _targetingContent = _element.Q<VisualElement>("targeting-content");
        _clipContent = _element.Q<VisualElement>("clip-content");
        _filterContent = _element.Q<VisualElement>("filter-content");
        _contentElements.AddRange(new[] { _filterContent, _clipContent });

        var toolbarContainer = _element.Q<IMGUIContainer>("toolbar-container");
        toolbarContainer.onGUIHandler = () =>
        {
            UpdateUndoRedoState();
            UpdateOverrideRestoreButtonsState();
            var newIndex = _toolbar.Draw(_selectedToolbarIndex);
            if (newIndex != _selectedToolbarIndex)
            {
                _selectedToolbarIndex = newIndex;
                UpdateTabVisibility();
            }
        };

        // Default selection
        UpdateTabVisibility();

        // --- Targeting Content ---
        var targetingField = _targetingContent.Q<ObjectField>("targeting-object-field");
        targetingField.RegisterValueChangedCallback(evt =>
        {
            _targetManager.Targeting?.SetTarget(evt.newValue);
        });
        _targetManager.OnTargetingChanged += (targeting) =>
        {
            targetingField.SetValueWithoutNotify(targeting?.GetTarget());
        };

        var targetingTypeField = _targetingContent.Q<DropdownField>("targeting-type-field");
        targetingTypeField.choices = _targetingTypeNames.Keys.ToList();
        targetingTypeField.RegisterValueChangedCallback(evt =>
        {
            var targeting = _targetingTypes[_targetingTypeNames[evt.newValue]]();
            _targetManager.SetTargeting(targeting);
            targetingField.objectType = targeting.GetObjectType();
        });
        _targetManager.OnTargetingChanged += (targeting) =>
        {
            if (targeting != null)
            {
                var objectType = targeting.GetObjectType();
                targetingTypeField.SetValueWithoutNotify(objectType.Name);
                targetingField.objectType = objectType;
            }
            else
            {
                targetingTypeField.SetValueWithoutNotify(null);
                targetingField.objectType = null;
            }
        };

        _targetingOptionsContainer = _targetingContent.Q<VisualElement>("targeting-options-container");
        RefreshTargetingContainer();
        _targetManager.OnTargetingChanged += (targeting) =>
        {
            RefreshTargetingContainer();
        };

        // --- Clip Import Content ---
        var clipField = _clipContent.Q<ObjectField>("clip-field");
        clipField.objectType = typeof(AnimationClip);
        clipField.RegisterValueChangedCallback(evt =>
        {
            _clip = evt.newValue as AnimationClip;
        });

        var clipImportOptionField = _clipContent.Q<EnumField>("import-option-field");
        clipImportOptionField.Init(_clipImportOption);
        clipImportOptionField.RegisterValueChangedCallback(evt =>
        {
            _clipImportOption = (ClipImportOption)evt.newValue;
        });

        var importClipButton = _clipContent.Q<Button>("import-clip-button");
        importClipButton.clicked += () =>
        {
            if (_clip == null) return;
            var resutlt = new BlendShapeWeightSet();
            _clip.GetFirstFrameBlendShapes(resutlt, _clipImportOption, _blendShapeManager.BaseSet.ToBlendShapeAnimations().ToList());
            _blendShapeManager.OverrideShapesAndSetWeight(resutlt.Select(x => (_blendShapeManager.GetIndexForShape(x.Name), x.Weight)));
        };

        // --- Filter Content ---
        _groupTogglesContainer = _filterContent.Q<VisualElement>("group-toggles-container");
        RefreshGroupToggles();
        _groupManager.OnGroupSelectionChanged += (groups) => RefreshGroupToggles();

        var allButton = _filterContent.Q<Button>("all-button");
        allButton.Add(new Image { image = _selectAllIcon });
        allButton.clicked += () =>
        {
            for (int i = 0; i < _groupManager.Groups.Count; i++)
            {
                _groupToggles[i].SetValueWithoutNotify(true);
            }
            _groupManager.SelectAll(true);
        };
        
        var noneButton = _filterContent.Q<Button>("none-button");
        noneButton.Add(new Image { image = _selectNoneIcon });
        noneButton.clicked += () =>
        {
            for (int i = 0; i < _groupManager.Groups.Count; i++)
            {
                _groupToggles[i].SetValueWithoutNotify(false);
            }
            _groupManager.SelectAll(false);
        };
        
        var leftToggle = _filterContent.Q<Toggle>("left-toggle");
        leftToggle.SetValueWithoutNotify(_groupManager.IsLeftSelected);
        leftToggle.RegisterValueChangedCallback(evt =>
        {
            _groupManager.IsLeftSelected = evt.newValue;
        });
        
        var rightToggle = _filterContent.Q<Toggle>("right-toggle");
        rightToggle.SetValueWithoutNotify(_groupManager.IsRightSelected);
        rightToggle.RegisterValueChangedCallback(evt =>
        {
            _groupManager.IsRightSelected = evt.newValue;
        });
    }

    private void UpdateTabVisibility()
    {
        for (int i = 0; i < _contentElements.Count; i++)
        {
            if (i == _selectedToolbarIndex)
            {
                _contentElements[i].AddToClassList("toolbar-content--visible");
            }
            else
            {
                _contentElements[i].RemoveFromClassList("toolbar-content--visible");
            }
        }
    }

    private void UpdateOverrideRestoreButtonsState()
    {
        if (_restoreInitialOverridesButton == null || _restoreEditedOverridesButton == null) return;

        var hasTarget = _targetManager.TargetRenderer != null;
        _restoreInitialOverridesButton.SetEnabled(hasTarget);
        _restoreEditedOverridesButton.SetEnabled(hasTarget && _blendShapeManager.CanRestoreEditedOverrides);
    }

    public void Dispose()
    {
        _toolbar.Dispose();
        Undo.undoRedoPerformed -= UpdateUndoRedoState;
    }

    private void RefreshTargetingContainer()
    {
        _targetingOptionsContainer.Clear();
        var targeting = _targetManager.Targeting;
        if (targeting != null)
        {
            _targetingOptionsContainer.Add(targeting.DrawOptions());
        }
    }

    public void RefreshTarget()
    {
        RefreshGroupToggles();
    }

    private void RefreshGroupToggles()
    {
        _groupTogglesContainer.Clear();
        _groupToggles.Clear();
        foreach (var group in _groupManager.Groups)
        {
            var toggle = new Toggle(group.Name) { value = group.IsSelected };
            toggle.AddToClassList("group-toggle");
            toggle.RegisterValueChangedCallback(evt =>
            {
                group.IsSelected = evt.newValue;
            });
            _groupTogglesContainer.Add(toggle);
            _groupToggles.Add(toggle);
        }
    }
}
