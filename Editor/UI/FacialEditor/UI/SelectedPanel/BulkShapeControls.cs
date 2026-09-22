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

    public VisualElement Element { get; } = new SpacedHorizontalElement();

    public BulkShapeControls(
        Action<float> setWeights,
        Action removeZeros,
        Action removeAll)
    {
        Element.style.alignItems = Align.Center;
        var label = new Label("facialEditor.bulkWeight.label".LS());
        var weight = new FloatField { isDelayed = true, value = 100f };
        weight.style.width = 55f;
        weight.RegisterValueChangedCallback(evt =>
        {
            var value = Mathf.Clamp(evt.newValue, 0f, 100f);
            weight.SetValueWithoutNotify(value);
            setWeights(value);
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
        foreach (var button in new[] { _removeZero, toggle, remove })
        {
            button.AddToClassList("compact-control");
            button.style.width = 30f;
        }
        Element.Add(label);
        Element.Add(weight);
        Element.Add(spacer);
        Element.Add(_removeZero);
        Element.Add(toggle);
        Element.Add(remove);
    }

    public void SetRemoveZeroVisible(bool visible) => _removeZero.SetVisible(visible);
}
