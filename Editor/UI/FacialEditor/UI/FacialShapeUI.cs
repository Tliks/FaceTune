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
    private readonly VisualElement _modeControlsContainer;
    private readonly VisualElement _modeControlsGap;
    private readonly Dictionary<int, (SelectedPanel Selected, UnselectedPanel Unselected)> _panels = new();
    private PreviewTimelineElement? _facialTimeline;
    private bool _facialTimelineVisible;
    private LipSyncPanel? _lipSyncPanel;
    private EyeBlinkEditorUI? _eyeBlinkUI;
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
        _modeControlsContainer = root.Q<VisualElement>("mode-controls-container");
        _modeControlsGap = root.Q<VisualElement>("mode-controls-gap");
        root.Q<VisualElement>("general-controls-container").Add(_generalControls.Element);
        if (context.ModeSession is LipSyncModeSession lipSync)
            _lipSyncPanel = new LipSyncPanel(context, lipSync.Editing);
        SetupListSelector();

        if (_lipSyncPanel != null) ShowLipSync();
        else if (context.ModeSession is EyeBlinkModeSession eyeBlink)
            _eyeBlinkUI = new EyeBlinkEditorUI(
                context, eyeBlink, _selectedContainer, _unselectedContainer,
                root.Q<VisualElement>("mode-controls-container"));
        else
        {
            if (context.ModeSession is FacialModeSession facial)
            {
                _facialTimeline = new PreviewTimelineElement(
                    context.InitialPreviewTime,
                    context.PreviewManager.SetNormalizedTime,
                    () => facial.PlaybackDurationSeconds);
                _modeControlsContainer.Add(_facialTimeline.Element);
                context.DataManager.OnAnyDataChange += UpdateFacialTimeline;
                UpdateFacialTimeline();
            }
            ShowActiveList();
            context.ActiveListChanged += ShowActiveList;
        }
    }

    private void SetupListSelector()
    {
        _modeControlsContainer.style.flexDirection = FlexDirection.Row;
        _modeControlsContainer.style.flexWrap = Wrap.Wrap;

        if (_context.Mode == ShapesEditorMode.LipSync)
        {
            _modeControlsContainer.SetVisible(false);
            _modeControlsGap.SetVisible(false);
        }
    }

    private void UpdateFacialTimeline()
    {
        var visible = _context.DataManager.HasPreviewMultiFrame;
        if (_facialTimelineVisible && !visible)
            _facialTimeline?.Seek(0f);
        _facialTimelineVisible = visible;
        _modeControlsContainer.SetVisible(visible);
        _modeControlsGap.SetVisible(visible);
    }

    private void ShowLipSync()
    {
        if (_lipSyncPanel == null) return;
        _selectedContainer.Clear();
        _unselectedContainer.Clear();
        _selectedContainer.Add(_lipSyncPanel.SelectedElement);
        _unselectedContainer.Add(_lipSyncPanel.AvailableElement);
    }

    private void ShowActiveList()
    {
        var index = _context.ActiveListIndex;
        if (!_panels.TryGetValue(index, out var panels))
        {
            var dataManager = _context.DataManagers[index];
            var selected = new SelectedPanel(
                dataManager,
                _context.GroupManager,
                _context.Mode == ShapesEditorMode.Facial);
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
        _eyeBlinkUI?.Dispose();
        if (_facialTimeline != null)
        {
            _context.DataManager.OnAnyDataChange -= UpdateFacialTimeline;
            _facialTimeline.Dispose();
        }
        _generalControls.Dispose();
    }
}
