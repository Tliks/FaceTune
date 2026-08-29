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
    private bool _zeroControlsRefreshPending;
    private readonly Dictionary<int, double> _flashExpiryByKeyIndex = new();
    private IVisualElementScheduledItem? _flashCleanupSchedule;
    
    private readonly record struct ElementData(
        string ShapeName,
        int KeyIndex,
        bool IsFacial,
        bool IsBase);

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
        _blendShapeManager.OnSingleShapeAdded += (keyIndex) =>
        {
            RebuildListViewsSlow();
            FlashOverrides(new[] { keyIndex });
        };
        _blendShapeManager.OnMultipleShapesAdded += (keyIndices) =>
        {
            RebuildListViewsSlow();
            FlashOverrides(keyIndices);
        };
        _blendShapeManager.OnSingleShapeRemoved += (keyIndex) => RebuildListViewsSlow();
        _blendShapeManager.OnMultipleShapesRemoved += (keyIndices) => RebuildListViewsSlow();
        // _blendShapeManager.OnSingleShapeWeightChanged += (keyIndex) => RebuildListViewsSlow();
        _blendShapeManager.OnMultipleShapeWeightChanged += (keyIndices) => RebuildListViewsSlow();
        _blendShapeManager.OnUnknownChange += () => RebuildListViewsSlow();
        _blendShapeManager.OnAnyDataChange += RequestZeroControlsVisibilityUpdate;
    }

    private bool _selectedZero = true;
    private void SetupControls()
    {
        _searchField = _element.Q<TextField>("search-field");
        _searchField.RegisterValueChangedCallback(_ => RebuildListViewsSlow());
        if (_searchField is PlaceholderTextField placeholderSearchField)
            placeholderSearchField.Placeholder = "facialEditor.search.placeholder".LS();

        _control = _element.Q("selected-shapes-controls");

        _styleToggle = _control.Q<SimpleToggle>("style-toggle");
        _styleToggle.SetValueWithoutNotify(false);
        _styleToggle.RegisterValueChangedCallback(_ => RebuildListViewsSlow());

        _baseToggle = _control.Q<SimpleToggle>("base-toggle");
        _baseToggle.SetValueWithoutNotify(true);
        _baseToggle.RegisterValueChangedCallback(_ => RebuildListViewsSlow());

        _zeroToggle = _control.Q<SimpleToggle>("zero-toggle");
        _zeroToggle.RegisterValueChangedCallback(evt => RebuildListViewsSlow());

        _selectedRemoveAll0Button = _control.Q<Button>("selected-remove-all-0-button");
        _selectedRemoveAll0Button.clicked += () =>
        {
            var indices = _currentSource
                .Where(item => IsExplicitZeroTarget(item.KeyIndex))
                .Select(item => item.KeyIndex);
            _blendShapeManager.RemoveShapes(indices);
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
                .Where(index => _blendShapeManager.IsInTarget(index));
            _blendShapeManager.RemoveShapes(indices);
        };
    }

    private void SetupListViews()
    {
        _selectedListView = _element.Q<ListView>("selected-list-view");
        _selectedListView.focusable = true;
        _selectedListView.fixedItemHeight = FacialShapeUI.ListItemHeight;
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
                }
            };
                        
            actionButton.clicked += () =>
            {
                if (element.userData is ElementData item)
                {
                    _blendShapeManager.RemoveShape(item.KeyIndex);
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
            actionButton.SetEnabled(_blendShapeManager.IsInTarget(item.KeyIndex));
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
            using var _ = new Utils.ProfilingSampleScope("SelectedPanel.FlashOverrides.Cleanup");
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
        var allSource = new List<ElementData>();
        var allKeys = _blendShapeManager.AllKeys;
        for (int i = 0; i < allKeys.Count; i++)
        {
            allSource.Add(new ElementData(
                allKeys[i],
                i,
                _blendShapeManager.IsFacialShape(i),
                _blendShapeManager.IsBaseShape(i)));
        }
        _allSource = allSource.AsReadOnly();
        _currentSource = new();
        BuildCurrentSource();
        UpdateSourceToggleVisibility();

        _selectedListView.itemsSource = _currentSource;

        _selectedListView.RefreshItems();
    }

    private void UpdateSourceToggleVisibility()
    {
        _styleToggle.SetVisible(_blendShapeManager.FacialSet.Count > 0);
        _baseToggle.SetVisible(_blendShapeManager.BaseSet.Count > 0);
        UpdateZeroControlsVisibility();
    }

    private void RequestZeroControlsVisibilityUpdate()
    {
        if (_zeroControlsRefreshPending) return;

        _zeroControlsRefreshPending = true;
        _element.schedule.Execute(() =>
        {
            _zeroControlsRefreshPending = false;
            UpdateZeroControlsVisibility();
        });
    }

    private void UpdateZeroControlsVisibility()
    {
        if (_allSource == null) return;

        var hasVisibleZero = EnumerateCurrentSource(applyZeroFilter: false)
            .Any(item => Mathf.Approximately(
                _blendShapeManager.GetEffectiveShapeWeight(item.KeyIndex),
                0f));
        _zeroToggle.SetVisible(hasVisibleZero);

        var hasExplicitZeroTarget = _currentSource.Any(item => IsExplicitZeroTarget(item.KeyIndex));
        _selectedRemoveAll0Button.SetVisible(hasExplicitZeroTarget);
    }

    private bool IsExplicitZeroTarget(int index)
        => _blendShapeManager.IsInTarget(index)
           && Mathf.Approximately(_blendShapeManager.GetShapeWeight(index), 0f);

    private void BuildCurrentSource()
    {
        using var _ = new Utils.ProfilingSampleScope("SelectedPanel.BuildCurrentSource");

        _currentSource.Clear();
        _currentSource.AddRange(EnumerateCurrentSource(applyZeroFilter: true));
    }

    private IEnumerable<ElementData> EnumerateCurrentSource(bool applyZeroFilter)
    {
        var searchText = _searchField.value ?? string.Empty;
        var hasSearchText = searchText.Length > 0;

        var allSourceCount = _allSource.Count;
        for (int i = 0; i < allSourceCount; i++)
        {
            var item = _allSource[i];

            if (hasSearchText && item.ShapeName.IndexOf(
                    searchText,
                    StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (_groupManager.IsLeftSelected && !_groupManager.IsBlendShapeVisible(item.KeyIndex))
                continue;

            var isInTarget = _blendShapeManager.IsInTarget(item.KeyIndex);
            var isVisibleSource = _styleToggle.value && item.IsFacial
                || _baseToggle.value && item.IsBase;
            if (!isVisibleSource && !isInTarget)
                continue;

            if (applyZeroFilter
                && !_zeroToggle.value
                && _blendShapeManager.GetEffectiveShapeWeight(item.KeyIndex) == 0f)
                continue;

            yield return item;
        }
    }


    private void RebuildListViewsSlow()
    {
        BuildCurrentSource();
        UpdateZeroControlsVisibility();
        _selectedListView.RefreshItems();
    }
}
