using UnityEngine.UIElements;
using aoyon.facetune.ui.components;

namespace aoyon.facetune.ui.shapes_editor;

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
    private ListView _styleListView = null!;
    private ListView _selectedListView = null!;
    
    private struct ElementData
    {
        public string ShapeName;
        public int KeyIndex;
        public bool IsStyle;
    }

    private IReadOnlyList<ElementData> _allSource = null!;
    private List<ElementData> _currentStyleSource = null!;
    private List<ElementData> _currentSelectedSource = null!;

    public SelectedPanel(BlendShapeOverrideManager blendShapeManager, BlendShapeGrouping groupManager)
    {
        _blendShapeManager = blendShapeManager;
        _groupManager = groupManager;
        
        EnsureAssets();
        
        _element = _uxml.CloneTree();
        _element.styleSheets.Add(_uss);
        
        SetupControls();
        SetupListViews();
        _groupManager.OnGroupSelectionChanged += (group, selected) => BuildAndRefreshListViewsSlow();
        _blendShapeManager.OnSingleShapeOverride += (keyIndex) => AddByKeyIndex(keyIndex);
        _blendShapeManager.OnMultipleShapeOverride += (keyIndices) => BuildAndRefreshListViewsSlow();
        _blendShapeManager.OnUnknownChange += () => BuildAndRefreshListViewsSlow();
    }

    private void EnsureAssets()
    {
        UIUtility.EnsureUxmlWithGuid(ref _uxml, "ccc8142fd21b4034aab76f2ac215b67e");
        UIUtility.EnsureUxmlWithGuid(ref _itemUxml, "fc51e445111d2074091e2fef5d3565f9");
        UIUtility.EnsureUssWithGuid(ref _uss, "1adda987d131ce34c8d57981b20ac1f8");
    }

    private void SetupControls()
    {
        var commonControls = _element.Q<VisualElement>("common-controls-panel");
        _searchField = commonControls.Q<TextField>("search-field");
        _searchField.RegisterValueChangedCallback(_ => BuildAndRefreshListViewsSlow());

        commonControls.Q<Button>("set-all-100-button").clicked += () =>
        {
            var indices = _currentStyleSource.Select(item => item.KeyIndex).Concat(_currentSelectedSource.Select(item => item.KeyIndex));
            _blendShapeManager.SetShapesWeight(indices, 100f);
            BuildAndRefreshListViewsSlow();
        };
        
        commonControls.Q<Button>("set-all-0-button").clicked += () =>
        {
            var indices = _currentStyleSource.Select(item => item.KeyIndex).Concat(_currentSelectedSource.Select(item => item.KeyIndex));
            _blendShapeManager.SetShapesWeight(indices, 0f);
            BuildAndRefreshListViewsSlow();
        };
        
        var styleShapesPanel = _element.Q("style-shapes-panel");
        styleShapesPanel.Q<Button>("reset-all-button").clicked += () =>
        {
            foreach (var item in _currentStyleSource)
            {
                var initialWeight = _blendShapeManager.GetRequiredInitialStyleWeight(item.ShapeName);
                _blendShapeManager.SetShapeWeight(item.KeyIndex, initialWeight);
            }
            _styleListView.RefreshItems();
        };

        var selectedShapesPanel = _element.Q("selected-shapes-panel");
        selectedShapesPanel.Q<Button>("remove-all-button").clicked += () =>
        {
            var indices = _currentSelectedSource.Select(item => item.KeyIndex);
            _blendShapeManager.UnoverrideShapes(indices);
            BuildAndRefreshListViewsSlow();
        };

        var verticalSplitter = SplitterFactory.Create(styleShapesPanel, selectedShapesPanel, SplitterFactory.Direction.Vertical);
        _element.Insert(1, verticalSplitter);
    }

    private void SetupListViews()
    {
        _styleListView = _element.Q<ListView>("style-list-view");
        _selectedListView = _element.Q<ListView>("selected-list-view");
        _styleListView.focusable = false;
        _selectedListView.focusable = false;
        _styleListView.selectionType = SelectionType.None;
        _selectedListView.selectionType = SelectionType.None;
        _styleListView.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
        _selectedListView.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;

        var allSource = new List<ElementData>();
        var allKeys = _blendShapeManager.AllKeys;
        for (int i = 0; i < allKeys.Count; i++)
        {
            allSource.Add(new ElementData { ShapeName = allKeys[i], KeyIndex = i, IsStyle = _blendShapeManager.IsStyleShape(i) });
        }
        _allSource = allSource.AsReadOnly();
        _currentStyleSource = new();
        _currentSelectedSource = new();
        BuildCurrentSource();

        _styleListView.itemsSource = _currentStyleSource;
        _selectedListView.itemsSource = _currentSelectedSource;

        _styleListView.makeItem = () => MakeElement(true);
        _styleListView.bindItem = (e, i) => BindElement(e, i, true);

        _selectedListView.makeItem = () => MakeElement(false);
        _selectedListView.bindItem = (e, i) => BindElement(e, i, false);

        VisualElement MakeElement(bool isStyle)
        {
            var element = _itemUxml.CloneTree();
            
            var sliderFloatField = element.Q<SliderFloatField>("slider-float-field");
            var zeroButton = element.Q<Button>("zero-button");
            var hundredButton = element.Q<Button>("hundred-button");
            var actionButton = element.Q<Button>("action");
            
            sliderFloatField.RegisterValueChangedCallback(evt =>
            {
                if (element.userData is ElementData item)
                {
                    _blendShapeManager.SetShapeWeight(item.KeyIndex, evt.newValue);
                    UpdateActionButton(item, isStyle, actionButton);
                }
            });
            
            zeroButton.clicked += () =>
            {
                if (element.userData is ElementData item)
                {
                    _blendShapeManager.SetShapeWeight(item.KeyIndex, 0f);
                    sliderFloatField.SetValueWithoutNotify(0f);
                    UpdateActionButton(item, isStyle, actionButton);
                }
            };
            
            hundredButton.clicked += () =>
            {
                if (element.userData is ElementData item)
                {
                    _blendShapeManager.SetShapeWeight(item.KeyIndex, 100f);
                    sliderFloatField.SetValueWithoutNotify(100f);
                    UpdateActionButton(item, isStyle, actionButton);
                }
            };
            
            actionButton.clicked += () =>
            {
                if (element.userData is ElementData item)
                {
                    if (isStyle)
                    {
                        var weight = _blendShapeManager.GetRequiredInitialStyleWeight(item.ShapeName);
                        _blendShapeManager.SetShapeWeight(item.KeyIndex, weight);
                        sliderFloatField.SetValueWithoutNotify(weight);
                    }
                    else
                    {
                        _blendShapeManager.UnoverrideShape(item.KeyIndex);
                        RemoveByKeyIndex(item.KeyIndex);
                    }
                }
            };
            
            return element;
        }

        void BindElement(VisualElement element, int index, bool isStyle)
        {
            var item = isStyle ? _currentStyleSource[index] : _currentSelectedSource[index];
            element.userData = item;
             
            var nameLabel = element.Q<Label>("name");
            var sliderFloatField = element.Q<SliderFloatField>("slider-float-field");
            var actionButton = element.Q<Button>("action");
            
            nameLabel.text = item.ShapeName;
            var currentWeight = _blendShapeManager.GetShapeWeight(item.KeyIndex);
            sliderFloatField.SetValueWithoutNotify(currentWeight);
            actionButton.text = isStyle ? "R" : "R";
            UpdateActionButton(item, isStyle, actionButton);
        }

        void UpdateActionButton(ElementData item, bool isStyle, Button actionButton)
        {
            if (isStyle)
            {
                actionButton.SetEnabled(!_blendShapeManager.IsInitialStyleWeight(item.KeyIndex));
            }
        }
    }

    private void BuildCurrentSource()
    {
        using var _ = new ProfilingSampleScope("SelectedPanel.BuildCurrentSource");
        _currentStyleSource.Clear();
        _currentSelectedSource.Clear();

        var searchText = _searchField.value?.ToLower() ?? "";
        var hasSearchText = !string.IsNullOrEmpty(searchText);

        var allSourceCount = _allSource.Count;
        for (int i = 0; i < allSourceCount; i++)
        {
            var item = _allSource[i];

            if (hasSearchText && !item.ShapeName.ToLower().Contains(searchText))
                continue;

            if (!_groupManager.IsBlendShapeVisible(item.KeyIndex))
                continue;

            if (item.IsStyle)
            {
                _currentStyleSource.Add(item);
            }
            else
            {
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
            item.IsStyle ? _currentStyleSource : _currentSelectedSource,
            item,
            item.IsStyle ? _styleListView : _selectedListView
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
        
        // スタイルリストからは削除しない
        return false;
    }

    private void BuildAndRefreshListViewsSlow()
    {
        BuildCurrentSource();
        _styleListView.RefreshItems();
        _selectedListView.RefreshItems();
    }
}
