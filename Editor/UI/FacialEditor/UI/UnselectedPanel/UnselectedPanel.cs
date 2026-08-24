using UnityEngine.UIElements;
using Aoyon.FaceTune.Gui.Components;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal class UnselectedPanel
{
    private readonly BlendShapeOverrideManager _blendShapeManager;
    private readonly BlendShapeGrouping _groupManager;
    private readonly PreviewManager _previewManager;

    private readonly VisualElement _element;
    public VisualElement Element => _element;

    private static VisualTreeAsset? _uxml;
    private static VisualTreeAsset? _unselectedItemUxml;
    private static StyleSheet? _uss;

    private TextField _unselectedSearchField = null!;
    private ListView _unselectedListView = null!;
    
    private static readonly Texture _selectAllIcon = EditorGUIUtility.IconContent("d_Toolbar Plus").image;
    
    private struct ListViewItem
    {
        public string ShapeName;
        public int KeyIndex;
    }
    
    private IReadOnlyList<ListViewItem> _allSource = null!;
    private List<ListViewItem> _currentSource = null!;
    
    public UnselectedPanel(BlendShapeOverrideManager blendShapeManager, BlendShapeGrouping groupManager, PreviewManager previewManager)
    {
        _blendShapeManager = blendShapeManager;
        _groupManager = groupManager;
        _previewManager = previewManager;

        var uxml = UIAssetHelper.EnsureUxmlWithGuid(ref _uxml, "736ebf000f485f041ac2becabbde48d3");
        var unselectedItemUxml = UIAssetHelper.EnsureUxmlWithGuid(ref _unselectedItemUxml, "3efe7e91dce1d544b873dd133a44039d");
        var uss = UIAssetHelper.EnsureUssWithGuid(ref _uss, "b9dfe6425f70d0544a5939a176bdf3b0");
        
        _element = uxml.CloneTree();
        _element.styleSheets.Add(uss);
        Localization.LocalizeUIElements(_element);
                
        SetupControls();
        SetupListView();
        _groupManager.OnGroupSelectionChanged += (groups) => RebuildListViewSlow();
        _groupManager.OnRightSelectionChanged += (isRightSelected) => RebuildListViewSlow();
        _blendShapeManager.OnSingleShapeAdded += (keyIndex) => RedrawItemByKeyIndex(keyIndex);
        _blendShapeManager.OnMultipleShapesAdded += (keyIndices) => RebuildListViewSlow();
        _blendShapeManager.OnSingleShapeRemoved += (keyIndex) => RedrawItemByKeyIndex(keyIndex);
        _blendShapeManager.OnMultipleShapesRemoved += (keyIndices) => RebuildListViewSlow();
        _blendShapeManager.OnUnknownChange += () => RebuildListViewSlow();
    }

    private void SetupControls()
    {
        _unselectedSearchField = _element.Q<TextField>("unselected-search-field");
        _unselectedSearchField.RegisterValueChangedCallback(_ => RebuildListViewSlow());
        if (_unselectedSearchField is PlaceholderTextField placeholderSearchField)
            placeholderSearchField.Placeholder = "facialEditor.search.placeholder".LS();
        
        var selectAllButton = _element.Q<Button>("select-all-button");
        selectAllButton.Add(new Image { image = _selectAllIcon });
        selectAllButton.clicked += () =>
        {
            _blendShapeManager.AddShapesWithWeight(_currentSource
                .Where(item => !_blendShapeManager.IsInTarget(item.KeyIndex))
                .Select(item => (
                    item.KeyIndex,
                    _blendShapeManager.GetEffectiveShapeWeight(item.KeyIndex))));
            RebuildListViewSlow();
        };
    }

    private void SetupListView()
    {
        _unselectedListView = _element.Q<ListView>("unselected-list-view");
        _unselectedListView.focusable = false;
        _unselectedListView.fixedItemHeight = FacialShapeUI.ListItemHeight;
        _unselectedListView.selectionType = SelectionType.None;
        _unselectedListView.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;

        InitializeListSource();

        _unselectedListView.makeItem = MakeUnselectedElement;
        _unselectedListView.bindItem = (e, i) => BindUnselectedElement(e, i);
        
        _unselectedListView.RegisterCallback<MouseLeaveEvent>(evt =>
        {
            _previewManager.CurrentHoveredIndex = -1;
        });

        VisualElement MakeUnselectedElement()
        {
            var element = _unselectedItemUxml!.CloneTree();
            Localization.LocalizeUIElements(element);
            
            element.RegisterCallback<ClickEvent>(evt =>
            {
                if (element.userData is ListViewItem data)
                {
                    _blendShapeManager.AddShapeWithWeight(
                        data.KeyIndex,
                        _blendShapeManager.GetEffectiveShapeWeight(data.KeyIndex));
                }
            });
            
            element.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (element.userData is ListViewItem data)
                {
                    _previewManager.CurrentHoveredIndex = data.KeyIndex;
                }
            });            
            return element;
        }

        void BindUnselectedElement(VisualElement element, int index)
        {
            var item = _currentSource[index];
            element.userData = item;
            element.Q<Label>("name").text = item.ShapeName;
            element.SetEnabled(!_blendShapeManager.IsInTarget(item.KeyIndex));
        }
    }   

    private void InitializeListSource()
    {
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
        _unselectedListView.RefreshItems();
    }

    private void BuildCurrentSource()
    {
        using var _ = new Utils.ProfilingSampleScope("UnselectedPanel.BuildCurrentSource");
        _currentSource.Clear();
        
        var searchText = _unselectedSearchField.value ?? string.Empty;
        var hasSearchText = searchText.Length > 0;
        
        var allSourceCount = _allSource.Count;
        for (int i = 0; i < allSourceCount; i++)
        {
            var item = _allSource[i];

            if (hasSearchText && item.ShapeName.IndexOf(
                    searchText,
                    StringComparison.OrdinalIgnoreCase) < 0)
                continue;
                
            if (_groupManager.IsRightSelected && !_groupManager.IsBlendShapeVisible(item.KeyIndex))
                continue;
                
            _currentSource.Add(item);
        }
    }

    public void RedrawItemByKeyIndex(int keyIndex)
    {
        int idx = FindListIndexByKeyIndex(keyIndex);
        if (idx >= 0)
        {
            _unselectedListView.RefreshItem(idx);
        }
    }

    // BuildCurrentSourceを呼んでいて重いので全体更新をしたい場合に呼ぶ
    private void RebuildListViewSlow()
    {
        BuildCurrentSource();
        _unselectedListView.RefreshItems();
    }

    private int FindListIndexByKeyIndex(int targetKeyIndex)
    {
        if (_currentSource == null || _currentSource.Count == 0) return -1;

        int lo = 0, hi = _currentSource.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            int key = _currentSource[mid].KeyIndex;
            if (key == targetKeyIndex) return mid;
            if (key < targetKeyIndex) lo = mid + 1;
            else hi = mid - 1;
        }

        return -1;
    }

    public bool ScrollToNearestKeyIndex(int targetKeyIndex, bool select = false, bool notify = false)
    {
        int idx = FindListIndexByKeyIndex(targetKeyIndex);
        if (idx < 0) return false;

        if (select)
        {
            if (notify) _unselectedListView.SetSelection(new[] { idx });
            else _unselectedListView.SetSelectionWithoutNotify(new[] { idx });
        }
        _unselectedListView.ScrollToItem(idx);
        return true;
    }
}
