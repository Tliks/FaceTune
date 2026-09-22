using Aoyon.FaceTune.Gui.Components;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class ManagerShapeList
{
    private readonly BlendShapeOverrideManager _manager;
    private readonly BlendShapeGrouping _groups;

    public VisualElement Element { get; } = new SpacedVerticalElement();

    public ManagerShapeList(
        BlendShapeOverrideManager manager,
        BlendShapeGrouping groups)
    {
        _manager = manager;
        _groups = groups;
        _manager.OnSingleShapeAdded += _ => Rebuild();
        _manager.OnMultipleShapesAdded += _ => Rebuild();
        _manager.OnSingleShapeRemoved += _ => Rebuild();
        _manager.OnMultipleShapesRemoved += _ => Rebuild();
        _manager.OnUnknownChange += Rebuild;
        Rebuild();
    }

    public void Rebuild()
    {
        Element.Clear();
        foreach (var index in _manager.GetTargetIndices(shapeIndex =>
                     !_groups.IsLeftSelected || _groups.IsBlendShapeVisible(shapeIndex)))
        {
            Element.Add(CreateRow(index));
        }
        if (Element.childCount == 0)
            Element.Add(CreateEmptyLabel());
    }

    internal static Label CreateEmptyLabel()
    {
        var label = new Label("facialEditor.list.empty".LS());
        label.style.height = FacialShapeUI.RowHeight;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.opacity = 0.6f;
        return label;
    }

    private VisualElement CreateRow(int index)
    {
        var row = new SelectedShapeRow();
        var name = _manager.AllKeys[index];
        row.NameLabel.text = name;
        row.FacialRail.style.opacity = 0f;
        row.SetChanged(_manager.IsShapeChangedFromInitialState(index));
        row.SetWarning(_manager.IsMissing(index), _manager.IsExplicitlyExcluded(name));
        row.Curve.SetVisible(false);
        row.CurveToggle.SetVisible(false);
        row.Weight.SetValueWithoutNotify(_manager.GetEffectiveShapeWeight(index));
        row.Weight.RegisterValueChangedCallback(evt =>
        {
            _manager.SetShapeWeight(index, evt.newValue);
            row.SetChanged(_manager.IsShapeChangedFromInitialState(index));
        });
        row.WeightToggle.clicked += () =>
        {
            var weight = Mathf.Approximately(_manager.GetEffectiveShapeWeight(index), 0f)
                ? 100f
                : 0f;
            _manager.SetShapeWeight(index, weight);
            row.Weight.SetValueWithoutNotify(weight);
            row.SetChanged(_manager.IsShapeChangedFromInitialState(index));
        };
        row.RemoveButton.clicked += () => _manager.RemoveShape(index);
        row.Metadata.RegisterCallback<ClickEvent>(_ =>
        {
            if (!_manager.TryRestoreShapeToInitialState(index)) return;
            row.Weight.SetValueWithoutNotify(_manager.GetEffectiveShapeWeight(index));
            row.SetChanged(false);
        });
        return row;
    }
}
