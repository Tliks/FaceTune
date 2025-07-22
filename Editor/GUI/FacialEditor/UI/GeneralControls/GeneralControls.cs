using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace aoyon.facetune.gui.shapes_editor;

internal class GeneralControls
{
    private readonly BlendShapeOverrideManager _blendShapeManager;
    private readonly BlendShapeGrouping _groupManager;
    private readonly List<Toggle> _groupToggles = new();
    private readonly VisualElement _element;
    
    private static VisualTreeAsset _uxml = null!;
    private static StyleSheet _uss = null!;

    public VisualElement Element => _element;
    
    public event Action? OnGroupSelectionChanged;
    public event Action<bool>? OnSetBlendShapeTo100OnHoverChanged;
    public event Action<bool>? OnHighlightBlendShapeVerticesOnHoverChanged;

    private bool _setBlendShapeTo100OnHover = true;
    public bool SetBlendShapeTo100OnHover
    {
        get => _setBlendShapeTo100OnHover;
        set
        {
            _setBlendShapeTo100OnHover = value;
            OnSetBlendShapeTo100OnHoverChanged?.Invoke(_setBlendShapeTo100OnHover);
        }
    }
    private bool _highlightBlendShapeVerticesOnHover = false;
    public bool HighlightBlendShapeVerticesOnHover
    {
        get => _highlightBlendShapeVerticesOnHover;
        set
        {
            _highlightBlendShapeVerticesOnHover = value;
            OnHighlightBlendShapeVerticesOnHoverChanged?.Invoke(_highlightBlendShapeVerticesOnHover);
        }
    }

    private AnimationClip? _clip;
    private ClipImportOption _clipImportOption = ClipImportOption.NonZero;

    public GeneralControls(BlendShapeOverrideManager blendShapeManager, BlendShapeGrouping groupManager)
    {
        _blendShapeManager = blendShapeManager;
        _groupManager = groupManager;

        EnsureAssets();
        _element = _uxml.CloneTree();
        _element.styleSheets.Add(_uss);
        SetupControls();
    }

    private void EnsureAssets()
    {
        UIAssetHelper.EnsureUxmlWithGuid(ref _uxml, "41adb90607cdad24292515795aeb1680");
        UIAssetHelper.EnsureUssWithGuid(ref _uss, "d76d3f47e63003541b2f77817315d701");
    }

    private void SetupControls()
    {
        var groupTogglesContainer = _element.Q<VisualElement>("group-toggles-container");
        
        _groupToggles.Clear();
        foreach (var group in _groupManager.Groups)
        {
            var toggle = new Toggle($"{group.Name}({group.BlendShapeIndices.Count})") { value = group.IsSelected };
            toggle.AddToClassList("group-toggle");
            toggle.RegisterValueChangedCallback(evt =>
            {
                group.IsSelected = evt.newValue;
                OnGroupSelectionChanged?.Invoke();
            });
            groupTogglesContainer.Add(toggle);
            _groupToggles.Add(toggle);
        }

        _element.Q<Button>("all-button").clicked += () =>
        {
            _groupManager.SelectAll(true);
            for (int i = 0; i < _groupManager.Groups.Count; i++)
            {
                _groupToggles[i].SetValueWithoutNotify(true);
            }
            OnGroupSelectionChanged?.Invoke();
        };
        
        _element.Q<Button>("none-button").clicked += () =>
        {
            _groupManager.SelectAll(false);
            for (int i = 0; i < _groupManager.Groups.Count; i++)
            {
                _groupToggles[i].SetValueWithoutNotify(false);
            }
            OnGroupSelectionChanged?.Invoke();
        };


        var clipImportPanel = _element.Q<VisualElement>("clip-import-panel");
        
        var clipField = clipImportPanel.Q<ObjectField>("clip-field");
        clipField.objectType = typeof(AnimationClip);
        clipField.RegisterValueChangedCallback(evt =>
        {
            _clip = evt.newValue as AnimationClip;
        });

        var clipImportOptionField = clipImportPanel.Q<EnumField>("import-option-field");
        clipImportOptionField.Init(_clipImportOption);
        clipImportOptionField.RegisterValueChangedCallback(evt =>
        {
            _clipImportOption = (ClipImportOption)evt.newValue;
        });

        var importClipButton = clipImportPanel.Q<Button>("import-clip-button");
        importClipButton.clicked += () =>
        {
            if (_clip == null) return;
            var resutlt = new BlendShapeSet();
            _clip.GetFirstFrameBlendShapes(resutlt, _clipImportOption, _blendShapeManager.StyleSet);
            _blendShapeManager.OverrideShapesAndSetWeight(resutlt.Select(x => (_blendShapeManager.GetIndexForShape(x.Name), x.Weight)));
        };

        var previewSettingPanel = _element.Q<VisualElement>("preview-setting-panel");

        var setBlendShapeTo100OnHoverButton = previewSettingPanel.Q<Toggle>("set-blendshape-to-100-on-hover-button");
        setBlendShapeTo100OnHoverButton.value = SetBlendShapeTo100OnHover;
        setBlendShapeTo100OnHoverButton.RegisterValueChangedCallback(evt => SetBlendShapeTo100OnHover = evt.newValue);

        var highlightBlendShapeVerticesOnHoverButton = previewSettingPanel.Q<Toggle>("highlight-blendshape-vertices-on-hover-button");
        highlightBlendShapeVerticesOnHoverButton.value = HighlightBlendShapeVerticesOnHover;
        highlightBlendShapeVerticesOnHoverButton.RegisterValueChangedCallback(evt => HighlightBlendShapeVerticesOnHover = evt.newValue);
    }
}