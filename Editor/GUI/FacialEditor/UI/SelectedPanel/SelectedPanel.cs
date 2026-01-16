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
    private SimpleToggle _zeroToggle = null!;

    private VisualElement _baseShapesPanel = null!;
    private VisualElement _selectedShapesPanel = null!;

    private ListView _baseListView = null!;
    private ListView _selectedListView = null!;
    
    private Button _selectedRemoveAll0Button = null!;
    private readonly Dictionary<int, double> _flashExpiryByKeyIndex = new();
    private IVisualElementScheduledItem? _flashCleanupSchedule;
    
    private struct ElementData
    {
        public string ShapeName;
        public int KeyIndex;
        public bool IsBase;
    }

    private IReadOnlyList<ElementData> _allSource = null!;
    private List<ElementData> _currentBaseSource = null!;
    private List<ElementData> _currentSelectedSource = null!;

    private static readonly Texture _toggleIcon = EditorGUIUtility.IconContent("d_preAudioLoopOff").image;
    private static readonly Texture _resetIcon = EditorGUIUtility.IconContent("d_Toolbar Minus").image;
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
        _groupManager.OnGroupSelectionChanged += (groups) => BuildAndRefreshListViewsSlow();
        _groupManager.OnLeftSelectionChanged += (isLeftSelected) => BuildAndRefreshListViewsSlow();
        _blendShapeManager.OnSingleShapeOverride += (keyIndex) =>
        {
            AddByKeyIndex(keyIndex);
            FlashOverrides(new[] { keyIndex });
        };
        _blendShapeManager.OnMultipleShapeOverride += (keyIndices) =>
        {
            BuildAndRefreshListViewsSlow();
            FlashOverrides(keyIndices);
        };
        _blendShapeManager.OnSingleShapeUnoverride += (keyIndex) => RemoveByKeyIndex(keyIndex);
        _blendShapeManager.OnMultipleShapeUnoverride += (keyIndices) => BuildAndRefreshListViewsSlow();
        // _blendShapeManager.OnSingleShapeWeightChanged += (keyIndex) => BuildAndRefreshListViewsSlow();
        _blendShapeManager.OnMultipleShapeWeightChanged += (keyIndices) => BuildAndRefreshListViewsSlow();
        _blendShapeManager.OnUnknownChange += () => BuildAndRefreshListViewsSlow();
        _blendShapeManager.OnBaseSetChange += () => { RefreshTarget(); UpdateBaseListVisibility(); };
        _blendShapeManager.OnAnyDataChange += () => UpdateSelectedRemoveAll0ButtonVisibility();
    }

    private bool _baseZero = true;
    private bool _selectedZero = true;
    private void SetupControls()
    {
        var commonControls = _element.Q<VisualElement>("common-controls-panel");
        _searchField = commonControls.Q<TextField>("search-field");
        _searchField.RegisterValueChangedCallback(_ => BuildAndRefreshListViewsSlow());

        _zeroToggle = commonControls.Q<SimpleToggle>("zero-toggle");
        _zeroToggle.RegisterValueChangedCallback(evt => BuildAndRefreshListViewsSlow());

        _baseShapesPanel = _element.Q("base-shapes-panel");

        var base0100Toggle = _baseShapesPanel.Q<Button>("base-0-100-toggle");
        base0100Toggle.Add(new Image { image = _toggleIcon });
        base0100Toggle.clicked += () =>
        {
            var indices = _currentBaseSource.Select(item => item.KeyIndex);
            _blendShapeManager.SetShapesWeight(indices, _baseZero ? 100f : 0f);
            _baseZero = !_baseZero;
        };
        
        var baseResetAllButton = _baseShapesPanel.Q<Button>("base-reset-all-button");
        baseResetAllButton.Add(new Image { image = _resetIcon });
        baseResetAllButton.clicked += () =>
        {
            _blendShapeManager.ResetShapesWeight(_currentBaseSource.Select(item => item.KeyIndex));
        };

        _selectedShapesPanel = _element.Q("selected-shapes-panel");

        _selectedRemoveAll0Button = _selectedShapesPanel.Q<Button>("selected-remove-all-0-button");
        UpdateSelectedRemoveAll0ButtonVisibility();
        _selectedRemoveAll0Button.clicked += () =>
        {
            // 現在表示しているものに限らず全ブレンドシェイプから0値を削除
            var indices = _blendShapeManager.GetOverridenIndices(index => !_blendShapeManager.IsBaseShape(index) && _blendShapeManager.GetShapeWeight(index) == 0f); 
            _blendShapeManager.UnoverrideShapes(indices);
        };

        var selected0100Toggle = _selectedShapesPanel.Q<Button>("selected-0-100-toggle");
        selected0100Toggle.Add(new Image { image = _toggleIcon });
        selected0100Toggle.clicked += () =>
        {
            var indices = _currentSelectedSource.Select(item => item.KeyIndex);
            _blendShapeManager.SetShapesWeight(indices, _selectedZero ? 100f : 0f);
            _selectedZero = !_selectedZero;
        };

        var removeAllButton = _selectedShapesPanel.Q<Button>("remove-all-button");
        removeAllButton.Add(new Image { image = _removeIcon });
        removeAllButton.clicked += () =>
        {
            var indices = _currentSelectedSource.Select(item => item.KeyIndex);
            _blendShapeManager.UnoverrideShapes(indices);
        };
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

    private void FlashOverrides(IEnumerable<int> keyIndices)
    {
        var now = EditorApplication.timeSinceStartup;
        const double fadeInSeconds = 0.5;
        var expiry = now + fadeInSeconds;

        foreach (var keyIndex in keyIndices)
        {
            _flashExpiryByKeyIndex[keyIndex] = expiry;
        }

        _baseListView.RefreshItems();
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
                _baseListView.RefreshItems();
                _selectedListView.RefreshItems();
            }

            if (_flashExpiryByKeyIndex.Count == 0)
            {
                _flashCleanupSchedule?.Pause();
            }
        }).Every(100);

        _flashCleanupSchedule.Resume();
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

    private void UpdateSelectedRemoveAll0ButtonVisibility()
    {
        var anyZero = _blendShapeManager.GetOverridenIndices(index => !_blendShapeManager.IsBaseShape(index) && _blendShapeManager.GetShapeWeight(index) == 0f).Any();
        _selectedRemoveAll0Button.SetVisible(anyZero);
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
                if (!_zeroToggle.value && _blendShapeManager.GetShapeWeight(item.KeyIndex) == 0f)
                    continue;

                _currentBaseSource.Add(item);
            }
            else if (selected)
            {
                if (!_zeroToggle.value && _blendShapeManager.GetShapeWeight(item.KeyIndex) == 0f)
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
