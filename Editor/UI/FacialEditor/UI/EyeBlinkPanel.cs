using Aoyon.FaceTune.Gui.Components;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class EyeBlinkPanel : IDisposable
{
    private enum RowKind { Header, Shape, Empty, Spacer }
    private readonly record struct RowData(RowKind Kind, int ListIndex, int ManagerIndex);

    private readonly FacialShapesEditorContext _context;
    private readonly UnselectedPanel[] _availablePanels;
    private readonly ListView _selected = new();
    private readonly List<RowData> _rows = new();

    public VisualElement SelectedElement => _selected;
    public VisualElement AvailableElement { get; } = new VisualElement();

    public EyeBlinkPanel(FacialShapesEditorContext context)
    {
        _context = context;
        _availablePanels = context.DataManagers
            .Select(manager => new UnselectedPanel(
                manager,
                context.GroupManager,
                context.PreviewManager))
            .ToArray();

        _selected.fixedItemHeight = FacialShapeUI.ListItemHeight;
        _selected.selectionType = SelectionType.None;
        _selected.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
        _selected.style.flexGrow = 1f;
        _selected.makeItem = MakeItem;
        _selected.bindItem = BindItem;
        _selected.itemsSource = _rows;
        AvailableElement.style.flexGrow = 1f;

        foreach (var manager in context.DataManagers)
        {
            manager.OnSingleShapeAdded += _ => RebuildRows();
            manager.OnMultipleShapesAdded += _ => RebuildRows();
            manager.OnSingleShapeRemoved += _ => RebuildRows();
            manager.OnMultipleShapesRemoved += _ => RebuildRows();
            manager.OnUnknownChange += RebuildRows;
        }
        context.ActiveListChanged += UpdateSelection;
        context.GroupManager.OnGroupSelectionChanged += _ => RebuildRows();
        context.GroupManager.OnLeftSelectionChanged += _ => RebuildRows();
        RebuildRows();
        UpdateSelection();
    }

    private VisualElement MakeItem()
    {
        var root = new VisualElement();
        var header = new SimpleToggle { name = "header" };
        header.style.alignSelf = Align.FlexStart;
        header.RegisterValueChangedCallback(evt =>
        {
            if (root.userData is not RowData { Kind: RowKind.Header } row) return;
            if (evt.newValue)
                _context.SetActiveList(row.ListIndex);
            else if (_context.ActiveListIndex == row.ListIndex)
                header.SetValueWithoutNotify(true);
        });

        var shape = new SelectedShapeRow { name = "shape" };
        shape.FacialRail.style.opacity = 0f;
        shape.Curve.SetVisible(false);
        shape.CurveToggle.SetVisible(false);
        shape.Weight.RegisterValueChangedCallback(evt =>
        {
            if (root.userData is not RowData { Kind: RowKind.Shape } row) return;
            var manager = _context.DataManagers[row.ListIndex];
            manager.SetShapeWeight(row.ManagerIndex, evt.newValue);
            shape.SetChanged(manager.IsShapeChangedFromInitialState(row.ManagerIndex));
        });
        shape.WeightToggle.clicked += () =>
        {
            if (root.userData is not RowData { Kind: RowKind.Shape } row) return;
            var manager = _context.DataManagers[row.ListIndex];
            var weight = Mathf.Approximately(manager.GetEffectiveShapeWeight(row.ManagerIndex), 0f)
                ? 100f
                : 0f;
            manager.SetShapeWeight(row.ManagerIndex, weight);
            shape.Weight.SetValueWithoutNotify(weight);
            shape.SetChanged(manager.IsShapeChangedFromInitialState(row.ManagerIndex));
        };
        shape.RemoveButton.clicked += () =>
        {
            if (root.userData is RowData { Kind: RowKind.Shape } row)
                _context.DataManagers[row.ListIndex].RemoveShape(row.ManagerIndex);
        };
        shape.Metadata.RegisterCallback<ClickEvent>(_ =>
        {
            if (root.userData is not RowData { Kind: RowKind.Shape } row) return;
            var manager = _context.DataManagers[row.ListIndex];
            if (!manager.TryRestoreShapeToInitialState(row.ManagerIndex)) return;
            shape.Weight.SetValueWithoutNotify(manager.GetEffectiveShapeWeight(row.ManagerIndex));
            shape.SetChanged(false);
        });

        var empty = SelectedShapeRow.CreateEmptyLabel();
        empty.name = "empty";
        root.Add(header);
        root.Add(shape);
        root.Add(empty);
        return root;
    }

    private void BindItem(VisualElement element, int index)
    {
        var row = _rows[index];
        element.userData = row;
        var header = element.Q<SimpleToggle>("header");
        var shape = element.Q<SelectedShapeRow>("shape");
        var empty = element.Q<Label>("empty");
        header.SetVisible(row.Kind == RowKind.Header);
        shape.SetVisible(row.Kind == RowKind.Shape);
        empty.SetVisible(row.Kind == RowKind.Empty);
        var selected = row.ListIndex == _context.ActiveListIndex;
        empty.SetEnabled(selected);
        if (row.Kind is RowKind.Spacer or RowKind.Empty) return;

        if (row.Kind == RowKind.Header)
        {
            header.text = row.ListIndex == 0
                ? "previewOverlay.eyeBlink.label".LS()
                : "eyeBlink.simple.conflictBlendShapes.label".LS();
            header.SetValueWithoutNotify(selected);
            return;
        }

        shape.SetEnabled(selected);
        var manager = _context.DataManagers[row.ListIndex];
        var name = manager.AllKeys[row.ManagerIndex];
        shape.NameLabel.text = name;
        shape.SetWarning(manager.IsMissing(row.ManagerIndex), manager.IsExplicitlyExcluded(name));
        shape.SetChanged(manager.IsShapeChangedFromInitialState(row.ManagerIndex));
        shape.Weight.SetValueWithoutNotify(manager.GetEffectiveShapeWeight(row.ManagerIndex));
    }

    private void RebuildRows()
    {
        _rows.Clear();
        for (var listIndex = 0; listIndex < _context.DataManagers.Count; listIndex++)
        {
            if (listIndex > 0)
                _rows.Add(new RowData(RowKind.Spacer, -1, -1));
            _rows.Add(new RowData(RowKind.Header, listIndex, -1));
            var manager = _context.DataManagers[listIndex];
            var indices = manager.GetTargetIndices(index =>
                    !_context.GroupManager.IsLeftSelected
                    || _context.GroupManager.IsBlendShapeVisible(index))
                .ToArray();
            if (indices.Length == 0)
            {
                _rows.Add(new RowData(RowKind.Empty, listIndex, -1));
                continue;
            }
            foreach (var managerIndex in indices)
                _rows.Add(new RowData(RowKind.Shape, listIndex, managerIndex));
        }
        _selected.Rebuild();
    }

    private void UpdateSelection()
    {
        _selected.RefreshItems();
        AvailableElement.Clear();
        AvailableElement.Add(_availablePanels[_context.ActiveListIndex].Element);
    }

    public void Dispose()
    {
        _context.ActiveListChanged -= UpdateSelection;
    }
}
