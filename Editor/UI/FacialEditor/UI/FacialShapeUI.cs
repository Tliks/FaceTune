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
    private LipSyncPanel? _lipSyncPanel;
    private EyeBlinkPanel? _eyeBlinkPanel;
    private PreviewTimelineElement? _previewTimeline;
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
        if (context.Mode is ShapesEditorMode.EyeBlinkSimple or ShapesEditorMode.EyeBlinkCustom)
        {
            _previewTimeline = new PreviewTimelineElement(
                context.InitialPreviewTime,
                context.PreviewManager.SetNormalizedTime);
        }
        if (context.ModeSession is LipSyncModeSession lipSync)
            _lipSyncPanel = new LipSyncPanel(context, lipSync.Editing);
        else if (context.Mode == ShapesEditorMode.EyeBlinkSimple)
            _eyeBlinkPanel = new EyeBlinkPanel(context, _previewTimeline!.Element);
        SetupListSelector(root);

        if (_lipSyncPanel != null) ShowLipSync();
        else if (_eyeBlinkPanel != null) ShowEyeBlink();
        else
        {
            ShowActiveList();
            context.ActiveListChanged += ShowActiveList;
        }
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
            case ShapesEditorMode.EyeBlinkCustom:
                container.SetVisible(false);
                gap.SetVisible(false);
                break;
            case ShapesEditorMode.LipSync:
                container.SetVisible(false);
                gap.SetVisible(false);
                break;
        }
    }

    private void ShowLipSync()
    {
        if (_lipSyncPanel == null) return;
        _selectedContainer.Clear();
        _unselectedContainer.Clear();
        _selectedContainer.Add(_lipSyncPanel.SelectedElement);
        _unselectedContainer.Add(_lipSyncPanel.AvailableElement);
    }

    private void ShowEyeBlink()
    {
        if (_eyeBlinkPanel == null) return;
        _selectedContainer.Clear();
        _unselectedContainer.Clear();
        _selectedContainer.Add(_eyeBlinkPanel.SelectedElement);
        _unselectedContainer.Add(_eyeBlinkPanel.AvailableElement);
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
        if (_context.Mode == ShapesEditorMode.EyeBlinkCustom && _previewTimeline != null)
            _selectedContainer.Add(_previewTimeline.Element);
        _selectedContainer.Add(panels.Selected.Element);
        _unselectedContainer.Add(panels.Unselected.Element);
    }

    public void RefreshLipSync() => _lipSyncPanel?.Rebuild();

    public void Dispose()
    {
        _context.ActiveListChanged -= ShowActiveList;
        _eyeBlinkPanel?.Dispose();
        _previewTimeline?.Dispose();
        _generalControls.Dispose();
    }
}
