using Aoyon.FaceTune.Gui.Components;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal class FacialShapeUI : IDisposable
{
    internal const float RowHeight = 18f;
    internal const float Spacing = 2f;
    internal const float ListItemHeight = RowHeight + Spacing;

    private static VisualTreeAsset? _uxml;
    private static StyleSheet? _uss;

    private readonly FacialShapesEditorContext _context;
    private readonly VisualElement _selectedContainer;
    private readonly VisualElement _unselectedContainer;
    private readonly Dictionary<int, (SelectedPanel Selected, UnselectedPanel Unselected)> _panels = new();
    private readonly List<SimpleToggle> _listButtons = new();
    private LipSyncPanel? _lipSyncPanel;
    private SimpleToggle? _lipSyncButton;
    private SimpleToggle? _cancellerButton;
    private GeneralControls _generalControls;

    public FacialShapeUI(
        VisualElement root,
        FacialShapesEditorContext context,
        Func<SkinnedMeshRenderer?, bool> tryChangeRenderer,
        Action save)
    {
        _context = context;
        var uxml = UIAssetHelper.EnsureUxmlWithGuid(ref _uxml, "c5be08ef18f5b6e409aa55f3e4cf67a0");
        var uss = UIAssetHelper.EnsureUssWithGuid(ref _uss, "5405c529d1ac1ba478455a85e4b1c771");

        root.Clear();
        root.Add(uxml.CloneTree());
        root.styleSheets.Add(uss);
        Localization.LocalizeUIElements(root);

        _generalControls = new GeneralControls(context, tryChangeRenderer, save);
        _selectedContainer = root.Q<VisualElement>("selected-content-container");
        _unselectedContainer = root.Q<VisualElement>("unselected-content-container");
        root.Q<VisualElement>("general-controls-container").Add(_generalControls.Element);
        if (context.Mode == ShapesEditorMode.LipSync)
            _lipSyncPanel = new LipSyncPanel(context);
        SetupListSelector(root);

        if (_lipSyncPanel == null) ShowActiveList();
        else ShowLipSync();
        context.ActiveListChanged += ShowActiveList;
    }

    private void SetupListSelector(VisualElement root)
    {
        var container = root.Q<VisualElement>("mode-controls-container");
        var gap = root.Q<VisualElement>("mode-controls-gap");
        container.style.flexDirection = FlexDirection.Row;
        container.style.flexWrap = Wrap.Wrap;

        switch (_context.Mode)
        {
            case ShapesEditorMode.Facial:
                container.SetVisible(false);
                gap.SetVisible(false);
                break;
            case ShapesEditorMode.EyeBlinkSimple:
                AddListButtons(container, new[]
                {
                    "eyeBlink.simple.blinkBlendShapes.label".LS(),
                    "eyeBlink.simple.conflictBlendShapes.label".LS()
                });
                AddTimeSlider(container, 1f);
                break;
            case ShapesEditorMode.EyeBlinkCustom:
                AddTimeSlider(container, 0f);
                break;
            case ShapesEditorMode.LipSync:
                SetupLipSyncToolbar(container);
                break;
        }
    }

    private void SetupLipSyncToolbar(VisualElement container)
    {
        _lipSyncButton = new SimpleToggle
        {
            text = "previewOverlay.lipSync.label".LS(),
            value = true
        };
        _cancellerButton = new SimpleToggle
        {
            text = "lipSync.cancellerBlendShapes.label".LS()
        };
        _lipSyncButton.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue) ShowLipSync();
            else if (_cancellerButton?.value != true)
                _lipSyncButton.SetValueWithoutNotify(true);
        });
        _cancellerButton.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue) ShowCanceller();
            else if (_lipSyncButton?.value != true)
                _cancellerButton.SetValueWithoutNotify(true);
        });
        _lipSyncButton.style.flexGrow = 1f;
        _cancellerButton.style.flexGrow = 1f;
        container.Add(_lipSyncButton);
        container.Add(_cancellerButton);
    }

    private void ShowLipSync()
    {
        if (_lipSyncPanel == null) return;
        _lipSyncButton?.SetValueWithoutNotify(true);
        _cancellerButton?.SetValueWithoutNotify(false);
        _selectedContainer.Clear();
        _unselectedContainer.Clear();
        _selectedContainer.Add(_lipSyncPanel.SelectedElement);
        _unselectedContainer.Add(_lipSyncPanel.AvailableElement);
    }

    private void ShowCanceller()
    {
        _lipSyncButton?.SetValueWithoutNotify(false);
        _cancellerButton?.SetValueWithoutNotify(true);
        ShowActiveList();
    }

    private void AddListButtons(
        VisualElement container,
        IEnumerable<string> labels)
    {
        var labelArray = labels.ToArray();
        for (var index = 0; index < labelArray.Length; index++)
        {
            var listIndex = index;
            var button = new SimpleToggle { text = labelArray[index] };
            button.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) _context.SetActiveList(listIndex);
            });
            button.AddToClassList("compact-control");
            button.style.minWidth = 120f;
            button.style.marginRight = Spacing;
            button.style.marginBottom = Spacing;
            container.Add(button);
            _listButtons.Add(button);
        }
    }

    private void AddTimeSlider(VisualElement container, float initialValue)
    {
        var time = new Slider(0f, 1f)
        {
            value = initialValue,
            showInputField = true,
            tooltip = "eyeBlink.animations.label".LS()
        };
        time.style.flexGrow = 1f;
        time.RegisterValueChangedCallback(evt =>
            _context.PreviewManager.SetNormalizedTime(evt.newValue));
        container.Add(time);
    }

    private void ShowActiveList()
    {
        var index = _context.ActiveListIndex;
        for (var buttonIndex = 0; buttonIndex < _listButtons.Count; buttonIndex++)
            _listButtons[buttonIndex].SetValueWithoutNotify(buttonIndex == index);
        if (!_panels.TryGetValue(index, out var panels))
        {
            var dataManager = _context.DataManagers[index];
            var selected = new SelectedPanel(dataManager, _context.GroupManager);
            var unselected = new UnselectedPanel(
                dataManager,
                _context.GroupManager,
                _context.PreviewManager);
            selected.OnSelectedItemNameClicked += keyIndex =>
                unselected.Element.schedule.Execute(() =>
                    unselected.ScrollToNearestKeyIndex(keyIndex, true, false));
            panels = (selected, unselected);
            _panels.Add(index, panels);
        }

        var editable = _context.IsListEditable(index);
        panels.Selected.Element.SetEnabled(editable);
        panels.Unselected.Element.SetEnabled(editable);
        _selectedContainer.Clear();
        _unselectedContainer.Clear();
        _selectedContainer.Add(panels.Selected.Element);
        _unselectedContainer.Add(panels.Unselected.Element);
    }

    public void RefreshLipSync() => _lipSyncPanel?.Rebuild();

    public void Dispose()
    {
        _context.ActiveListChanged -= ShowActiveList;
        _generalControls.Dispose();
    }
}
