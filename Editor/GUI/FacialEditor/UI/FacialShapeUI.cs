using UnityEngine.UIElements;

namespace aoyon.facetune.ui.shapes_editor;

internal class FacialShapeUI
{
    private readonly BlendShapeOverrideManager _blendShapeManager;
    private readonly BlendShapeGrouping _groupManager;

    private static VisualTreeAsset _uxml = null!;
    private static StyleSheet _uss = null!;

    public readonly GeneralControls GeneralControls;
    public readonly SelectedPanel SelectedPanel;
    public readonly UnselectedPanel UnselectedPanel;

    public readonly VisualElement Root;

    public FacialShapeUI(VisualElement root, BlendShapeOverrideManager manager)
    {
        _blendShapeManager = manager;
        _groupManager = new BlendShapeGrouping(_blendShapeManager.AllKeys);

        EnsureUIAssets();

        Root = root;
        _uxml.CloneTree(Root);
        Root.styleSheets.Add(_uss);

        GeneralControls = new GeneralControls(_blendShapeManager, _groupManager);
        SelectedPanel = new SelectedPanel(_blendShapeManager, _groupManager);
        UnselectedPanel = new UnselectedPanel(_blendShapeManager, _groupManager);

        // _root.Q<VisualElement>("test-container").Add(_selectedPanel.Element);

        Root.Q<VisualElement>("controls-container").Add(GeneralControls.Element);
        Root.Q<VisualElement>("primary-content-container").Add(SelectedPanel.Element);
        Root.Q<VisualElement>("secondary-content-container").Add(UnselectedPanel.Element);
    }

    private void EnsureUIAssets()
    {
        UIUtility.EnsureUxmlWithGuid(ref _uxml, "c5be08ef18f5b6e409aa55f3e4cf67a0");
        UIUtility.EnsureUssWithGuid(ref _uss, "5405c529d1ac1ba478455a85e4b1c771");
    }
}