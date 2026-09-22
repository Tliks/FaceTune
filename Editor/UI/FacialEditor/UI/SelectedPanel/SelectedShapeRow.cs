using Aoyon.FaceTune.Gui.Components;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class SelectedShapeRow : VisualElement
{
    private static VisualTreeAsset? _uxml;
    private static StyleSheet? _uss;
    private static readonly Texture ToggleIcon =
        EditorGUIUtility.IconContent("d_preAudioLoopOff").image;
    private static readonly Texture RemoveIcon =
        EditorGUIUtility.IconContent("d_Toolbar Minus").image;
    private static readonly Texture WarningIcon =
        EditorGUIUtility.IconContent("console.warnicon.sml").image;

    public VisualElement Metadata => this.Q<VisualElement>("metadata-gutter");
    public VisualElement ChangedMarker => this.Q<VisualElement>("changed-marker");
    public VisualElement FacialRail => this.Q<VisualElement>("facial-rail");
    public Image Warning => this.Q<Image>("validation-warning");
    public Label NameLabel => this.Q<Label>("name");
    public SliderFloatField Weight => this.Q<SliderFloatField>("slider-float-field");
    public IMGUIContainer Curve => this.Q<IMGUIContainer>("curve-field");
    public Button CurveToggle => this.Q<Button>("curve-toggle");
    public Button WeightToggle => this.Q<Button>("toggle-button");
    public Button Remove => this.Q<Button>("action");

    public SelectedShapeRow()
    {
        var uxml = UIAssetHelper.EnsureUxmlWithGuid(
            ref _uxml,
            "fc51e445111d2074091e2fef5d3565f9");
        var uss = UIAssetHelper.EnsureUssWithGuid(
            ref _uss,
            "a00c7162d21d9e34ab15764bdb0d1173");
        uxml.CloneTree(this);
        styleSheets.Add(uss);
        Localization.LocalizeUIElements(this);

        CurveToggle.text = "M";
        CurveToggle.tooltip = "blendShapeAnimation.multiFrame.label".LS();
        WeightToggle.Add(new Image { image = ToggleIcon });
        Remove.Add(new Image { image = RemoveIcon });
        Warning.image = WarningIcon;
    }

    public void SetChanged(bool changed)
        => ChangedMarker.EnableInClassList("changed-marker--visible", changed);

    public void SetWarning(bool missing, bool unavailable)
    {
        Warning.SetVisible(missing || unavailable);
        Warning.tooltip = missing
            ? "blendShape.validation.missing.tooltip".LS()
            : unavailable
                ? "blendShape.validation.unavailable.tooltip".LS()
                : string.Empty;
    }
}
