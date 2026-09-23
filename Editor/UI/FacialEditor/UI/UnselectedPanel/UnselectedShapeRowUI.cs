using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal static class UnselectedShapeRowUI
{
    private static VisualTreeAsset? _uxml;

    internal static VisualElement Create()
    {
        var uxml = UIAssetHelper.EnsureUxmlWithGuid(
            ref _uxml,
            "3efe7e91dce1d544b873dd133a44039d");
        var element = uxml.CloneTree();
        // テンプレートは空Labelのみのため行ごとのローカライズは不要
        return element;
    }
}
