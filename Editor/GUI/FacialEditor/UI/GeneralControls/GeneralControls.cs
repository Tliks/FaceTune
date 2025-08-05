using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal class GeneralControls
{
    private readonly TargetManager _targetManager;
    private readonly BlendShapeOverrideManager _blendShapeManager;
    private readonly BlendShapeGrouping _groupManager;
    private readonly PreviewManager _previewManager;

    private static VisualTreeAsset _uxml = null!;
    private static StyleSheet _uss = null!;
    private readonly VisualElement _element;
    public VisualElement Element => _element;
    private VisualElement _targetingOptionsContainer = null!;
    private VisualElement _groupTogglesContainer = null!;
    private readonly List<Toggle> _groupToggles = new();

    private AnimationClip? _clip;
    private ClipImportOption _clipImportOption = ClipImportOption.NonZero;

    public GeneralControls(TargetManager targetManager, BlendShapeOverrideManager blendShapeManager, BlendShapeGrouping groupManager, PreviewManager previewManager)
    {
        _targetManager = targetManager;
        _blendShapeManager = blendShapeManager;
        _groupManager = groupManager;
        _previewManager = previewManager;

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

    private static Dictionary<Type, Func<IShapesEditorTargeting>> _targetingTypes = new()
    {
        { typeof(AnimationClip), () => new AnimationClipTargeting() },
        { typeof(FacialDataComponent), () => new FacialDataTargeting() },
        { typeof(FacialStyleComponent), () => new FacialStyleTargeting() },
        { typeof(AdvancedEyeBlinkComponent), () => new AdvancedEyeBlinkTargeting() },
        { typeof(AdvancedLipSyncComponent), () => new AdvancedLipSyncTargeting() },
    };
    private static Dictionary<string, Type> _targetingTypeNames = _targetingTypes.ToDictionary(x => x.Key.Name, x => x.Key);

    private void SetupControls()
    {
        var targetPanel = _element.Q<VisualElement>("target-panel");

        var targetRendererField = targetPanel.Q<ObjectField>("target-renderer-field");
        targetRendererField.objectType = typeof(SkinnedMeshRenderer);
        targetRendererField.RegisterValueChangedCallback(evt =>
        {
            _targetManager.TrySetTargetRenderer(evt.newValue as SkinnedMeshRenderer);
        });
        _targetManager.OnTargetRendererChanged += (renderer) =>
        {
            targetRendererField.SetValueWithoutNotify(renderer);
        };

        var targetingField = targetPanel.Q<ObjectField>("targeting-object-field");
        targetingField.RegisterValueChangedCallback(evt =>
        {
            _targetManager.Targeting?.SetTarget(evt.newValue);
        });
        _targetManager.OnTargetingChanged += (targeting) =>
        {
            targetingField.SetValueWithoutNotify(targeting?.GetTarget());
        };

        var targetingTypeField = targetPanel.Q<DropdownField>("targeting-type-field");
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

        _targetingOptionsContainer = targetPanel.Q<VisualElement>("targeting-options-container");
        RefreshTargetingContainer();
        _targetManager.OnTargetingChanged += (targeting) =>
        {
            RefreshTargetingContainer();
        };

        var saveButton = targetPanel.Q<Button>("save-button");
        saveButton.clicked += () =>
        {
            _targetManager.Save();
        };
        saveButton.SetEnabled(_targetManager.CanSave);
        _targetManager.OnCanSaveChanged += (canSave) => saveButton.SetEnabled(canSave);

        _groupTogglesContainer = _element.Q<VisualElement>("group-toggles-container");
        RefreshGroupToggles();
        _groupManager.OnGroupSelectionChanged += (groups) => RefreshGroupToggles();

        _element.Q<Button>("all-button").clicked += () =>
        {
            for (int i = 0; i < _groupManager.Groups.Count; i++)
            {
                _groupToggles[i].SetValueWithoutNotify(true);
            }
            _groupManager.SelectAll(true);
        };
        
        _element.Q<Button>("none-button").clicked += () =>
        {
            for (int i = 0; i < _groupManager.Groups.Count; i++)
            {
                _groupToggles[i].SetValueWithoutNotify(false);
            }
            _groupManager.SelectAll(false);
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
            _clip.GetFirstFrameBlendShapes(resutlt, _clipImportOption, _blendShapeManager.StyleSet.ToBlendShapeAnimations().ToList());
            _blendShapeManager.OverrideShapesAndSetWeight(resutlt.Select(x => (_blendShapeManager.GetIndexForShape(x.Name), x.Weight)));
        };

        var previewSettingPanel = _element.Q<VisualElement>("preview-setting-panel");

        var setBlendShapeTo100OnHoverButton = previewSettingPanel.Q<Toggle>("set-blendshape-to-100-on-hover-button");
        setBlendShapeTo100OnHoverButton.value = _previewManager.SetBlendShapeTo100OnHover;
        setBlendShapeTo100OnHoverButton.RegisterValueChangedCallback(evt => _previewManager.SetBlendShapeTo100OnHover = evt.newValue);
        _previewManager.OnSetBlendShapeTo100OnHoverChanged += (value) => setBlendShapeTo100OnHoverButton.SetValueWithoutNotify(value);

        var highlightBlendShapeVerticesOnHoverButton = previewSettingPanel.Q<Toggle>("highlight-blendshape-vertices-on-hover-button");
        highlightBlendShapeVerticesOnHoverButton.value = _previewManager.HighlightBlendShapeVerticesOnHover;
        highlightBlendShapeVerticesOnHoverButton.RegisterValueChangedCallback(evt => _previewManager.HighlightBlendShapeVerticesOnHover = evt.newValue);
        _previewManager.OnHighlightBlendShapeVerticesOnHoverChanged += (value) => highlightBlendShapeVerticesOnHoverButton.SetValueWithoutNotify(value);
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
        // RefreshTargetingContainer();
        RefreshGroupToggles();
    }

    private void RefreshGroupToggles()
    {
        _groupTogglesContainer.Clear();
        _groupToggles.Clear();
        foreach (var group in _groupManager.Groups)
        {
            var toggle = new Toggle($"{group.Name} ({group.BlendShapeIndices.Count})") { value = group.IsSelected };
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