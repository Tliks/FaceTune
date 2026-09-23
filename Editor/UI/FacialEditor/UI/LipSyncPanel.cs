using Aoyon.FaceTune.Gui.Components;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class LipSyncPanel
{
    private const float HeaderWidth = 56f;

    private enum RowKind { Controls, Shape, Empty }
    private enum Page { LipSync, ConflictCorrection }

    private readonly record struct RowData(
        RowKind Kind,
        int SectionIndex,
        string ShapeName,
        int ManagerIndex,
        bool Canceller,
        bool ShowHeader);

    private sealed class RowElement : VisualElement
    {
        public RowKind? Kind;
    }

    private readonly FacialShapesEditorContext _context;
    private readonly LipSyncEditing _editing;
    private readonly BlendShapeOverrideManager _canceller;
    private readonly ListView _selected = new();
    private readonly List<RowData> _rows = new();
    private readonly LipSyncAvailablePanel _available;
    private readonly PopupField<string> _modeField;
    private readonly SimpleToggle _lipSyncPageToggle = new();
    private readonly SimpleToggle _conflictPageToggle = new();
    private Page _page = Page.LipSync;

    public VisualElement SelectedElement { get; } = new SpacedVerticalElement();
    public VisualElement AvailableElement => _available.Element;

    public LipSyncPanel(
        FacialShapesEditorContext context,
        LipSyncEditing editing)
    {
        _context = context;
        _editing = editing;
        _canceller = context.DataManagers[0];

        SelectedElement.styleSheets.Add(
            TrackingShapeRow.SharedStyleSheet);

        _available = new LipSyncAvailablePanel(context, editing, _canceller);

        SetupSelected();
        _modeField = CreateModeField();

        SelectedElement.Add(CreatePageSelector());
        SelectedElement.Add(_modeField);
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
        var root = new RowElement();
        root.style.flexDirection = FlexDirection.Row;
        root.style.alignItems = Align.Center;
        return root;
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

    private SimpleToggle BuildHeader(RowElement root)
    {
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
            _editing.SetSelectedViseme(row.SectionIndex);
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
        return header;
    }

    private void BuildControlsRow(RowElement root)
    {
        var header = BuildHeader(root);
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
            },
            trackingColumns: true);
        bulk.Element.name = "bulk";
        bulk.Element.userData = bulk;

        root.Add(header);
        root.Add(bulk.Element);
    }

    private void BuildShapeRow(RowElement root)
    {
        var header = BuildHeader(root);
        var shape = new TrackingShapeRow
        {
            name = "shape"
        };
        shape.style.flexGrow = 1f;
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

        root.Add(header);
        root.Add(shape);
    }

    private void BuildEmptyRow(RowElement root)
    {
        var header = BuildHeader(root);
        var empty = TrackingShapeRow.CreateEmptyLabel();
        empty.name = "empty";
        empty.style.flexGrow = 1f;
        root.Add(header);
        root.Add(empty);
    }

    private void BindSelectedItem(VisualElement element, int index)
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

        var header = root.Q<SimpleToggle>("header");
        header.style.display = _page == Page.ConflictCorrection
            ? DisplayStyle.None
            : DisplayStyle.Flex;
        header.style.visibility = row.ShowHeader
            ? Visibility.Visible
            : Visibility.Hidden;
        if (row.ShowHeader)
            header.text = VrcVisemeLipSyncShapes.Names[row.SectionIndex];

        var sectionSelected = row.Canceller
            ? _editing.CancellerSelected
            : !_editing.CancellerSelected
              && (row.SectionIndex < 0 || row.SectionIndex == _editing.SelectedViseme);
        header.SetValueWithoutNotify(sectionSelected);
        header.SetEnabled(row.Canceller || _editing.Draft.Mode == LipSyncSettings.Kind.Custom);

        if (row.Kind == RowKind.Controls)
        {
            var bulkElement = root.Q<VisualElement>("bulk");
            var bulk = (BulkShapeControls)bulkElement.userData;
            bulkElement.SetEnabled(sectionSelected
                                   && (row.Canceller
                                       || _editing.Draft.Mode == LipSyncSettings.Kind.Custom));
            bulk.SetRemoveZeroVisible(!row.Canceller && _editing.HasZeroWeight());
            return;
        }

        if (row.Kind == RowKind.Empty)
        {
            root.Q<Label>("empty").SetEnabled(sectionSelected
                             && (row.Canceller || _editing.Draft.Mode == LipSyncSettings.Kind.Custom));
            return;
        }

        var shape = root.Q<TrackingShapeRow>("shape");
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
        shape.Metadata.SetEnabled(row.Canceller || _editing.Draft.Mode == LipSyncSettings.Kind.Custom);
    }

    private VisualElement CreatePageSelector()
    {
        var container = new HorizontalElement();

        _lipSyncPageToggle.text = "shapesEditor.lipSync.label".LS();
        _lipSyncPageToggle.AddToClassList("compact-control");
        _lipSyncPageToggle.style.flexGrow = 1f;
        _lipSyncPageToggle.style.marginRight = FacialShapeUI.Spacing;

        _conflictPageToggle.text = "shapesEditor.conflictCorrection.label".LS();
        _conflictPageToggle.AddToClassList("compact-control");
        _conflictPageToggle.style.flexGrow = 1f;

        _lipSyncPageToggle.SetValueWithoutNotify(true);
        _conflictPageToggle.SetValueWithoutNotify(false);

        _lipSyncPageToggle.RegisterValueChangedCallback(evt =>
        {
            if (!evt.newValue)
            {
                if (_page == Page.LipSync)
                    _lipSyncPageToggle.SetValueWithoutNotify(true);

                return;
            }

            SetPage(Page.LipSync);
        });

        _conflictPageToggle.RegisterValueChangedCallback(evt =>
        {
            if (!evt.newValue)
            {
                if (_page == Page.ConflictCorrection)
                    _conflictPageToggle.SetValueWithoutNotify(true);

                return;
            }

            SetPage(Page.ConflictCorrection);
        });

        container.Add(_lipSyncPageToggle);
        container.Add(_conflictPageToggle);

        return container;
    }

    private void SetPage(Page page)
    {
        if (_page == page)
        {
            UpdatePageUI();
            return;
        }

        _page = page;

        if (_page == Page.ConflictCorrection)
            _editing.SelectCanceller();
        else
            _editing.SetSelectedViseme(_editing.SelectedViseme);

        UpdatePageUI();
        RebuildRows();
        _available.Rebuild();
    }

    private void UpdatePageUI()
    {
        var lipSync = _page == Page.LipSync;

        _lipSyncPageToggle.SetValueWithoutNotify(lipSync);
        _conflictPageToggle.SetValueWithoutNotify(!lipSync);

        _modeField.SetVisible(lipSync);
    }

    private void RebuildRows()
    {
        _modeField.SetValueWithoutNotify(_modeField.choices[
            _editing.Draft.Mode == LipSyncSettings.Kind.Custom ? 1 : 0]);
        _rows.Clear();

        if (_page == Page.LipSync)
            BuildLipSyncRows();
        else
            BuildConflictCorrectionRows();

        _selected.RefreshItems();
    }

    private void BuildLipSyncRows()
    {
        var shapes = _editing.PreviewShapes ?? new VrcVisemeLipSyncShapes();

        // ページ全体の一括操作。必ず1個だけ。
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
    }

    private void BuildConflictCorrectionRows()
    {
        // 干渉対策ページ全体の一括操作。1個だけ。
        _rows.Add(new RowData(RowKind.Controls, -1, string.Empty, -1, true, false));

        var indices = _canceller.GetTargetIndices(index =>
                !_context.GroupManager.IsLeftSelected
                || _context.GroupManager.IsBlendShapeVisible(index))
            .ToArray();
        if (indices.Length == 0)
        {
            _rows.Add(new RowData(RowKind.Empty, -1, string.Empty, -1, true, false));
            return;
        }
        foreach (var managerIndex in indices)
        {
            _rows.Add(new RowData(
                RowKind.Shape,
                -1,
                _canceller.AllKeys[managerIndex],
                managerIndex,
                true,
                false));
        }
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
            : _editing.PreviewShapes?.GetShapes(row.SectionIndex)
                .FirstOrDefault(shape => shape.Name == row.ShapeName).Weight ?? 0f;

    private void SetWeight(RowData row, float weight)
    {
        if (row.Canceller) _canceller.SetShapeWeight(row.ManagerIndex, weight);
        else _editing.SetWeight(row.SectionIndex, row.ShapeName, weight);
        RefreshBulkControl();
    }

    private void RefreshBulkControl()
    {
        if (_page != Page.LipSync)
            return;

        foreach (var root in _selected.Query<RowElement>().ToList())
        {
            if (root.userData is not RowData { Kind: RowKind.Controls, Canceller: false })
                continue;
            var bulk = root.Q<VisualElement>("bulk");
            if (bulk?.userData is BulkShapeControls controls)
                controls.SetRemoveZeroVisible(_editing.HasZeroWeight());
        }
    }

    private bool IsChanged(RowData row, float weight)
        => row.Canceller
            ? _canceller.IsShapeChangedFromInitialState(row.ManagerIndex)
            : _editing.Draft.Mode == LipSyncSettings.Kind.Custom
              && _editing.IsChanged(
                  row.SectionIndex,
                  new BlendShapeWeight(row.ShapeName, weight));

    private PopupField<string> CreateModeField()
    {
        var labels = new[]
        {
            "lipSync.mode.option.builtIn".LS(),
            "lipSync.mode.option.custom".LS()
        };
        var selected = _editing.Draft.Mode == LipSyncSettings.Kind.Custom ? 1 : 0;
        var field = new PopupField<string>(
            "shapesEditor.mode.label".LS(),
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
