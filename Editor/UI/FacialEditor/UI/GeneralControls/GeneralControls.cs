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
    private bool _actionStateRefreshPending;

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

        var uxml = UIAssetHelper.EnsureUxmlWithGuid(ref _uxml, "41adb90607cdad24292515795aeb1680");
        var uss = UIAssetHelper.EnsureUssWithGuid(ref _uss, "d76d3f47e63003541b2f77817315d701");

        _element = uxml.CloneTree();
        _element.styleSheets.Add(uss);
        Localization.LocalizeUIElements(_element);

        SetupControls();
    }

    private void UpdateUndoRedoState()
    {
        _undoButton?.SetEnabled(_blendShapeManager.CanUndo);
        _redoButton?.SetEnabled(_blendShapeManager.CanRedo);
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

        if (_context.Renderer == null)
        {
            _element.Q<VisualElement>("targeting-content").Insert(1, new HelpBox(
                "facialEditor.rendererRequired.message".LS(),
                HelpBoxMessageType.Warning));
        }

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
        else
        {
            targetingOptionsContainer.RemoveFromHierarchy();
        }

        _saveButton = _element.Q<Button>("save-button");
        _saveButton.clicked += _save;

        _undoButton = _element.Q<Button>("undo-button");
        _undoButton.Add(CreateStepIcon(_undoIcon));
        _undoButton.clicked += Undo.PerformUndo;

        _redoButton = _element.Q<Button>("redo-button");
        _redoButton.Add(CreateStepIcon(_redoIcon));
        _redoButton.clicked += Undo.PerformRedo;

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

        _blendShapeManager.OnAnyDataChange += UpdateUndoRedoState;
        _blendShapeManager.OnAnyDataChange += RequestActionButtonStateUpdate;

        var clipField = new ObjectField { objectType = typeof(AnimationClip) };
        clipField.AddToClassList("compact-field");
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

        var clipImportOptions = new List<string>
        {
            "clipImportOption.option.all".LS(),
            "clipImportOption.option.nonZero".LS()
        };
        var selectedClipImportOption = _clipImportOption == ClipImportOption.All ? 0 : 1;
        var clipImportOptionField = new PopupField<string>(clipImportOptions, selectedClipImportOption);
        clipImportOptionField.AddToClassList("compact-field");
        clipImportOptionField.RegisterValueChangedCallback(evt =>
        {
            _clipImportOption = evt.newValue == clipImportOptions[0] ? ClipImportOption.All : ClipImportOption.NonZero;
        });
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

    private static Image CreateStepIcon(Texture icon)
    {
        var image = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
        image.AddToClassList("step-action-icon");
        return image;
    }

    private void ImportClip(AnimationClip clip)
    {
        var animations = new List<BlendShapeWeightAnimation>();
        clip.GetBlendShapeAnimations(_clipImportOption, animations, string.Empty);
        _blendShapeManager.AddShapesWithWeight(
            animations
                .Where(animation => !animation.IsMultiFrame)
                .Select(animation =>
                (
                    _blendShapeManager.GetIndexForShape(animation.Name),
                    animation.ToFirstFrameBlendShape().Weight
                )));
    }

    private void RequestActionButtonStateUpdate()
    {
        if (_actionStateRefreshPending) return;

        _actionStateRefreshPending = true;
        _element.schedule.Execute(() =>
        {
            _actionStateRefreshPending = false;
            UpdateActionButtonStates();
        });
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
        _blendShapeManager.OnAnyDataChange -= UpdateUndoRedoState;
        _blendShapeManager.OnAnyDataChange -= RequestActionButtonStateUpdate;
    }

    private void RebuildGroupToggles()
    {
        _groupTogglesContainer.Clear();
        _groupToggles.Clear();
        var toggleWidth = CalculateGroupToggleWidth();
        foreach (var group in _groupManager.Groups)
        {
            var toggle = new SimpleToggle { text = group.Name, value = group.IsSelected };
            toggle.AddToClassList("compact-control");
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
