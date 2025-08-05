using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.shapes_editor;

internal class FacialShapeUI
{
    private static VisualTreeAsset _uxml = null!;
    private static StyleSheet _uss = null!;

    private GeneralControls _generalControls = null!;
    private SelectedPanel _selectedPanel = null!;
    private UnselectedPanel _unselectedPanel = null!;

    public FacialShapeUI(VisualElement root, TargetManager targetManager, BlendShapeOverrideManager dataManager, BlendShapeGrouping groupManager, PreviewManager previewManager)
    {
        EnsureUIAssets();

        root.Clear();
        root.Add(_uxml.CloneTree());
        root.styleSheets.Add(_uss);

        _generalControls = new GeneralControls(targetManager, dataManager, groupManager, previewManager);
        _selectedPanel = new SelectedPanel(dataManager, groupManager);
        _unselectedPanel = new UnselectedPanel(dataManager, groupManager, previewManager);

        root.Q<VisualElement>("general-controls-container").Add(_generalControls.Element);
        root.Q<VisualElement>("selected-content-container").Add(_selectedPanel.Element);
        root.Q<VisualElement>("unselected-content-container").Add(_unselectedPanel.Element);
    }

    private void EnsureUIAssets()
    {
        UIAssetHelper.EnsureUxmlWithGuid(ref _uxml, "c5be08ef18f5b6e409aa55f3e4cf67a0");
        UIAssetHelper.EnsureUssWithGuid(ref _uss, "5405c529d1ac1ba478455a85e4b1c771");
    }

    public void RefreshTarget()
    {
        _generalControls.RefreshTarget();
        _selectedPanel.RefreshTarget();
        _unselectedPanel.RefreshTarget();
    }
}