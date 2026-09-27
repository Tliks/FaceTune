using Aoyon.FaceTune.Gui.Components;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class BulkShapeControls
{
    private static readonly Texture ToggleIcon =
        EditorGUIUtility.IconContent("d_preAudioLoopOff").image;
    private static readonly Texture RemoveIcon =
        EditorGUIUtility.IconContent("d_Toolbar Minus").image;
    private bool _setToZero;
    private readonly Button _removeZero;
    private readonly Button? _curveToggle;

    public VisualElement Element { get; }

    public BulkShapeControls(
        Action<float> setWeights,
        Action removeZeros,
        Action removeAll,
        bool trackingColumns = false,
        Action? toggleCurveMode = null)
    {
        Element = trackingColumns ? new HorizontalElement() : new SpacedHorizontalElement();
        Element.name = "list-item-container";
        Element.AddToClassList("list-item-row");
        Element.styleSheets.Add(TrackingShapeRow.SharedStyleSheet);
        Element.style.alignItems = Align.Center;
        Element.style.flexGrow = 1f;
        var weight = new TextField { isDelayed = true };
        weight.style.width = 32f;
        weight.style.marginLeft = 0f;
        weight.style.marginRight = 0f;
        weight.style.marginTop = 0f;
        weight.style.marginBottom = 0f;
        weight.style.minHeight = 0f;
        weight.style.flexShrink = 0f;
        weight.RegisterValueChangedCallback(evt =>
        {
            if (float.TryParse(evt.newValue, out var value))
                setWeights(Mathf.Clamp(value, 0f, 100f));
            weight.SetValueWithoutNotify(string.Empty);
        });
        var spacer = new VisualElement();
        spacer.style.flexGrow = 1f;
        _removeZero = new Button(removeZeros) { text = "0-" };
        var toggle = new Button(() =>
        {
            setWeights(_setToZero ? 0f : 100f);
            _setToZero = !_setToZero;
        });
        toggle.Add(new Image { image = ToggleIcon });
        var remove = new Button(removeAll);
        remove.Add(new Image { image = RemoveIcon });
        var weightColumn = new HorizontalElement { name = "slider-float-field" };
        weightColumn.AddToClassList("compact-field");
        weightColumn.style.alignItems = Align.Center;
        weightColumn.Add(spacer);
        weightColumn.Add(_removeZero);
        weightColumn.Add(weight);

        Element.Add(new VisualElement { name = "metadata-gutter" });
        Element.Add(new VisualElement { name = "validation-warning" });
        Element.Add(new VisualElement { name = "name" });
        Element.Add(weightColumn);

        if (toggleCurveMode != null)
        {
            _curveToggle = new Button(toggleCurveMode) { name = "curve-toggle", text = "M" };
            _curveToggle.tooltip = "blendShapeAnimation.multiFrame.label".LS();
            Element.Add(_curveToggle);
        }

        toggle.name = "toggle-button";
        remove.name = "action";
        foreach (var button in new[] { _removeZero, _curveToggle, toggle, remove })
        {
            if (button == null) continue;
            button.AddToClassList("compact-control");
            button.style.width = 30f;
            button.style.flexShrink = 0f;
        }
        Element.Add(toggle);
        Element.Add(remove);
    }

    public void SetRemoveZeroVisible(bool visible) => _removeZero.SetVisible(visible);

    public void SetCurveModeActive(bool active)
    {
        if (_curveToggle != null)
            _curveToggle.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
    }
}
