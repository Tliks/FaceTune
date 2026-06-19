using UnityEditor;
using UnityEngine.UIElements;
using Aoyon.FaceTune.Gui.Components;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal class SelectedPanel
{
    private readonly BlendShapeOverrideManager _blendShapeManager;
    private readonly BlendShapeGrouping _groupManager;

    private readonly VisualElement _element;
    public VisualElement Element => _element;

    private static VisualTreeAsset? _uxml;
    private static VisualTreeAsset? _itemUxml;
    private static StyleSheet? _uss;
    private static StyleSheet? _itemUss;

    private TextField _searchField = null!;
    private SimpleToggle _styleToggle = null!;
    private SimpleToggle _baseToggle = null!;
    private SimpleToggle _zeroToggle = null!;

    private VisualElement _control = null!;

    private ListView _selectedListView = null!;
    
    private Button _selectedRemoveAll0Button = null!;
    private readonly Dictionary<int, double> _flashExpiryByKeyIndex = new();
    private IVisualElementScheduledItem? _flashCleanupSchedule;
    
    private struct ElementData
    {
        public string ShapeName;
        public int KeyIndex;
        public bool IsBase;
        public bool IsStyle;
    }

    private IReadOnlyList<ElementData> _allSource = null!;
    private List<ElementData> _currentSource = null!;

    private static readonly Texture _toggleIcon = EditorGUIUtility.IconContent("d_preAudioLoopOff").image;
    private static readonly Texture _removeIcon = EditorGUIUtility.IconContent("d_Toolbar Minus").image;

	public event Action<int>? OnSelectedItemNameClicked;

    public SelectedPanel(BlendShapeOverrideManager blendShapeManager, BlendShapeGrouping groupManager)
    {
        _blendShapeManager = blendShapeManager;
        _groupManager = groupManager;
        
        var uxml = UIAssetHelper.EnsureUxmlWithGuid(ref _uxml, "ccc8142fd21b4034aab76f2ac215b67e");
        var itemUxml = UIAssetHelper.EnsureUxmlWithGuid(ref _itemUxml, "fc51e445111d2074091e2fef5d3565f9");
        var uss = UIAssetHelper.EnsureUssWithGuid(ref _uss, "1adda987d131ce34c8d57981b20ac1f8");
        var itemUss = UIAssetHelper.EnsureUssWithGuid(ref _itemUss, "a00c7162d21d9e34ab15764bdb0d1173");
        
        _element = uxml.CloneTree();
        _element.styleSheets.Add(uss);
        Localization.LocalizeUIElements(_element);
        
        SetupControls();
        SetupListViews();
        
        // rebuild sourcce
        _groupManager.OnGroupSelectionChanged += (groups) => RebuildListViewsSlow();
        _groupManager.OnLeftSelectionChanged += (isLeftSelected) => RebuildListViewsSlow();
        _blendShapeManager.OnSingleShapeOverride += (keyIndex) =>
        {
            RebuildListViewsSlow();
            FlashOverrides(new[] { keyIndex });
        };
        _blendShapeManager.OnMultipleShapeOverride += (keyIndices) =>
        {
            RebuildListViewsSlow();
            FlashOverrides(keyIndices);
        };
        _blendShapeManager.OnSingleShapeUnoverride += (keyIndex) => RebuildListViewsSlow();
        _blendShapeManager.OnMultipleShapeUnoverride += (keyIndices) => RebuildListViewsSlow();
        // _blendShapeManager.OnSingleShapeWeightChanged += (keyIndex) => RebuildListViewsSlow();
        _blendShapeManager.OnMultipleShapeWeightChanged += (keyIndices) => RebuildListViewsSlow();
        _blendShapeManager.OnUnknownChange += () => RebuildListViewsSlow();
        _blendShapeManager.OnAnyDataChange += () => UpdateSelectedRemoveAll0ButtonVisibility();
    }

    private bool _selectedZero = true;
    private void SetupControls()
    {
        _searchField = _element.Q<TextField>("search-field");
        _searchField.RegisterValueChangedCallback(_ => RebuildListViewsSlow());

        _control = _element.Q("selected-shapes-controls");

        _styleToggle = _control.Q<SimpleToggle>("style-toggle");
        _styleToggle.RegisterValueChangedCallback(evt => RebuildListViewsSlow());

        _baseToggle = _control.Q<SimpleToggle>("base-toggle");
        _baseToggle.RegisterValueChangedCallback(evt => RebuildListViewsSlow());

        _zeroToggle = _control.Q<SimpleToggle>("zero-toggle");
        _zeroToggle.RegisterValueChangedCallback(evt => RebuildListViewsSlow());

        _selectedRemoveAll0Button = _control.Q<Button>("selected-remove-all-0-button");
        UpdateSelectedRemoveAll0ButtonVisibility();
        _selectedRemoveAll0Button.clicked += () =>
        {
            // 現在表示しているものに限らず全ブレンドシェイプから0値を削除
            var indices = _blendShapeManager.GetOverridenIndices(index => !_blendShapeManager.IsBaseShape(index) && _blendShapeManager.GetShapeWeight(index) == 0f); 
            _blendShapeManager.UnoverrideShapes(indices);
        };

        var selected0100Toggle = _control.Q<Button>("selected-0-100-toggle");
        selected0100Toggle.Add(new Image { image = _toggleIcon });
        selected0100Toggle.clicked += () =>
        {
            var indices = _currentSource.Select(item => item.KeyIndex);
            _blendShapeManager.SetShapesWeight(indices, _selectedZero ? 100f : 0f);
            _selectedZero = !_selectedZero;
        };

        var removeAllButton = _control.Q<Button>("remove-all-button");
        removeAllButton.Add(new Image { image = _removeIcon });
        removeAllButton.clicked += () =>
        {
            var indices = _currentSource
                .Select(item => item.KeyIndex)
                .Where(index => _blendShapeManager.IsOverridden(index));
            _blendShapeManager.UnoverrideShapes(indices);
        };
    }

    private void SetupListViews()
    {
        _selectedListView = _element.Q<ListView>("selected-list-view");
        _selectedListView.focusable = true;
        _selectedListView.selectionType = SelectionType.None;
        _selectedListView.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;

        InitializeListSource();

        _selectedListView.makeItem = MakeElement;
        _selectedListView.bindItem = BindElement;

        VisualElement MakeElement()
        {
            var element = _itemUxml!.CloneTree();
            element.styleSheets.Add(_itemUss!);
            Localization.LocalizeUIElements(element);

            var flashOverlay = new VisualElement { name = "flash-overlay", pickingMode = PickingMode.Ignore };
            flashOverlay.AddToClassList("flash-overlay");
            element.Insert(0, flashOverlay);

            var nameLabel = element.Q<Label>("name");
            var sliderFloatField = element.Q<SliderFloatField>("slider-float-field");
            var toggleButton = element.Q<Button>("toggle-button");
            var actionButton = element.Q<Button>("action");

            nameLabel.RegisterCallback<ClickEvent>(evt =>
            {
                if (element.userData is ElementData item)
                {
                    OnSelectedItemNameClicked?.Invoke(item.KeyIndex);
                }
            });

            sliderFloatField.RegisterValueChangedCallback(evt =>
            {
                if (element.userData is ElementData item)
                {
                    _blendShapeManager.SetShapeWeight(item.KeyIndex, evt.newValue);
                    UpdateActionButton(item, actionButton);
                }
            });
            _blendShapeManager.OnSingleShapeWeightChanged += (keyIndex) =>
            {
                if (element.userData is ElementData item && item.KeyIndex == keyIndex)
                {
                    sliderFloatField.SetValueWithoutNotify(_blendShapeManager.GetEffectiveShapeWeight(keyIndex));
                    UpdateActionButton(item, actionButton);
                }
            };
             
            toggleButton.text = "";
            toggleButton.Add(new Image { image = _toggleIcon });
            actionButton.text = "";
            actionButton.Add(new Image { image = _removeIcon });

            toggleButton.clicked += () =>
            {
                if (element.userData is ElementData item)
                {
                    var currentWeight = _blendShapeManager.GetEffectiveShapeWeight(item.KeyIndex);
                    var newWeight = currentWeight == 0f ? 100f : 0f;
                    _blendShapeManager.SetShapeWeight(item.KeyIndex, newWeight);
                    sliderFloatField.SetValueWithoutNotify(newWeight);
                    UpdateActionButton(item, actionButton);
                }
            };
                        
            actionButton.clicked += () =>
            {
                if (element.userData is ElementData item)
                {
                    _blendShapeManager.UnoverrideShape(item.KeyIndex);
                    sliderFloatField.SetValueWithoutNotify(_blendShapeManager.GetEffectiveShapeWeight(item.KeyIndex));
                    UpdateActionButton(item, actionButton);
                }
            };
            
			return element;
        }

        void BindElement(VisualElement element, int index)
        {
            var item = _currentSource[index];
            element.userData = item;

            var flashOverlay = element.Q<VisualElement>("flash-overlay");
            if (flashOverlay != null)
            {
                if (_flashExpiryByKeyIndex.ContainsKey(item.KeyIndex))
                    flashOverlay.style.opacity = 1f;
                else
                    flashOverlay.style.opacity = 0f;
            }
             
            var nameLabel = element.Q<Label>("name");
            var sliderFloatField = element.Q<SliderFloatField>("slider-float-field");
            var actionButton = element.Q<Button>("action");
            
            nameLabel.text = item.ShapeName;
            var currentWeight = _blendShapeManager.GetEffectiveShapeWeight(item.KeyIndex);
            sliderFloatField.SetValueWithoutNotify(currentWeight);
            UpdateActionButton(item, actionButton);
        }

        void UpdateActionButton(ElementData item, Button actionButton)
        {
            actionButton.SetEnabled(_blendShapeManager.IsOverridden(item.KeyIndex));
        }
    }

    private void FlashOverrides(IEnumerable<int> keyIndices)
    {
        var now = EditorApplication.timeSinceStartup;
        const double fadeInSeconds = 0.5;
        var expiry = now + fadeInSeconds;

        foreach (var keyIndex in keyIndices)
        {
            _flashExpiryByKeyIndex[keyIndex] = expiry;
        }

        _selectedListView.RefreshItems();

        _flashCleanupSchedule ??= _element.schedule.Execute(() =>
        {
            if (_flashExpiryByKeyIndex.Count == 0)
            {
                _flashCleanupSchedule?.Pause();
                return;
            }

            var current = EditorApplication.timeSinceStartup;
            using var _ = new ProfilingSampleScope("SelectedPanel.FlashOverrides.Cleanup");
            var removedAny = false;
            foreach (var pair in _flashExpiryByKeyIndex.ToList())
            {
                if (pair.Value <= current)
                {
                    _flashExpiryByKeyIndex.Remove(pair.Key);
                    removedAny = true;
                }
            }

            if (removedAny)
            {
                _selectedListView.RefreshItems();
            }

            if (_flashExpiryByKeyIndex.Count == 0)
            {
                _flashCleanupSchedule?.Pause();
            }
        }).Every(100);

        _flashCleanupSchedule.Resume();
    }

    private void InitializeListSource()
    {
        UpdateSourceToggleVisibility();

        var allSource = new List<ElementData>();
        var allKeys = _blendShapeManager.AllKeys;
        for (int i = 0; i < allKeys.Count; i++)
        {
            allSource.Add(new ElementData
            {
                ShapeName = allKeys[i],
                KeyIndex = i,
                IsBase = _blendShapeManager.IsBaseShape(i),
                IsStyle = _blendShapeManager.IsStyleShape(i)
            });
        }
        _allSource = allSource.AsReadOnly();
        _currentSource = new();
        BuildCurrentSource();

        _selectedListView.itemsSource = _currentSource;

        _selectedListView.RefreshItems();
    }

    private void UpdateSourceToggleVisibility()
    {
        _styleToggle.SetVisible(_blendShapeManager.StyleSet.Count > 0);
        _baseToggle.SetVisible(_blendShapeManager.BaseSet.Count > 0);
    }

    private void UpdateSelectedRemoveAll0ButtonVisibility()
    {
        var anyZero = _blendShapeManager.GetOverridenIndices(index => !_blendShapeManager.IsBaseShape(index) && _blendShapeManager.GetShapeWeight(index) == 0f).Any();
        _selectedRemoveAll0Button.SetVisible(anyZero);
    }

    private void BuildCurrentSource()
    {
        using var _ = new ProfilingSampleScope("SelectedPanel.BuildCurrentSource");

        _currentSource.Clear();

        var searchText = _searchField.value?.ToLower() ?? "";
        var hasSearchText = !string.IsNullOrEmpty(searchText);

        var allSourceCount = _allSource.Count;
        for (int i = 0; i < allSourceCount; i++)
        {
            var item = _allSource[i];

            if (hasSearchText && !item.ShapeName.ToLower().Contains(searchText))
                continue;

            if (_groupManager.IsLeftSelected && !_groupManager.IsBlendShapeVisible(item.KeyIndex))
                continue;

            var isOverridden = _blendShapeManager.IsOverridden(item.KeyIndex);
            if (!item.IsStyle && !item.IsBase && !isOverridden)
                continue;

            if (!_styleToggle.value && item.IsStyle && !isOverridden)
                continue;

            if (!_baseToggle.value && item.IsBase && !isOverridden)
                continue;

            if (!_zeroToggle.value && _blendShapeManager.GetEffectiveShapeWeight(item.KeyIndex) == 0f)
                continue;

            _currentSource.Add(item);
        }
    }


    private void RebuildListViewsSlow()
    {
        BuildCurrentSource();
        _selectedListView.RefreshItems();
    }
}
