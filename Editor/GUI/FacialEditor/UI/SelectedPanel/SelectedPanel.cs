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
    private VisualElement _styleShapesPanel = null!;
    private VisualElement _selectedShapesPanel = null!;
    private ListView _styleListView = null!;
    private ListView _selectedListView = null!;
    private SimpleToggle _styleZeroToggle = null!;
    private SimpleToggle _selectedZeroToggle = null!;
    
    private struct ElementData
    {
        public string ShapeName;
        public int KeyIndex;
        public bool IsStyle;
    }

    private IReadOnlyList<ElementData> _allSource = null!;
    private List<ElementData> _currentStyleSource = null!;
    private List<ElementData> _currentSelectedSource = null!;

    private static readonly Texture _toggleIcon = EditorGUIUtility.IconContent("d_preAudioLoopOff@2x").image;
    private static readonly Texture _resetIcon = EditorGUIUtility.IconContent("d_Toolbar Minus@2x").image;
    private static readonly Texture _removeIcon = EditorGUIUtility.IconContent("d_Toolbar Minus@2x").image;

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
        _blendShapeManager.OnSingleShapeOverride += (keyIndex) => AddByKeyIndex(keyIndex);
        _blendShapeManager.OnMultipleShapeOverride += (keyIndices) => BuildAndRefreshListViewsSlow();
        _blendShapeManager.OnSingleShapeUnoverride += (keyIndex) => RemoveByKeyIndex(keyIndex);
        _blendShapeManager.OnMultipleShapeUnoverride += (keyIndices) => BuildAndRefreshListViewsSlow();
        // _blendShapeManager.OnSingleShapeWeightChanged += (keyIndex) => BuildAndRefreshListViewsSlow();
        _blendShapeManager.OnMultipleShapeWeightChanged += (keyIndices) => BuildAndRefreshListViewsSlow();
        _blendShapeManager.OnUnknownChange += () => BuildAndRefreshListViewsSlow();
        _blendShapeManager.OnStyleSetChange += () => { RefreshTarget(); UpdateStyleVisibility(); };
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

        _styleShapesPanel = _element.Q("style-shapes-panel");

        _styleShapesPanel.Q<Button>("style-set-all-100-button").clicked += () =>
        {
            var indices = _currentStyleSource.Select(item => item.KeyIndex);
            _blendShapeManager.SetShapesWeight(indices, 100f);
        };
        _styleShapesPanel.Q<Button>("style-set-all-0-button").clicked += () =>
        {
            var indices = _currentStyleSource.Select(item => item.KeyIndex);
            _blendShapeManager.SetShapesWeight(indices, 0f);
        };
        _styleShapesPanel.Q<Button>("style-reset-all-button").clicked += () =>
        {
            _blendShapeManager.ResetShapesWeight(_currentStyleSource.Select(item => item.KeyIndex));
        };
        _styleZeroToggle = _styleShapesPanel.Q<SimpleToggle>("style-zero-toggle");
        _styleZeroToggle.RegisterValueChangedCallback(evt =>
        {
            BuildAndRefreshStyleListViewsSlow();
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
        _styleListView = _element.Q<ListView>("style-list-view");
        _selectedListView = _element.Q<ListView>("selected-list-view");
        _styleListView.focusable = true;
        _selectedListView.focusable = true;
        _styleListView.selectionType = SelectionType.None;
        _selectedListView.selectionType = SelectionType.None;
        _styleListView.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
        _selectedListView.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;

        RefreshTarget();

        _styleListView.makeItem = () => MakeElement(true);
        _styleListView.bindItem = (e, i) => BindElement(e, i, true);

        _selectedListView.makeItem = () => MakeElement(false);
        _selectedListView.bindItem = (e, i) => BindElement(e, i, false);

        VisualElement MakeElement(bool isStyle)
        {
            var element = _itemUxml.CloneTree();
            
            var sliderFloatField = element.Q<SliderFloatField>("slider-float-field");
            var toggleButton = element.Q<Button>("toggle-button");
            var actionButton = element.Q<Button>("action");

            toggleButton.text = "";
            toggleButton.Add(new Image { image = _toggleIcon });
            actionButton.text = "";
            actionButton.Add(new Image { image = isStyle ? _resetIcon : _removeIcon });
            
            sliderFloatField.RegisterValueChangedCallback(evt =>
            {
                if (element.userData is ElementData item)
                {
                    _blendShapeManager.SetShapeWeight(item.KeyIndex, evt.newValue);
                    UpdateActionButton(item, isStyle, actionButton);
                }
            });
            _blendShapeManager.OnSingleShapeWeightChanged += (keyIndex) =>
            {
                if (element.userData is ElementData item && item.KeyIndex == keyIndex)
                {
                    sliderFloatField.SetValueWithoutNotify(_blendShapeManager.GetShapeWeight(keyIndex));
                    UpdateActionButton(item, isStyle, actionButton);
                }
            };
            
            toggleButton.clicked += () =>
            {
                if (element.userData is ElementData item)
                {
                    var currentWeight = _blendShapeManager.GetShapeWeight(item.KeyIndex);
                    var newWeight = currentWeight == 0f ? 100f : 0f;
                    _blendShapeManager.SetShapeWeight(item.KeyIndex, newWeight);
                    sliderFloatField.SetValueWithoutNotify(newWeight);
                    UpdateActionButton(item, isStyle, actionButton);
                }
            };
                        
            actionButton.clicked += () =>
            {
                if (element.userData is ElementData item)
                {
                    if (isStyle) // reset
                    {
                        var weight = _blendShapeManager.ResetShapeWeight(item.KeyIndex);
                        sliderFloatField.SetValueWithoutNotify(weight);
                    }
                    else // remove
                    {
                        _blendShapeManager.UnoverrideShape(item.KeyIndex);
                        RemoveByKeyIndex(item.KeyIndex);
                    }
                    UpdateActionButton(item, isStyle, actionButton);
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

    public void RefreshTarget()
    {
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

        _styleListView.RefreshItems();
        _selectedListView.RefreshItems();
    }

    private void UpdateStyleVisibility()
    {
        var hasStyleShapes = _currentStyleSource.Count > 0;
        _styleShapesPanel.style.display = hasStyleShapes ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void BuildCurrentSource(bool style = true, bool selected = true)
    {
        using var _ = new ProfilingSampleScope("SelectedPanel.BuildCurrentSource");

        if (style)
            _currentStyleSource.Clear();
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

            if (!_groupManager.IsBlendShapeVisible(item.KeyIndex))
                continue;

            if (style && item.IsStyle)
            {
                if (!_styleZeroToggle.value && _blendShapeManager.GetShapeWeight(item.KeyIndex) == 0f)
                    continue;

                _currentStyleSource.Add(item);
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

    private void BuildAndRefreshStyleListViewsSlow()
    {
        BuildCurrentSource(style: true, selected: false);
        _styleListView.RefreshItems();
    }

    private void BuildAndRefreshSelectedListViewsSlow()
    {
        BuildCurrentSource(style: false, selected: true);
        _selectedListView.RefreshItems();
    }

    private void BuildAndRefreshListViewsSlow()
    {
        BuildCurrentSource();
        _styleListView.RefreshItems();
        _selectedListView.RefreshItems();
    }
}
