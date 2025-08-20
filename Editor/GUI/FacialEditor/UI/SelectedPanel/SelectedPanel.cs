using UnityEngine.UIElements;
using Aoyon.FaceTune.Gui.Components;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal class SelectedPanel
{
    private readonly BlendShapeOverrideManager _blendShapeManager;
    private readonly BlendShapeGrouping _groupManager;

    private readonly VisualElement _element;
    public VisualElement Element => _element;

    private static VisualTreeAsset _uxml = null!;
    private static VisualTreeAsset _itemUxml = null!;
    private static StyleSheet _uss = null!;

    private TextField _searchField = null!;
    private VisualElement _baseShapesPanel = null!;
    private VisualElement _selectedShapesPanel = null!;
    private ListView _baseListView = null!;
    private ListView _selectedListView = null!;
    private SimpleToggle _baseZeroToggle = null!;
    private SimpleToggle _selectedZeroToggle = null!;
    
    private struct ElementData
    {
        public string ShapeName;
        public int KeyIndex;
        public bool IsBase;
    }

    private IReadOnlyList<ElementData> _allSource = null!;
    private List<ElementData> _currentBaseSource = null!;
    private List<ElementData> _currentSelectedSource = null!;

    private static readonly Texture _toggleIcon = EditorGUIUtility.IconContent("d_preAudioLoopOff@2x").image;
    private static readonly Texture _resetIcon = EditorGUIUtility.IconContent("d_Toolbar Minus@2x").image;
    private static readonly Texture _removeIcon = EditorGUIUtility.IconContent("d_Toolbar Minus@2x").image;

	public event Action<int>? OnSelectedItemNameClicked;

    public SelectedPanel(BlendShapeOverrideManager blendShapeManager, BlendShapeGrouping groupManager)
    {
        _blendShapeManager = blendShapeManager;
        _groupManager = groupManager;
        
        EnsureAssets();
        
        _element = _uxml.CloneTree();
        _element.styleSheets.Add(_uss);
        
        SetupControls();
        SetupListViews();
        
        // rebuild sourcce
        _groupManager.OnGroupSelectionChanged += (groups) => BuildAndRefreshListViewsSlow();
        _groupManager.OnLeftSelectionChanged += (isLeftSelected) => BuildAndRefreshListViewsSlow();
        _blendShapeManager.OnSingleShapeOverride += (keyIndex) => AddByKeyIndex(keyIndex);
        _blendShapeManager.OnMultipleShapeOverride += (keyIndices) => BuildAndRefreshListViewsSlow();
        _blendShapeManager.OnSingleShapeUnoverride += (keyIndex) => RemoveByKeyIndex(keyIndex);
        _blendShapeManager.OnMultipleShapeUnoverride += (keyIndices) => BuildAndRefreshListViewsSlow();
        // _blendShapeManager.OnSingleShapeWeightChanged += (keyIndex) => BuildAndRefreshListViewsSlow();
        _blendShapeManager.OnMultipleShapeWeightChanged += (keyIndices) => BuildAndRefreshListViewsSlow();
        _blendShapeManager.OnUnknownChange += () => BuildAndRefreshListViewsSlow();
        _blendShapeManager.OnBaseSetChange += () => { RefreshTarget(); UpdateBaseListVisibility(); };
    }

    private void EnsureAssets()
    {
        UIAssetHelper.EnsureUxmlWithGuid(ref _uxml, "ccc8142fd21b4034aab76f2ac215b67e");
        UIAssetHelper.EnsureUxmlWithGuid(ref _itemUxml, "fc51e445111d2074091e2fef5d3565f9");
        UIAssetHelper.EnsureUssWithGuid(ref _uss, "1adda987d131ce34c8d57981b20ac1f8");
    }

    private void SetupControls()
    {
        var commonControls = _element.Q<VisualElement>("common-controls-panel");
        _searchField = commonControls.Q<TextField>("search-field");
        _searchField.RegisterValueChangedCallback(_ => BuildAndRefreshListViewsSlow());

        _baseShapesPanel = _element.Q("base-shapes-panel");

        _baseShapesPanel.Q<Button>("base-set-all-100-button").clicked += () =>
        {
            var indices = _currentBaseSource.Select(item => item.KeyIndex);
            _blendShapeManager.SetShapesWeight(indices, 100f);
        };
        _baseShapesPanel.Q<Button>("base-set-all-0-button").clicked += () =>
        {
            var indices = _currentBaseSource.Select(item => item.KeyIndex);
            _blendShapeManager.SetShapesWeight(indices, 0f);
        };
        _baseShapesPanel.Q<Button>("base-reset-all-button").clicked += () =>
        {
            _blendShapeManager.ResetShapesWeight(_currentBaseSource.Select(item => item.KeyIndex));
        };
        _baseZeroToggle = _baseShapesPanel.Q<SimpleToggle>("base-zero-toggle");
        _baseZeroToggle.RegisterValueChangedCallback(evt =>
        {
            BuildAndRefreshBaseListViewsSlow();
        });

        _selectedShapesPanel = _element.Q("selected-shapes-panel");

        _selectedShapesPanel.Q<Button>("selected-set-all-100-button").clicked += () =>
        {
            var indices = _currentSelectedSource.Select(item => item.KeyIndex);
            _blendShapeManager.SetShapesWeight(indices, 100f);
        };
        _selectedShapesPanel.Q<Button>("selected-set-all-0-button").clicked += () =>
        {
            var indices = _currentSelectedSource.Select(item => item.KeyIndex);
            _blendShapeManager.SetShapesWeight(indices, 0f);
        };
        _selectedShapesPanel.Q<Button>("remove-all-button").clicked += () =>
        {
            var indices = _currentSelectedSource.Select(item => item.KeyIndex);
            _blendShapeManager.UnoverrideShapes(indices);
        };
        _selectedZeroToggle = _selectedShapesPanel.Q<SimpleToggle>("selected-zero-toggle");
        _selectedZeroToggle.RegisterValueChangedCallback(evt =>
        {
            BuildAndRefreshSelectedListViewsSlow();
        });
    }

    private void SetupListViews()
    {
        _baseListView = _element.Q<ListView>("base-list-view");
        _selectedListView = _element.Q<ListView>("selected-list-view");
        _baseListView.focusable = true;
        _selectedListView.focusable = true;
        _baseListView.selectionType = SelectionType.None;
        _selectedListView.selectionType = SelectionType.None;
        _baseListView.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
        _selectedListView.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;

        RefreshTarget();

        _baseListView.makeItem = () => MakeElement(true);
        _baseListView.bindItem = (e, i) => BindElement(e, i, true);

        _selectedListView.makeItem = () => MakeElement(false);
        _selectedListView.bindItem = (e, i) => BindElement(e, i, false);

        VisualElement MakeElement(bool isBase)
        {
            var element = _itemUxml.CloneTree();
            
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
                    UpdateActionButton(item, isBase, actionButton);
                }
            });
            _blendShapeManager.OnSingleShapeWeightChanged += (keyIndex) =>
            {
                if (element.userData is ElementData item && item.KeyIndex == keyIndex)
                {
                    sliderFloatField.SetValueWithoutNotify(_blendShapeManager.GetShapeWeight(keyIndex));
                    UpdateActionButton(item, isBase, actionButton);
                }
            };
            
            toggleButton.text = "";
            toggleButton.Add(new Image { image = _toggleIcon });
            actionButton.text = "";
            actionButton.Add(new Image { image = isBase ? _resetIcon : _removeIcon });

            toggleButton.clicked += () =>
            {
                if (element.userData is ElementData item)
                {
                    var currentWeight = _blendShapeManager.GetShapeWeight(item.KeyIndex);
                    var newWeight = currentWeight == 0f ? 100f : 0f;
                    _blendShapeManager.SetShapeWeight(item.KeyIndex, newWeight);
                    sliderFloatField.SetValueWithoutNotify(newWeight);
                    UpdateActionButton(item, isBase, actionButton);
                }
            };
                        
            actionButton.clicked += () =>
            {
                if (element.userData is ElementData item)
                {
                    if (isBase) // reset
                    {
                        var weight = _blendShapeManager.ResetShapeWeight(item.KeyIndex);
                        sliderFloatField.SetValueWithoutNotify(weight);
                    }
                    else // remove
                    {
                        _blendShapeManager.UnoverrideShape(item.KeyIndex);
                        RemoveByKeyIndex(item.KeyIndex);
                    }
                    UpdateActionButton(item, isBase, actionButton);
                }
            };
            
			return element;
        }

        void BindElement(VisualElement element, int index, bool isBase)
        {
            var item = isBase ? _currentBaseSource[index] : _currentSelectedSource[index];
            element.userData = item;
             
            var nameLabel = element.Q<Label>("name");
            var sliderFloatField = element.Q<SliderFloatField>("slider-float-field");
            var actionButton = element.Q<Button>("action");
            
            nameLabel.text = item.ShapeName;
            var currentWeight = _blendShapeManager.GetShapeWeight(item.KeyIndex);
            sliderFloatField.SetValueWithoutNotify(currentWeight);
            UpdateActionButton(item, isBase, actionButton);
        }

        void UpdateActionButton(ElementData item, bool isBase, Button actionButton)
        {
            if (isBase)
            {
                actionButton.SetEnabled(!_blendShapeManager.IsInitialBaseWeight(item.KeyIndex));
            }
        }
    }

    public void RefreshTarget()
    {
        var allSource = new List<ElementData>();
        var allKeys = _blendShapeManager.AllKeys;
        for (int i = 0; i < allKeys.Count; i++)
        {
            allSource.Add(new ElementData { ShapeName = allKeys[i], KeyIndex = i, IsBase = _blendShapeManager.IsBaseShape(i) });
        }
        _allSource = allSource.AsReadOnly();
        _currentBaseSource = new();
        _currentSelectedSource = new();
        BuildCurrentSource();

        _baseListView.itemsSource = _currentBaseSource;
        _selectedListView.itemsSource = _currentSelectedSource;

        _baseListView.RefreshItems();
        _selectedListView.RefreshItems();
    }

    private void UpdateBaseListVisibility()
    {
        var hasStyleShapes = _currentBaseSource.Count > 0;
        _baseShapesPanel.style.display = hasStyleShapes ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void BuildCurrentSource(bool isBase = true, bool selected = true)
    {
        using var _ = new ProfilingSampleScope("SelectedPanel.BuildCurrentSource");

        if (isBase)
            _currentBaseSource.Clear();
        if (selected)
            _currentSelectedSource.Clear();

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

            if (isBase && item.IsBase)
            {
                if (!_baseZeroToggle.value && _blendShapeManager.GetShapeWeight(item.KeyIndex) == 0f)
                    continue;

                _currentBaseSource.Add(item);
            }
            else if (selected)
            {
                if (!_selectedZeroToggle.value && _blendShapeManager.GetShapeWeight(item.KeyIndex) == 0f)
                    continue;

                if (_blendShapeManager.IsOverridden(item.KeyIndex))
                    _currentSelectedSource.Add(item);
            }
        }
    }


    public bool AddByKeyIndex(int keyIndex)
    {
        using var _ = new ProfilingSampleScope("SelectedPanel.AddByKeyIndex");
        var item = _allSource[keyIndex];

        return AddItemToSortedList(
            item.IsBase ? _currentBaseSource : _currentSelectedSource,
            item,
            item.IsBase ? _baseListView : _selectedListView
        );
    }

    private bool AddItemToSortedList(List<ElementData> list, ElementData item, ListView listView)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].KeyIndex == item.KeyIndex)
                return false;

            if (list[i].KeyIndex > item.KeyIndex)
            {
                list.Insert(i, item);
                listView.RefreshItems();
                return true;
            }
        }
        list.Add(item);
        listView.RefreshItems();
        return true;
    }
    
    public bool RemoveByKeyIndex(int keyIndex)
    {
        using var _ = new ProfilingSampleScope("SelectedPanel.RemoveByKeyIndex");
        
        // 選択済みリストから削除
        for (int i = 0; i < _currentSelectedSource.Count; i++)
        {
            if (_currentSelectedSource[i].KeyIndex == keyIndex)
            {
                _currentSelectedSource.RemoveAt(i);
                _selectedListView.RefreshItems();
                return true;
            }
        }
        
        // Baseからは削除しない
        return false;
    }

    private void BuildAndRefreshBaseListViewsSlow()
    {
        BuildCurrentSource(isBase: true, selected: false);
        _baseListView.RefreshItems();
    }

    private void BuildAndRefreshSelectedListViewsSlow()
    {
        BuildCurrentSource(isBase: false, selected: true);
        _selectedListView.RefreshItems();
    }

    private void BuildAndRefreshListViewsSlow()
    {
        BuildCurrentSource();
        _baseListView.RefreshItems();
        _selectedListView.RefreshItems();
    }
}
