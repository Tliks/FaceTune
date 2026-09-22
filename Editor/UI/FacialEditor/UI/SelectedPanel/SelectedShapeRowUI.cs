using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal static class SelectedShapeRowUI
{
    private static VisualTreeAsset? _uxml;
    private static StyleSheet? _uss;
    private static readonly Texture ToggleIcon =
        EditorGUIUtility.IconContent("d_preAudioLoopOff").image;
    private static readonly Texture RemoveIcon =
        EditorGUIUtility.IconContent("d_Toolbar Minus").image;
    internal static readonly Texture WarningIcon =
        EditorGUIUtility.IconContent("console.warnicon.sml").image;

    internal static VisualElement Create()
    {
        var uxml = UIAssetHelper.EnsureUxmlWithGuid(
            ref _uxml,
            "fc51e445111d2074091e2fef5d3565f9");
        var uss = UIAssetHelper.EnsureUssWithGuid(
            ref _uss,
            "a00c7162d21d9e34ab15764bdb0d1173");
        var element = uxml.CloneTree();
        element.styleSheets.Add(uss);
        Localization.LocalizeUIElements(element);

        var curve = element.Q<Button>("curve-toggle");
        curve.text = "M";
        curve.tooltip = "blendShapeAnimation.multiFrame.label".LS();
        element.Q<Button>("toggle-button").Add(new Image { image = ToggleIcon });
        element.Q<Button>("action").Add(new Image { image = RemoveIcon });
        element.Q<Image>("validation-warning").image = WarningIcon;
        return element;
    }
}
