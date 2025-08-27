using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal class FacialShapeUI
{
    private static VisualTreeAsset? _uxml;
    private static StyleSheet? _uss;

    private GeneralControls _generalControls = null!;
    private SelectedPanel _selectedPanel = null!;
    private UnselectedPanel _unselectedPanel = null!;

    public FacialShapeUI(VisualElement root, TargetManager targetManager, BlendShapeOverrideManager dataManager, BlendShapeGrouping groupManager, PreviewManager previewManager)
    {
        var uxml = UIAssetHelper.EnsureUxmlWithGuid(ref _uxml, "c5be08ef18f5b6e409aa55f3e4cf67a0");
        var uss = UIAssetHelper.EnsureUssWithGuid(ref _uss, "5405c529d1ac1ba478455a85e4b1c771");

        root.Clear();
        root.Add(uxml.CloneTree());
        root.styleSheets.Add(uss);
        Localization.LocalizeUIElements(root);

        _generalControls = new GeneralControls(targetManager, dataManager, groupManager, previewManager);
        _selectedPanel = new SelectedPanel(dataManager, groupManager);
        _unselectedPanel = new UnselectedPanel(dataManager, groupManager, previewManager);

		root.Q<VisualElement>("general-controls-container").Add(_generalControls.Element);
        root.Q<VisualElement>("selected-content-container").Add(_selectedPanel.Element);
        root.Q<VisualElement>("unselected-content-container").Add(_unselectedPanel.Element);

		UIEventHandler();
    }

    private void UIEventHandler()
    {
		_selectedPanel.OnSelectedItemNameClicked += keyIndex =>
		{
			_unselectedPanel.Element.schedule.Execute(() =>
			{
				_unselectedPanel.ScrollToNearestKeyIndex(keyIndex, true, false);
			});
		};
    }

    public void RefreshTarget()
    {
        _generalControls.RefreshTarget();
        _selectedPanel.RefreshTarget();
        _unselectedPanel.RefreshTarget();
    }
}