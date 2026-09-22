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
    private static StyleSheet? _uss;

    private TextField _searchField = null!;
    private SimpleToggle _styleToggle = null!;

    private VisualElement _control = null!;

    private ListView _selectedListView = null!;
    
    private Button _selectedRemoveAll0Button = null!;
    private bool _controlsRefreshPending;
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
        var uss = UIAssetHelper.EnsureUssWithGuid(ref _uss, "1adda987d131ce34c8d57981b20ac1f8");
        
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
        _blendShapeManager.OnAnyDataChange += RequestControlsVisibilityUpdate;
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
            var element = SelectedShapeRowUI.Create();

            var flashOverlay = new VisualElement { name = "flash-overlay", pickingMode = PickingMode.Ignore };
            flashOverlay.AddToClassList("flash-overlay");
            element.Insert(0, flashOverlay);

            var metadataGutter = element.Q<VisualElement>("metadata-gutter");
            var changedMarker = element.Q<VisualElement>("changed-marker");
            var facialRail = element.Q<VisualElement>("facial-rail");
            var nameLabel = element.Q<Label>("name");
            var warningIcon = element.Q<Image>("validation-warning");
            var sliderFloatField = element.Q<SliderFloatField>("slider-float-field");
            var curveField = element.Q<IMGUIContainer>("curve-field");
            var curveToggle = element.Q<Button>("curve-toggle");
            var toggleButton = element.Q<Button>("toggle-button");
            var actionButton = element.Q<Button>("action");

            metadataGutter.RegisterCallback<ClickEvent>(evt =>
            {
                if (element.userData is not ElementData item
                    || !_blendShapeManager.IsShapeChangedFromInitialState(item.KeyIndex))
                    return;

                _blendShapeManager.TryRestoreShapeToInitialState(item.KeyIndex);
                evt.StopPropagation();
            });

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
                    UpdateRowPresentation(item, changedMarker, facialRail, nameLabel);
                    UpdateActionButton(item, actionButton);
                }
            };
             
            curveToggle.clicked += () =>
            {
                if (element.userData is ElementData item)
                {
                    _blendShapeManager.ToggleCurveMode(item.KeyIndex);
                }
            };

            curveField.onGUIHandler = () =>
            {
                if (element.userData is not ElementData item) return;
                if (!_blendShapeManager.IsCurveMode(item.KeyIndex)) return;

                var curveProperty = _blendShapeManager.GetCurveProperty(item.KeyIndex);
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(
                    new Rect(0f, 0f, curveField.worldBound.width, curveField.worldBound.height),
                    curveProperty,
                    GUIContent.none);
                if (EditorGUI.EndChangeCheck())
                {
                    _blendShapeManager.CommitCurveEdit(item.KeyIndex, curveProperty.animationCurveValue);
                }
            };

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
             
            var changedMarker = element.Q<VisualElement>("changed-marker");
            var facialRail = element.Q<VisualElement>("facial-rail");
            var nameLabel = element.Q<Label>("name");
            var warningIcon = element.Q<Image>("validation-warning");
            var sliderFloatField = element.Q<SliderFloatField>("slider-float-field");
            var curveField = element.Q<IMGUIContainer>("curve-field");
            var curveToggle = element.Q<Button>("curve-toggle");
            var toggleButton = element.Q<Button>("toggle-button");
            var actionButton = element.Q<Button>("action");

            var isInTarget = _blendShapeManager.IsInTarget(item.KeyIndex);
            UpdateRowPresentation(item, changedMarker, facialRail, nameLabel, isInTarget);
            var isCurveMode = _blendShapeManager.IsCurveMode(item.KeyIndex);
            sliderFloatField.SetVisible(!isCurveMode);
            curveField.SetVisible(isCurveMode);
            curveToggle.SetVisible(_blendShapeManager.AllowsCurves);
            curveToggle.SetEnabled(_blendShapeManager.AllowsCurves);
            curveToggle.style.unityFontStyleAndWeight = isCurveMode ? FontStyle.Bold : FontStyle.Normal;
            toggleButton.SetEnabled(!isCurveMode);

            nameLabel.text = item.ShapeName;
            var missing = _blendShapeManager.IsMissing(item.KeyIndex);
            var unavailable = _blendShapeManager.IsExplicitlyExcluded(item.ShapeName);
            warningIcon.SetVisible(missing || unavailable);
            warningIcon.tooltip = missing
                ? "blendShape.validation.missing.tooltip".LS()
                : unavailable
                    ? "blendShape.validation.unavailable.tooltip".LS()
                    : string.Empty;
            var currentWeight = _blendShapeManager.GetEffectiveShapeWeight(item.KeyIndex);
            sliderFloatField.SetValueWithoutNotify(currentWeight);
            UpdateActionButton(item, actionButton);
        }

        void UpdateRowPresentation(
            ElementData item,
            VisualElement changedMarker,
            VisualElement facialRail,
            Label nameLabel,
            bool? isInTarget = null)
        {
            changedMarker.EnableInClassList(
                "changed-marker--visible",
                _blendShapeManager.IsShapeChangedFromInitialState(item.KeyIndex));
            facialRail.style.opacity = _styleToggle.value && item.IsFacial ? 0.5f : 0f;
            nameLabel.style.opacity = (isInTarget ?? _blendShapeManager.IsInTarget(item.KeyIndex)) ? 1f : 0.65f;
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
        _styleToggle.SetVisible(_allSource.Any(item => item.IsFacial));
        BuildCurrentSource();

        _selectedListView.itemsSource = _currentSource;

        UpdateControlsVisibility();
        _selectedListView.RefreshItems();
    }

    private void RequestControlsVisibilityUpdate()
    {
        if (_controlsRefreshPending) return;

        _controlsRefreshPending = true;
        _element.schedule.Execute(() =>
        {
            _controlsRefreshPending = false;
            UpdateControlsVisibility();
        });
    }

    private void UpdateControlsVisibility()
    {
        if (_allSource == null) return;

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
        _currentSource.AddRange(EnumerateCurrentSource());
    }

    private IEnumerable<ElementData> EnumerateCurrentSource()
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
            var isVisibleSource = item.IsBase || (_styleToggle.value && item.IsFacial);
            if (!isVisibleSource && !isInTarget)
                continue;

            yield return item;
        }
    }


    private void RebuildListViewsSlow()
    {
        BuildCurrentSource();
        UpdateControlsVisibility();
        _selectedListView.RefreshItems();
    }
}
