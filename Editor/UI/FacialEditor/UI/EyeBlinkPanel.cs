using Aoyon.FaceTune.Gui.Components;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class EyeBlinkPanel : IDisposable
{
    private enum RowKind { Controls, Shape, Empty }
    private readonly record struct RowData(RowKind Kind, int ListIndex, int ManagerIndex);

    private sealed class RowElement : VisualElement
    {
        public RowKind? Kind;
    }

    private readonly FacialShapesEditorContext _context;
    private readonly UnselectedPanel?[] _availablePanels;
    private readonly ListView _selected = new();
    private readonly List<RowData> _rows = new();
    private readonly SimpleToggle _blinkPageToggle = new();
    private readonly SimpleToggle _conflictPageToggle = new();

    public VisualElement SelectedElement { get; } = new SpacedVerticalElement();
    public VisualElement AvailableElement { get; } = new VisualElement();

    public EyeBlinkPanel(FacialShapesEditorContext context)
    {
        _context = context;

        SelectedElement.styleSheets.Add(
            TrackingShapeRow.SharedStyleSheet);

        _availablePanels = new UnselectedPanel?[context.DataManagers.Count];

        SelectedElement.Add(CreatePageSelector());
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
        UpdateSelection();
    }

    private VisualElement MakeItem()
    {
        var root = new RowElement();
        root.style.flexDirection = FlexDirection.Row;
        root.style.alignItems = Align.Center;
        return root;
    }

    private VisualElement CreatePageSelector()
    {
        var container = new HorizontalElement();

        _blinkPageToggle.text = "shapesEditor.eyeBlink.label".LS();
        _blinkPageToggle.AddToClassList("compact-control");
        _blinkPageToggle.style.flexGrow = 1f;
        _blinkPageToggle.style.marginRight = FacialShapeUI.Spacing;

        _conflictPageToggle.text = "shapesEditor.conflictCorrection.label".LS();
        _conflictPageToggle.AddToClassList("compact-control");
        _conflictPageToggle.style.flexGrow = 1f;

        _blinkPageToggle.RegisterValueChangedCallback(evt =>
        {
            if (!evt.newValue)
            {
                if (_context.ActiveListIndex == 0)
                    _blinkPageToggle.SetValueWithoutNotify(true);

                return;
            }

            _context.SetActiveList(0);
        });

        _conflictPageToggle.RegisterValueChangedCallback(evt =>
        {
            if (!evt.newValue)
            {
                if (_context.ActiveListIndex == 1)
                    _conflictPageToggle.SetValueWithoutNotify(true);

                return;
            }

            _context.SetActiveList(1);
        });

        container.Add(_blinkPageToggle);
        container.Add(_conflictPageToggle);

        return container;
    }

    private void UpdatePageSelector()
    {
        var blink = _context.ActiveListIndex == 0;

        _blinkPageToggle.SetValueWithoutNotify(blink);
        _conflictPageToggle.SetValueWithoutNotify(!blink);
    }

    private void BuildRowElement(RowElement root, RowKind kind)
    {
        switch (kind)
        {
            case RowKind.Controls:
                BuildControlsRow(root);
                break;

            case RowKind.Shape:
                BuildShapeRow(root);
                break;

            case RowKind.Empty:
                BuildEmptyRow(root);
                break;
        }
    }

    private void BuildControlsRow(RowElement root)
    {
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
            },
            trackingColumns: true);
        bulk.Element.name = "bulk";
        bulk.Element.style.flexGrow = 1f;
        bulk.Element.userData = bulk;

        root.Add(bulk.Element);
    }

    private void BuildShapeRow(RowElement root)
    {
        var shape = new TrackingShapeRow
        {
            name = "shape"
        };
        shape.style.flexGrow = 1f;
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

        root.Add(shape);
    }

    private void BuildEmptyRow(RowElement root)
    {
        var empty = TrackingShapeRow.CreateEmptyLabel();
        empty.name = "empty";
        empty.style.flexGrow = 1f;
        root.Add(empty);
    }

    private void BindItem(VisualElement element, int index)
    {
        var row = _rows[index];
        var root = (RowElement)element;
        root.userData = row;

        if (root.Kind != row.Kind)
        {
            root.Clear();
            root.Kind = row.Kind;
            BuildRowElement(root, row.Kind);
        }

        switch (row.Kind)
        {
            case RowKind.Controls:
            {
                var bulkElement = root.Q<VisualElement>("bulk");
                var bulk = (BulkShapeControls)bulkElement.userData;
                // 干渉補正はWeight 0で追加する列表のため、0-を出さない
                bulk.SetRemoveZeroVisible(row.ListIndex == 0 && HasZeroShape(row.ListIndex));
                break;
            }

            case RowKind.Empty:
                break;

            case RowKind.Shape:
            {
                var shape = root.Q<TrackingShapeRow>("shape");
                var manager = _context.DataManagers[row.ListIndex];
                var name = manager.AllKeys[row.ManagerIndex];
                shape.NameLabel.text = name;
                shape.SetWarning(manager.IsMissing(row.ManagerIndex), manager.IsExplicitlyExcluded(name));
                shape.SetChanged(manager.IsShapeChangedFromInitialState(row.ManagerIndex));
                shape.Weight.SetValueWithoutNotify(manager.GetEffectiveShapeWeight(row.ManagerIndex));
                break;
            }
        }
    }

    private void RebuildRows()
    {
        _rows.Clear();

        var listIndex = _context.ActiveListIndex;
        var manager = _context.DataManagers[listIndex];

        // 現在ページ全体の一括操作。1個だけ。
        _rows.Add(new RowData(RowKind.Controls, listIndex, -1));

        var indices = manager.GetTargetIndices(index =>
                !_context.GroupManager.IsLeftSelected
                || _context.GroupManager.IsBlendShapeVisible(index))
            .ToArray();
        if (indices.Length == 0)
        {
            _rows.Add(new RowData(RowKind.Empty, listIndex, -1));
        }
        else
        {
            foreach (var managerIndex in indices)
            {
                _rows.Add(new RowData(RowKind.Shape, listIndex, managerIndex));
            }
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

    private bool HasZeroShape(int listIndex)
    {
        var manager = _context.DataManagers[listIndex];
        return VisibleTargetIndices(listIndex).Any(manager.IsExplicitZeroTarget);
    }

    private void RemoveSectionZeros(int listIndex)
    {
        var manager = _context.DataManagers[listIndex];
        manager.RemoveShapes(VisibleTargetIndices(listIndex)
            .Where(manager.IsExplicitZeroTarget));
    }

    private void RefreshBulkControl(int listIndex)
    {
        if (listIndex != 0) return;
        foreach (var root in _selected.Query<RowElement>().ToList())
        {
            if (root.userData is not RowData { Kind: RowKind.Controls } row
                || row.ListIndex != listIndex) continue;
            var bulk = root.Q<VisualElement>("bulk");
            if (bulk?.userData is BulkShapeControls controls)
                controls.SetRemoveZeroVisible(HasZeroShape(listIndex));
        }
    }

    private UnselectedPanel GetAvailablePanel(int index)
    {
        var existing = _availablePanels[index];
        if (existing != null)
            return existing;

        var panel = new UnselectedPanel(
            _context.DataManagers[index],
            _context.GroupManager,
            _context.PreviewManager,
            index == 1 ? 0f : 100f);

        _availablePanels[index] = panel;
        return panel;
    }

    private void UpdateSelection()
    {
        if (_context.ModeSession is EyeBlinkModeSession
            { Mode: not EyeBlinkSettings.Kind.SimpleAnimation }) return;
        UpdatePageSelector();
        RebuildRows();

        AvailableElement.Clear();
        AvailableElement.Add(
        GetAvailablePanel(_context.ActiveListIndex).Element);
    }

    public void Dispose()
    {
        _context.ActiveListChanged -= UpdateSelection;
    }
}

internal sealed class EyeBlinkBuiltInPanel
{
    public VisualElement Element { get; } = new SpacedVerticalElement();

    public EyeBlinkBuiltInPanel(FacialShapesEditorContext context,
        IReadOnlyList<BlendShapeWeightAnimation>? animations)
    {
        Element.styleSheets.Add(TrackingShapeRow.SharedStyleSheet);
        Element.style.minWidth = 0f;
        if (animations == null)
        {
            var message = new Label("eyeBlink.builtIn.unavailable.message".LS());
            message.style.whiteSpace = WhiteSpace.Normal;
            message.style.maxWidth = Length.Percent(100f);
            Element.Add(message);
            return;
        }
        foreach (var animation in animations)
        {
            var row = new TrackingShapeRow();
            row.NameLabel.text = animation.Name;
            row.Weight.SetValueWithoutNotify(EyeBlinkModeConversion.ClosedWeight(animation));
            row.SetWarning(!context.Catalog.Contains(animation.Name), false);
            row.SetEnabled(false);
            row.style.height = FacialShapeUI.ListItemHeight;
            Element.Add(row);
        }
    }
}
