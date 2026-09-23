using Aoyon.FaceTune.Gui.Components;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class TrackingShapeRow : VisualElement
{
    private static StyleSheet? _uss;

    private static readonly Texture ToggleIcon =
        EditorGUIUtility.IconContent("d_preAudioLoopOff").image;

    private static readonly Texture RemoveIcon =
        EditorGUIUtility.IconContent("d_Toolbar Minus").image;

    private static readonly Texture WarningIcon =
        EditorGUIUtility.IconContent("console.warnicon.sml").image;

    public static StyleSheet SharedStyleSheet =>
        UIAssetHelper.EnsureUssWithGuid(
            ref _uss,
            "a00c7162d21d9e34ab15764bdb0d1173");

    public VisualElement Metadata { get; }
    public VisualElement ChangedMarker { get; }
    public Image Warning { get; }
    public Label NameLabel { get; }
    public SliderFloatField Weight { get; }
    public Button WeightToggle { get; }
    public Button RemoveButton { get; }

    public TrackingShapeRow()
    {
        name = "list-item-container";
        AddToClassList("list-item-row");

        style.flexDirection = FlexDirection.Row;
        style.alignItems = Align.Center;

        Metadata = new VisualElement
        {
            name = "metadata-gutter"
        };

        ChangedMarker = new VisualElement
        {
            name = "changed-marker"
        };

        Metadata.Add(ChangedMarker);

        Warning = new Image
        {
            name = "validation-warning",
            image = WarningIcon
        };

        NameLabel = new Label
        {
            name = "name"
        };
        NameLabel.AddToClassList("list-item-label");

        Weight = new SliderFloatField
        {
            name = "slider-float-field",
            lowValue = 0f,
            highValue = 100f
        };
        Weight.AddToClassList("compact-field");

        WeightToggle = new Button
        {
            name = "toggle-button"
        };
        WeightToggle.AddToClassList("compact-control");
        WeightToggle.Add(new Image { image = ToggleIcon });

        RemoveButton = new Button
        {
            name = "action"
        };
        RemoveButton.AddToClassList("compact-control");
        RemoveButton.Add(new Image { image = RemoveIcon });

        Add(Metadata);
        Add(Warning);
        Add(NameLabel);
        Add(Weight);
        Add(WeightToggle);
        Add(RemoveButton);
    }

    public void SetChanged(bool changed)
    {
        ChangedMarker.EnableInClassList(
            "changed-marker--visible",
            changed);
    }

    public void SetWarning(bool missing, bool unavailable)
    {
        Warning.SetVisible(missing || unavailable);

        Warning.tooltip = missing
            ? "blendShape.validation.missing.tooltip".LS()
            : unavailable
                ? "blendShape.validation.unavailable.tooltip".LS()
                : string.Empty;
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
}
