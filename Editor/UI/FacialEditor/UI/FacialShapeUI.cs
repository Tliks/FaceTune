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

        ShowActiveList();
        context.ActiveListChanged += ShowActiveList;
    }

    private void ShowActiveList()
    {
        var index = _context.ActiveListIndex;
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

        _selectedContainer.Clear();
        _unselectedContainer.Clear();
        _selectedContainer.Add(panels.Selected.Element);
        _unselectedContainer.Add(panels.Unselected.Element);
    }

    public void Dispose()
    {
        _context.ActiveListChanged -= ShowActiveList;
        _generalControls.Dispose();
    }
}
