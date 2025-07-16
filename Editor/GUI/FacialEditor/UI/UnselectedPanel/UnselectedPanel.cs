using UnityEngine.UIElements;

namespace aoyon.facetune.ui.shapes_editor;

internal class UnselectedPanel
{
    private readonly BlendShapeOverrideManager _blendShapeManager;
    private readonly BlendShapeGrouping _groupManager;

    private readonly VisualElement _element;
    public VisualElement Element => _element;

    private static VisualTreeAsset _uxml = null!;
    private static VisualTreeAsset _unselectedItemUxml = null!;
    private static StyleSheet _uss = null!;

    private TextField _unselectedSearchField = null!;
    private FloatField _addWeightField = null!;
    private ListView _unselectedListView = null!;
    
    private int _currentHoveredIndex = -1;
    public int CurrentHoveredIndex
    {
        get => _currentHoveredIndex;
        set
        {
            if (_currentHoveredIndex != value)
            {
                _currentHoveredIndex = value;
                OnHoveredIndexChanged?.Invoke(value);
            }
        }
    }
    public event Action<int>? OnHoveredIndexChanged;
    
    private struct ListViewItem
    {
        public string ShapeName;
        public int KeyIndex;
    }
    
    private IReadOnlyList<ListViewItem> _allSource = null!;
    private List<ListViewItem> _currentSource = null!;
    
    public UnselectedPanel(BlendShapeOverrideManager blendShapeManager, BlendShapeGrouping groupManager)
    {
        _blendShapeManager = blendShapeManager;
        _groupManager = groupManager;
        
        EnsureAssets();
        
        _element = _uxml.CloneTree();
        _element.styleSheets.Add(_uss);
        
        SetupControls();
        SetupListView();
        _groupManager.OnGroupSelectionChanged += (groups) => BuildAndRefreshListViewSlow();
        _blendShapeManager.OnSingleShapeUnoverride += (keyIndex) => AddByKeyIndex(keyIndex);
        _blendShapeManager.OnMultipleShapeUnoverride += (keyIndices) => BuildAndRefreshListViewSlow();
        _blendShapeManager.OnUnknownChange += () => BuildAndRefreshListViewSlow();
    }

    private void EnsureAssets()
    {
        UIUtility.EnsureUxmlWithGuid(ref _uxml, "736ebf000f485f041ac2becabbde48d3");
        UIUtility.EnsureUxmlWithGuid(ref _unselectedItemUxml, "3efe7e91dce1d544b873dd133a44039d");
        UIUtility.EnsureUssWithGuid(ref _uss, "b9dfe6425f70d0544a5939a176bdf3b0");
    }

    private void SetupControls()
    {
        _unselectedSearchField = _element.Q<TextField>("unselected-search-field");
        _unselectedSearchField.RegisterValueChangedCallback(_ => BuildAndRefreshListViewSlow());
        
        _addWeightField = _element.Q<FloatField>("add-weight-field");
        _element.Q<Button>("add-all-button").clicked += () =>
        {
            _blendShapeManager.OverrideShapesAndSetWeight(_currentSource.Select(item => item.KeyIndex), _addWeightField.value);
            BuildAndRefreshListViewSlow();
        };
    }

    private void SetupListView()
    {
        _unselectedListView = _element.Q<ListView>("unselected-list-view");

        var allSource = new List<ListViewItem>();
        var allKeys = _blendShapeManager.AllKeys;
        for (int i = 0; i < allKeys.Count; i++)
        {
            allSource.Add(new ListViewItem { ShapeName = allKeys[i], KeyIndex = i });
        }
        _allSource = allSource.AsReadOnly();
        _currentSource = new();
        BuildCurrentSource();

        _unselectedListView.itemsSource = _currentSource;
        
        _unselectedListView.makeItem = MakeUnselectedElement;
        _unselectedListView.bindItem = (e, i) => BindUnselectedElement(e, i);
        
        _unselectedListView.RegisterCallback<MouseLeaveEvent>(evt =>
        {
            CurrentHoveredIndex = -1;
        });

        VisualElement MakeUnselectedElement()
        {
            var element = _unselectedItemUxml.CloneTree();
            element.RegisterCallback<ClickEvent>(evt =>
            {
                if (element.userData is ListViewItem data)
                {
                    _blendShapeManager.OverrideShapeAndSetWeight(data.KeyIndex, _addWeightField.value);
                    RemoveByKeyIndex(data.KeyIndex);
                }
            });
            
            element.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (element.userData is ListViewItem data)
                {
                    CurrentHoveredIndex = data.KeyIndex;
                }
            });            
            return element;
        }

        void BindUnselectedElement(VisualElement element, int index)
        {
            var item = _currentSource[index];
            element.userData = item;
            element.Q<Label>("name").text = item.ShapeName;
        }
    }

    private void BuildCurrentSource()
    {
        using var _ = new ProfilingSampleScope("UnselectedPanel.BuildCurrentSource");
        _currentSource.Clear();
        
        var searchText = _unselectedSearchField.value?.ToLower() ?? "";
        var hasSearchText = !string.IsNullOrEmpty(searchText);
        
        var allSourceCount = _allSource.Count;
        for (int i = 0; i < allSourceCount; i++)
        {
            var item = _allSource[i];

            if (_blendShapeManager.IsStyleShape(item.KeyIndex))
                continue;
            
            if (_blendShapeManager.IsOverridden(item.KeyIndex))
                continue;
                
            if (hasSearchText && !item.ShapeName.ToLower().Contains(searchText))
                continue;
                
            if (!_groupManager.IsBlendShapeVisible(item.KeyIndex))
                continue;
                
            _currentSource.Add(item);
        }
    }

    // ソートされているので二分探索をしても良い
    public bool AddByKeyIndex(int keyIndex)
    {
        using var _ = new ProfilingSampleScope("UnselectedPanel.AddByKeyIndex");
        var item = _allSource[keyIndex];
        
        for (int i = 0; i < _currentSource.Count; i++)
        {
            if (_currentSource[i].KeyIndex == keyIndex)
                return false; // 既に存在する
                
            if (_currentSource[i].KeyIndex > keyIndex)
            {
                _currentSource.Insert(i, item);
                _unselectedListView.RefreshItems();
                return true;
            }
        }
        
        // 末尾に追加
        _currentSource.Add(item);
        _unselectedListView.RefreshItems();
        return true;
    }
    
    public bool RemoveByKeyIndex(int keyIndex)
    {
        using var _ = new ProfilingSampleScope("UnselectedPanel.RemoveByKeyIndex");
        for (int i = 0; i < _currentSource.Count; i++)
        {
            if (_currentSource[i].KeyIndex == keyIndex)
            {
                _currentSource.RemoveAt(i);
                _unselectedListView.RefreshItems();
                return true;
            }
        }
        return false;
    }

    // BuildCurrentSourceを呼んでいて重いので全体更新をしたい場合に呼ぶ
    private void BuildAndRefreshListViewSlow()
    {
        BuildCurrentSource();
        _unselectedListView.RefreshItems();
    }
}
