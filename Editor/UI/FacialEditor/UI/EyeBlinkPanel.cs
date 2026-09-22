using Aoyon.FaceTune.Gui.Components;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class EyeBlinkPanel : IDisposable
{
    private enum RowKind { Controls, Shape, Empty, Spacer }
    private readonly record struct RowData(RowKind Kind, int ListIndex, int ManagerIndex);

    private readonly FacialShapesEditorContext _context;
    private readonly UnselectedPanel[] _availablePanels;
    private readonly ListView _selected = new();
    private readonly List<RowData> _rows = new();
    private readonly List<(VisualElement Root, BulkShapeControls Controls)> _bulkRows = new();

    public VisualElement SelectedElement { get; } = new SpacedVerticalElement();
    public VisualElement AvailableElement { get; } = new VisualElement();

    public EyeBlinkPanel(
        FacialShapesEditorContext context,
        VisualElement timeline)
    {
        _context = context;
        _availablePanels = context.DataManagers
            .Select((manager, index) => new UnselectedPanel(
                manager,
                context.GroupManager,
                context.PreviewManager,
                index == 1 ? 0f : 100f))
            .ToArray();

        SelectedElement.Add(timeline);
        var timelineGap = new VisualElement();
        timelineGap.style.height = FacialShapeUI.RowHeight;
        SelectedElement.Add(timelineGap);
        SelectedElement.Add(_selected);

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
        var root = new HorizontalElement();
        root.style.alignItems = Align.Center;
        var header = new SimpleToggle { name = "header" };
        header.style.alignSelf = Align.FlexStart;
        header.style.marginRight = FacialShapeUI.Spacing;
        header.RegisterValueChangedCallback(evt =>
        {
            if (root.userData is not RowData { Kind: RowKind.Controls } row) return;
            if (evt.newValue)
                _context.SetActiveList(row.ListIndex);
            else if (_context.ActiveListIndex == row.ListIndex)
                header.SetValueWithoutNotify(true);
        });

        var bulk = new BulkShapeControls(
            weight =>
            {
                if (root.userData is RowData { Kind: RowKind.Controls } row)
                    SetSectionWeights(row.ListIndex, weight);
            },
            () =>
            {
                if (root.userData is RowData { Kind: RowKind.Controls } row)
                    RemoveSectionZeros(row.ListIndex);
            },
            () =>
            {
                if (root.userData is RowData { Kind: RowKind.Controls } row)
                    _context.DataManagers[row.ListIndex].RemoveShapes(
                        VisibleTargetIndices(row.ListIndex));
            });
        bulk.Element.name = "bulk";
        bulk.Element.style.flexGrow = 1f;
        bulk.Element.userData = bulk;
        _bulkRows.Add((root, bulk));

        var shape = new SelectedShapeRow { name = "shape" };
        shape.style.flexGrow = 1f;
        shape.FacialRail.style.opacity = 0f;
        shape.Curve.SetVisible(false);
        shape.CurveToggle.SetVisible(false);
        shape.Weight.RegisterValueChangedCallback(evt =>
        {
            if (root.userData is not RowData { Kind: RowKind.Shape } row) return;
            var manager = _context.DataManagers[row.ListIndex];
            manager.SetShapeWeight(row.ManagerIndex, evt.newValue);
            shape.SetChanged(manager.IsShapeChangedFromInitialState(row.ManagerIndex));
            RefreshBulkControl(row.ListIndex);
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
            RefreshBulkControl(row.ListIndex);
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
        empty.style.flexGrow = 1f;
        root.Add(header);
        root.Add(bulk.Element);
        root.Add(shape);
        root.Add(empty);
        return root;
    }

    private void BindItem(VisualElement element, int index)
    {
        var row = _rows[index];
        element.userData = row;
        var header = element.Q<SimpleToggle>("header");
        var bulkElement = element.Q<VisualElement>("bulk");
        var bulk = (BulkShapeControls)bulkElement.userData;
        var shape = element.Q<SelectedShapeRow>("shape");
        var empty = element.Q<Label>("empty");
        header.SetVisible(row.Kind == RowKind.Controls);
        bulkElement.SetVisible(row.Kind == RowKind.Controls);
        shape.SetVisible(row.Kind == RowKind.Shape);
        empty.SetVisible(row.Kind == RowKind.Empty);
        var selected = row.ListIndex == _context.ActiveListIndex;
        empty.SetEnabled(selected);
        bulkElement.SetEnabled(selected);
        if (row.Kind is RowKind.Spacer or RowKind.Empty) return;

        if (row.Kind == RowKind.Controls)
        {
            header.text = row.ListIndex == 0
                ? "shapesEditor.eyeBlink.label".LS()
                : "shapesEditor.conflictCorrection.label".LS();
            header.SetValueWithoutNotify(selected);
            var controlsManager = _context.DataManagers[row.ListIndex];
            bulk.SetRemoveZeroVisible(VisibleTargetIndices(row.ListIndex)
                .Any(index => Mathf.Approximately(controlsManager.GetShapeWeight(index), 0f)));
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
            _rows.Add(new RowData(RowKind.Controls, listIndex, -1));
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
        _selected.RefreshItems();
    }

    private int[] VisibleTargetIndices(int listIndex)
    {
        var manager = _context.DataManagers[listIndex];
        return manager.GetTargetIndices(index =>
                !_context.GroupManager.IsLeftSelected
                || _context.GroupManager.IsBlendShapeVisible(index))
            .ToArray();
    }

    private void SetSectionWeights(int listIndex, float weight)
    {
        _context.DataManagers[listIndex].SetShapesWeight(
            VisibleTargetIndices(listIndex),
            weight);
        _selected.RefreshItems();
    }

    private void RemoveSectionZeros(int listIndex)
    {
        var manager = _context.DataManagers[listIndex];
        manager.RemoveShapes(VisibleTargetIndices(listIndex)
            .Where(index => Mathf.Approximately(manager.GetShapeWeight(index), 0f)));
    }

    private void RefreshBulkControl(int listIndex)
    {
        var manager = _context.DataManagers[listIndex];
        var visible = VisibleTargetIndices(listIndex);
        foreach (var (root, controls) in _bulkRows)
        {
            if (root.userData is RowData { Kind: RowKind.Controls } row
                && row.ListIndex == listIndex)
            {
                controls.SetRemoveZeroVisible(visible.Any(index =>
                    Mathf.Approximately(manager.GetShapeWeight(index), 0f)));
            }
        }
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
