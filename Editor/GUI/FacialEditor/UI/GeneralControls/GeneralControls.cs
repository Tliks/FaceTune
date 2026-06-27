using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Aoyon.FaceTune.Gui.Components;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal class GeneralControls : IDisposable
{
    private readonly FacialShapesEditorContext _context;
    private readonly Func<SkinnedMeshRenderer?, bool> _tryChangeRenderer;
    private readonly Action _save;
    private readonly BlendShapeOverrideManager _blendShapeManager;
    private readonly BlendShapeGrouping _groupManager;

    private static VisualTreeAsset? _uxml;
    private static StyleSheet? _uss;
    private readonly VisualElement _element;
    public VisualElement Element => _element;

    private VisualElement _groupTogglesContainer = null!;
    private readonly List<SimpleToggle> _groupToggles = new();
    private const float GroupToggleHorizontalPadding = 8f;

    private VisualElement _filterContent = null!;

    private Button _saveButton = null!;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _restoreInitialOverridesButton = null!;
    private Button _restoreEditedOverridesButton = null!;
    private readonly EditorApplication.CallbackFunction _undoStateUpdateCallback;

    private ClipImportOption _clipImportOption = ClipImportOption.NonZero;

    private static readonly Texture _selectAllIcon = EditorGUIUtility.IconContent("d_Toolbar Plus").image;
    private static readonly Texture _selectNoneIcon = EditorGUIUtility.IconContent("d_Toolbar Minus").image;
    private static readonly Texture _undoIcon = EditorGUIUtility.IconContent("Animation.PrevKey@2x").image;
    private static readonly Texture _redoIcon = EditorGUIUtility.IconContent("Animation.NextKey@2x").image;
    private static readonly Texture _restoreInitialOverridesIcon = EditorGUIUtility.IconContent("Animation.FirstKey@2x").image;
    private static readonly Texture _restoreEditedOverridesIcon = EditorGUIUtility.IconContent("Animation.LastKey@2x").image;

    public GeneralControls(
        FacialShapesEditorContext context,
        Func<SkinnedMeshRenderer?, bool> tryChangeRenderer,
        Action save)
    {
        _context = context;
        _tryChangeRenderer = tryChangeRenderer;
        _save = save;
        _blendShapeManager = context.DataManager;
        _groupManager = context.GroupManager;
        _undoStateUpdateCallback = UpdateUndoRedoState;

        var uxml = UIAssetHelper.EnsureUxmlWithGuid(ref _uxml, "41adb90607cdad24292515795aeb1680");
        var uss = UIAssetHelper.EnsureUssWithGuid(ref _uss, "d76d3f47e63003541b2f77817315d701");

        _element = uxml.CloneTree();
        _element.styleSheets.Add(uss);
        Localization.LocalizeUIElements(_element);

        Undo.undoRedoPerformed += QueueUndoStateUpdate;

        SetupControls();
    }

    private void QueueUndoStateUpdate()
    {
        EditorApplication.delayCall -= _undoStateUpdateCallback;
        EditorApplication.delayCall += _undoStateUpdateCallback;
    }

    private void UpdateUndoRedoState()
    {
        if (_undoButton == null || _redoButton == null) return;

        _undoButton.SetEnabled(CanUndoForThisWindow());
        _redoButton.SetEnabled(CanRedoPolicy());
    }

    private static bool CanUndoForThisWindow()
    {
        if (UndoUtility.TryHasUndo(out var canUndoFromUndo))
        {
            if (!canUndoFromUndo) return false;
            return Undo.GetCurrentGroupName() != "Facial Shapes Editor: Window Opened";
        }

        var fallbackName = Undo.GetCurrentGroupName();
        return !string.IsNullOrEmpty(fallbackName) && fallbackName != "Facial Shapes Editor: Window Opened";
    }

    private static bool CanRedoPolicy()
    {
        if (UndoUtility.TryHasRedo(out var canRedoFromUndo))
        {
            return canRedoFromUndo;
        }

        return true;
    }

    private void SetupControls()
    {
        var targetRendererField = _element.Q<ObjectField>("target-renderer-field");
        targetRendererField.objectType = typeof(SkinnedMeshRenderer);
        targetRendererField.SetValueWithoutNotify(_context.Renderer);
        targetRendererField.SetEnabled(_context.CanChangeRenderer);
        targetRendererField.RegisterValueChangedCallback(evt =>
        {
            if (!_tryChangeRenderer(evt.newValue as SkinnedMeshRenderer))
            {
                targetRendererField.SetValueWithoutNotify(_context.Renderer);
            }
        });

        var targetingField = _element.Q<ObjectField>("targeting-object-field");
        targetingField.objectType = _context.Targeting.GetObjectType();
        targetingField.SetValueWithoutNotify(_context.Targeting.GetTarget());
        targetingField.SetEnabled(_context.Targeting is AnimationClipTargeting);
        targetingField.RegisterValueChangedCallback(evt =>
        {
            if (_context.Targeting is not AnimationClipTargeting) return;
            _context.Targeting.SetTarget(evt.newValue);
            UpdateActionButtonStates();
        });

        var targetingOptionsContainer = _element.Q<VisualElement>("targeting-options-container");
        if (_context.Targeting.DrawOptions() is { } options)
        {
            targetingOptionsContainer.Add(options);
        }

        _saveButton = _element.Q<Button>("save-button");
        _saveButton.clicked += _save;

        _undoButton = _element.Q<Button>("undo-button");
        _undoButton.Add(new Image { image = _undoIcon });
        _undoButton.clicked += () => Undo.PerformUndo();

        _redoButton = _element.Q<Button>("redo-button");
        _redoButton.Add(new Image { image = _redoIcon });
        _redoButton.clicked += () => Undo.PerformRedo();

        _restoreInitialOverridesButton = _element.Q<Button>("restore-initial-overrides-button");
        _restoreInitialOverridesButton.Add(new Image { image = _restoreInitialOverridesIcon });
        _restoreInitialOverridesButton.clicked += () =>
        {
            _blendShapeManager.TryRestoreInitialOverrides();
            UpdateActionButtonStates();
        };

        _restoreEditedOverridesButton = _element.Q<Button>("restore-edited-overrides-button");
        _restoreEditedOverridesButton.Add(new Image { image = _restoreEditedOverridesIcon });
        _restoreEditedOverridesButton.clicked += () =>
        {
            _blendShapeManager.TryRestoreEditedOverrides();
            UpdateActionButtonStates();
        };

        UpdateUndoRedoState();
        UpdateActionButtonStates();

        _blendShapeManager.OnAnyDataChange += UpdateActionButtonStates;
        _blendShapeManager.OnAnyDataChange += QueueUndoStateUpdate;

        var clipField = new ObjectField { objectType = typeof(AnimationClip) };
        clipField.AddToClassList("clip-import-field");
        clipField.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue is AnimationClip clip)
            {
                ImportClip(clip);
                clipField.SetValueWithoutNotify(null);
            }
        });
        _element.Q<VisualElement>("clip-field-container").Add(clipField);

        var clipImportOptionField = new EnumField(_clipImportOption);
        clipImportOptionField.AddToClassList("clip-import-field");
        clipImportOptionField.RegisterValueChangedCallback(evt => _clipImportOption = (ClipImportOption)evt.newValue);
        _element.Q<VisualElement>("import-option-field-container").Add(clipImportOptionField);

        _filterContent = _element.Q<VisualElement>("filter-content");
        _groupTogglesContainer = _filterContent.Q<VisualElement>("group-toggles-container");
        RebuildGroupToggles();
        _groupManager.OnGroupSelectionChanged += _ => RebuildGroupToggles();

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
        leftToggle.RegisterValueChangedCallback(evt => _groupManager.IsLeftSelected = evt.newValue);

        var rightToggle = _filterContent.Q<Toggle>("right-toggle");
        rightToggle.SetValueWithoutNotify(_groupManager.IsRightSelected);
        rightToggle.RegisterValueChangedCallback(evt => _groupManager.IsRightSelected = evt.newValue);
    }

    private void ImportClip(AnimationClip clip)
    {
        var result = new BlendShapeWeightSet();
        clip.GetFirstFrameBlendShapes(_clipImportOption, result, null, _blendShapeManager.EffectiveBaseSet.ToBlendShapeAnimations().ToList());
        _blendShapeManager.OverrideShapesAndSetWeight(result.Select(x => (_blendShapeManager.GetIndexForShape(x.Name), x.Weight)));
    }

    private void UpdateActionButtonStates()
    {
        var hasRenderer = _context.Renderer != null;
        _saveButton?.SetEnabled(hasRenderer && _context.Targeting.GetTarget() != null && _blendShapeManager.IsChangedFromInitialState);
        _restoreInitialOverridesButton?.SetEnabled(hasRenderer && _blendShapeManager.IsChangedFromInitialState);
        _restoreEditedOverridesButton?.SetEnabled(hasRenderer && _blendShapeManager.CanRestoreEditedOverrides);
    }

    public void Dispose()
    {
        Undo.undoRedoPerformed -= QueueUndoStateUpdate;
        EditorApplication.delayCall -= _undoStateUpdateCallback;
        _blendShapeManager.OnAnyDataChange -= UpdateActionButtonStates;
        _blendShapeManager.OnAnyDataChange -= QueueUndoStateUpdate;
    }

    private void RebuildGroupToggles()
    {
        _groupTogglesContainer.Clear();
        _groupToggles.Clear();
        var toggleWidth = CalculateGroupToggleWidth();
        foreach (var group in _groupManager.Groups)
        {
            var toggle = new SimpleToggle { text = group.Name, value = group.IsSelected };
            toggle.AddToClassList("group-toggle");
            toggle.style.width = toggleWidth;
            toggle.style.flexBasis = toggleWidth;
            toggle.RegisterValueChangedCallback(evt => group.IsSelected = evt.newValue);
            _groupTogglesContainer.Add(toggle);
            _groupToggles.Add(toggle);
        }
    }

    private float CalculateGroupToggleWidth()
    {
        if (_groupManager.Groups.Count == 0) return 0f;

        return _groupManager.Groups
            .Max(group => EditorStyles.miniButton.CalcSize(new GUIContent(group.Name)).x + GroupToggleHorizontalPadding);
    }
}
