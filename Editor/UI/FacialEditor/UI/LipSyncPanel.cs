using Aoyon.FaceTune.Gui.Components;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class LipSyncPanel
{
    private const float HeaderWidth = 56f;

    private enum RowKind { Controls, Shape, Empty, Spacer }

    private readonly record struct RowData(
        RowKind Kind,
        int SectionIndex,
        string ShapeName,
        int ManagerIndex,
        bool Canceller,
        bool ShowHeader);

    private readonly FacialShapesEditorContext _context;
    private readonly LipSyncEditing _editing;
    private readonly BlendShapeOverrideManager _canceller;
    private readonly ListView _selected = new();
    private readonly List<RowData> _rows = new();
    private readonly List<(VisualElement Root, BulkShapeControls Controls)> _bulkRows = new();
    private readonly LipSyncAvailablePanel _available;

    public VisualElement SelectedElement { get; } = new SpacedVerticalElement();
    public VisualElement AvailableElement => _available.Element;

    public LipSyncPanel(
        FacialShapesEditorContext context,
        LipSyncEditing editing)
    {
        _context = context;
        _editing = editing;
        _canceller = context.DataManagers[0];
        _available = new LipSyncAvailablePanel(context, editing, _canceller);

        SetupSelected();
        SelectedElement.Add(CreateModeField());
        SelectedElement.Add(_selected);

        context.GroupManager.OnGroupSelectionChanged += _ => Rebuild();
        context.GroupManager.OnLeftSelectionChanged += _ => RebuildRows();
        context.GroupManager.OnRightSelectionChanged += _ => _available.Rebuild();
        editing.StructureChanged += RebuildRows;
        _canceller.OnSingleShapeAdded += _ => RebuildRows();
        _canceller.OnMultipleShapesAdded += _ => RebuildRows();
        _canceller.OnSingleShapeRemoved += _ => RebuildRows();
        _canceller.OnMultipleShapesRemoved += _ => RebuildRows();
        _canceller.OnUnknownChange += RebuildRows;
        Rebuild();
    }

    private void SetupSelected()
    {
        _selected.fixedItemHeight = FacialShapeUI.ListItemHeight;
        _selected.selectionType = SelectionType.None;
        _selected.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
        _selected.style.flexGrow = 1f;
        _selected.makeItem = MakeSelectedItem;
        _selected.bindItem = BindSelectedItem;
        _selected.itemsSource = _rows;
    }

    private VisualElement MakeSelectedItem()
    {
        var root = new HorizontalElement();
        root.style.alignItems = Align.Center;
        var header = new SimpleToggle { name = "header" };
        header.style.width = HeaderWidth;
        header.style.minWidth = HeaderWidth;
        header.style.maxWidth = HeaderWidth;
        header.style.height = FacialShapeUI.RowHeight;
        header.style.marginRight = FacialShapeUI.Spacing;
        header.RegisterValueChangedCallback(evt =>
        {
            if (root.userData is not RowData { ShowHeader: true } row) return;
            if (!evt.newValue)
            {
                header.SetValueWithoutNotify(true);
                return;
            }
            if (row.Canceller)
                _editing.SelectCanceller();
            else
                _editing.SetSelectedViseme(
                    row.SectionIndex >= 0 ? row.SectionIndex : _editing.SelectedViseme);
            _selected.RefreshItems();
            _available.Rebuild();
        });
        header.RegisterCallback<MouseEnterEvent>(_ =>
        {
            if (root.userData is RowData
                {
                    ShowHeader: true,
                    Canceller: false,
                    SectionIndex: >= 0
                } row)
                _editing.SetHoveredViseme(row.SectionIndex);
        });
        header.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            if (root.userData is RowData
                {
                    ShowHeader: true,
                    Canceller: false,
                    SectionIndex: >= 0
                })
                _editing.SetHoveredViseme(-1);
        });

        var bulk = new BulkShapeControls(
            weight =>
            {
                if (root.userData is RowData { Kind: RowKind.Controls } row)
                    SetSectionWeights(row, weight);
            },
            () =>
            {
                if (root.userData is RowData { Kind: RowKind.Controls } row)
                    RemoveSectionZeros(row);
            },
            () =>
            {
                if (root.userData is RowData { Kind: RowKind.Controls } row)
                    RemoveSectionShapes(row);
            });
        bulk.Element.name = "bulk";
        bulk.Element.userData = bulk;
        _bulkRows.Add((root, bulk));

        var shape = new SelectedShapeRow { name = "shape" };
        shape.style.flexGrow = 1f;
        shape.FacialRail.style.opacity = 0f;
        shape.Curve.SetVisible(false);
        shape.CurveToggle.SetVisible(false);
        shape.Weight.RegisterValueChangedCallback(evt =>
        {
            if (root.userData is not RowData row || row.Kind != RowKind.Shape) return;
            SetWeight(row, evt.newValue);
            shape.SetChanged(IsChanged(row, evt.newValue));
        });
        shape.WeightToggle.clicked += () =>
        {
            if (root.userData is not RowData row || row.Kind != RowKind.Shape) return;
            var weight = Mathf.Approximately(GetWeight(row), 0f) ? 100f : 0f;
            SetWeight(row, weight);
            shape.Weight.SetValueWithoutNotify(weight);
            shape.SetChanged(IsChanged(row, weight));
        };
        shape.RemoveButton.clicked += () =>
        {
            if (root.userData is not RowData row || row.Kind != RowKind.Shape) return;
            if (row.Canceller) _canceller.RemoveShape(row.ManagerIndex);
            else _editing.Remove(row.SectionIndex, row.ShapeName);
        };
        shape.Metadata.RegisterCallback<ClickEvent>(_ =>
        {
            if (root.userData is not RowData row || row.Kind != RowKind.Shape) return;
            if (row.Canceller)
                _canceller.TryRestoreShapeToInitialState(row.ManagerIndex);
            else
                _editing.Restore(row.SectionIndex, row.ShapeName);
            var weight = GetWeight(row);
            shape.Weight.SetValueWithoutNotify(weight);
            shape.SetChanged(IsChanged(row, weight));
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

    private void BindSelectedItem(VisualElement element, int index)
    {
        var row = _rows[index];
        element.userData = row;
        var header = element.Q<SimpleToggle>("header");
        var bulkElement = element.Q<VisualElement>("bulk");
        var bulk = (BulkShapeControls)bulkElement.userData;
        var shape = element.Q<SelectedShapeRow>("shape");
        var empty = element.Q<Label>("empty");

        if (row.Kind == RowKind.Spacer)
        {
            header.SetVisible(false);
            bulkElement.SetVisible(false);
            shape.SetVisible(false);
            empty.SetVisible(false);
            return;
        }

        header.SetVisible(true);
        header.style.visibility = row.ShowHeader
            ? Visibility.Visible
            : Visibility.Hidden;
        header.text = row.Canceller
            ? "shapesEditor.conflictCorrection.label".LS()
            : row.Kind == RowKind.Controls
                ? "shapesEditor.lipSync.label".LS()
                : VrcVisemeLipSyncShapes.Names[row.SectionIndex];
        header.tooltip = row.Canceller
            ? "lipSync.cancellerBlendShapes.tooltip".LS()
            : string.Empty;
        var sectionSelected = row.Canceller
            ? _editing.CancellerSelected
            : !_editing.CancellerSelected
              && (row.SectionIndex < 0 || row.SectionIndex == _editing.SelectedViseme);
        header.SetValueWithoutNotify(sectionSelected);
        header.SetEnabled(row.Canceller || _editing.Draft.Mode == LipSyncSettings.Kind.Custom);

        bulkElement.SetVisible(row.Kind == RowKind.Controls);
        bulkElement.SetEnabled(sectionSelected
                               && (row.Canceller
                                   || _editing.Draft.Mode == LipSyncSettings.Kind.Custom));
        if (row.Kind == RowKind.Controls)
            bulk.SetRemoveZeroVisible(!row.Canceller && row.SectionIndex < 0
                ? _editing.HasZeroWeight()
                : SectionShapeRows(row)
                    .Any(shapeRow => Mathf.Approximately(GetWeight(shapeRow), 0f)));
        empty.SetVisible(row.Kind == RowKind.Empty);
        empty.SetEnabled(sectionSelected
                         && (row.Canceller || _editing.Draft.Mode == LipSyncSettings.Kind.Custom));
        shape.SetVisible(row.Kind == RowKind.Shape);
        if (row.Kind != RowKind.Shape) return;

        shape.SetEnabled(sectionSelected
                         && (row.Canceller || _editing.Draft.Mode == LipSyncSettings.Kind.Custom));
        shape.NameLabel.text = row.ShapeName;
        var missing = !_context.Catalog.Contains(row.ShapeName);
        var unavailable = row.Canceller
            ? _canceller.IsExplicitlyExcluded(row.ShapeName)
            : _editing.UnavailableNames.Contains(row.ShapeName);
        shape.SetWarning(missing, unavailable);
        var weight = GetWeight(row);
        shape.Weight.SetValueWithoutNotify(weight);
        shape.SetChanged(IsChanged(row, weight));
    }

    private void RebuildRows()
    {
        _rows.Clear();
        var shapes = _editing.PreviewShapes ?? new VrcVisemeLipSyncShapes();
        _rows.Add(new RowData(RowKind.Controls, -1, string.Empty, -1, false, false));
        for (var visemeIndex = 0; visemeIndex < VrcVisemeLipSyncShapes.Count; visemeIndex++)
        {
            var visible = shapes.GetShapes(visemeIndex)
                .Where(shape => !_context.GroupManager.IsLeftSelected
                                || _context.GroupManager.IsBlendShapeVisible(
                                    _context.Catalog.IndexOf(shape.Name)))
                .ToArray();
            if (visible.Length == 0)
            {
                _rows.Add(new RowData(RowKind.Empty, visemeIndex, string.Empty, -1, false, true));
                continue;
            }
            for (var index = 0; index < visible.Length; index++)
            {
                _rows.Add(new RowData(
                    RowKind.Shape,
                    visemeIndex,
                    visible[index].Name,
                    -1,
                    false,
                    index == 0));
            }
        }

        _rows.Add(new RowData(RowKind.Spacer, -1, string.Empty, -1, false, false));
        _rows.Add(new RowData(RowKind.Controls, -1, string.Empty, -1, true, true));
        var cancellerIndices = _canceller.GetTargetIndices(index =>
                !_context.GroupManager.IsLeftSelected
                || _context.GroupManager.IsBlendShapeVisible(index))
            .ToArray();
        if (cancellerIndices.Length == 0)
        {
            _rows.Add(new RowData(RowKind.Empty, -1, string.Empty, -1, true, false));
        }
        else
        {
            for (var index = 0; index < cancellerIndices.Length; index++)
            {
                var managerIndex = cancellerIndices[index];
                _rows.Add(new RowData(
                    RowKind.Shape,
                    -1,
                    _canceller.AllKeys[managerIndex],
                    managerIndex,
                    true,
                    false));
            }
        }
        _selected.RefreshItems();
    }

    private RowData[] SectionShapeRows(RowData section)
    {
        var visemeIndex = section.SectionIndex >= 0
            ? section.SectionIndex
            : _editing.SelectedViseme;
        return _rows.Where(row => row.Kind == RowKind.Shape
                                  && row.Canceller == section.Canceller
                                  && (row.Canceller || row.SectionIndex == visemeIndex))
            .ToArray();
    }

    private void SetSectionWeights(RowData section, float weight)
    {
        if (section.Canceller)
        {
            _canceller.SetShapesWeight(
                SectionShapeRows(section).Select(row => row.ManagerIndex),
                weight);
        }
        else
        {
            _editing.SetAllWeights(weight);
        }
        _selected.RefreshItems();
    }

    private void RemoveSectionZeros(RowData section)
    {
        if (section.Canceller)
        {
            var rows = SectionShapeRows(section)
                .Where(row => Mathf.Approximately(GetWeight(row), 0f));
            _canceller.RemoveShapes(rows.Select(row => row.ManagerIndex));
        }
        else
        {
            _editing.RemoveAllShapes(zeroOnly: true);
        }
    }

    private void RemoveSectionShapes(RowData section)
    {
        if (section.Canceller)
            _canceller.RemoveShapes(SectionShapeRows(section)
                .Select(row => row.ManagerIndex));
        else
            _editing.RemoveAllShapes(zeroOnly: false);
    }

    private float GetWeight(RowData row)
        => row.Canceller
            ? _canceller.GetEffectiveShapeWeight(row.ManagerIndex)
            : _editing.GetWeight(row.SectionIndex, row.ShapeName);

    private void SetWeight(RowData row, float weight)
    {
        if (row.Canceller) _canceller.SetShapeWeight(row.ManagerIndex, weight);
        else _editing.SetWeight(row.SectionIndex, row.ShapeName, weight);
        RefreshBulkControl(row);
    }

    private void RefreshBulkControl(RowData changedRow)
    {
        foreach (var (root, controls) in _bulkRows)
        {
            if (root.userData is not RowData { Kind: RowKind.Controls } section
                || section.Canceller != changedRow.Canceller)
                continue;
            controls.SetRemoveZeroVisible(section.Canceller
                ? SectionShapeRows(section)
                    .Any(row => Mathf.Approximately(GetWeight(row), 0f))
                : _editing.HasZeroWeight());
        }
    }

    private bool IsChanged(RowData row, float weight)
        => row.Canceller
            ? _canceller.IsShapeChangedFromInitialState(row.ManagerIndex)
            : _editing.IsChanged(
                row.SectionIndex,
                new BlendShapeWeight(row.ShapeName, weight));

    private VisualElement CreateModeField()
    {
        var labels = new[]
        {
            "lipSync.mode.option.builtIn".LS(),
            "lipSync.mode.option.custom".LS()
        };
        var selected = _editing.Draft.Mode == LipSyncSettings.Kind.Custom ? 1 : 0;
        var field = new PopupField<string>(
            "shapesEditor.lipSync.label".LS(),
            labels.ToList(),
            selected);
        field.RegisterValueChangedCallback(evt =>
        {
            var mode = evt.newValue == labels[1]
                ? LipSyncSettings.Kind.Custom
                : LipSyncSettings.Kind.BuiltIn;
            _editing.SetMode(mode);
        });
        return field;
    }

    public void Rebuild()
    {
        RebuildRows();
        _available.Rebuild();
    }
}
