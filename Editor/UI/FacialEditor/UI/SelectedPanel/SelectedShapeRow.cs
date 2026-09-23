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

    // getterでQ()を毎回実行しないよう、constructorで一度だけ取得する
    public VisualElement Metadata { get; }
    public VisualElement ChangedMarker { get; }
    public VisualElement FacialRail { get; }
    public Image Warning { get; }
    public Label NameLabel { get; }
    public SliderFloatField Weight { get; }
    public IMGUIContainer Curve { get; }
    public Button CurveToggle { get; }
    public Button WeightToggle { get; }
    public Button RemoveButton { get; }

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
        Metadata = this.Q<VisualElement>("metadata-gutter");
        ChangedMarker = this.Q<VisualElement>("changed-marker");
        FacialRail = this.Q<VisualElement>("facial-rail");
        Warning = this.Q<Image>("validation-warning");
        NameLabel = this.Q<Label>("name");
        Weight = this.Q<SliderFloatField>("slider-float-field");
        Curve = this.Q<IMGUIContainer>("curve-field");
        CurveToggle = this.Q<Button>("curve-toggle");
        WeightToggle = this.Q<Button>("toggle-button");
        RemoveButton = this.Q<Button>("action");

        // テンプレートにローカライズキーを持つ表示文字列がなく、tooltip等はコード側で.LS()済みのため行ごとのLocate処理はしない

        CurveToggle.text = "M";
        CurveToggle.tooltip = "blendShapeAnimation.multiFrame.label".LS();
        WeightToggle.Add(new Image { image = ToggleIcon });
        RemoveButton.Add(new Image { image = RemoveIcon });
        Warning.image = WarningIcon;
    }

    internal static Label CreateEmptyLabel()
    {
        var label = new Label("facialEditor.list.empty".LS());
        label.style.height = FacialShapeUI.RowHeight;
        label.style.marginLeft = 11f;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.opacity = 0.6f;
        return label;
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
