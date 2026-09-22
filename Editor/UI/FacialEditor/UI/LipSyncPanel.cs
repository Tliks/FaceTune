using Aoyon.FaceTune.Gui.Components;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class LipSyncPanel
{
    private const float HeaderWidth = 56f;
    private readonly FacialShapesEditorContext _context;
    private readonly LipSyncEditing _editing;
    private readonly BlendShapeOverrideManager _canceller;
    private readonly TextField _availableSearch = new PlaceholderTextField();
    private readonly ScrollView _sections = new();
    private readonly ListView _available = new();
    private readonly List<string> _availableNames = new();
    private readonly List<SimpleToggle> _visemeButtons = new();
    private readonly List<VisualElement> _visemeContents = new();
    private SimpleToggle? _cancellerButton;
    private VisualElement? _cancellerContent;

    public VisualElement SelectedElement { get; } = new SpacedVerticalElement();
    public VisualElement AvailableElement { get; } = new SpacedVerticalElement();

    public LipSyncPanel(
        FacialShapesEditorContext context,
        LipSyncEditing editing)
    {
        _context = context;
        _editing = editing;
        _canceller = context.DataManagers[0];
        _availableSearch.value = string.Empty;
        if (_availableSearch is PlaceholderTextField available)
            available.Placeholder = "facialEditor.search.placeholder".LS();
        _availableSearch.RegisterValueChangedCallback(_ => RebuildAvailable());

        _sections.style.flexGrow = 1f;
        _available.style.flexGrow = 1f;
        SelectedElement.Add(CreateModeField());
        SelectedElement.Add(_sections);
        AvailableElement.Add(_availableSearch);
        AvailableElement.Add(_available);

        _available.fixedItemHeight = FacialShapeUI.ListItemHeight;
        _available.selectionType = SelectionType.None;
        _available.makeItem = MakeAvailableItem;
        _available.bindItem = BindAvailableItem;
        _available.RegisterCallback<MouseLeaveEvent>(_ =>
            _context.PreviewManager.CurrentHoveredIndex = -1);
        context.GroupManager.OnGroupSelectionChanged += _ => Rebuild();
        context.GroupManager.OnLeftSelectionChanged += _ => RebuildSections();
        context.GroupManager.OnRightSelectionChanged += _ => RebuildAvailable();
        _canceller.OnSingleShapeAdded += _ => RebuildSections();
        _canceller.OnMultipleShapesAdded += _ => RebuildSections();
        _canceller.OnSingleShapeRemoved += _ => RebuildSections();
        _canceller.OnMultipleShapesRemoved += _ => RebuildSections();
        _canceller.OnUnknownChange += RebuildSections;

        Rebuild();
    }

    private VisualElement CreateModeField()
    {
        var labels = new[]
        {
            "lipSync.mode.option.builtIn".LS(),
            "lipSync.mode.option.custom".LS()
        };
        var selected = _editing.Draft.Mode == LipSyncSettings.Kind.Custom ? 1 : 0;
        var field = new PopupField<string>(
            "lipSync.mode.label".LS(),
            labels.ToList(),
            selected);
        field.RegisterValueChangedCallback(evt =>
        {
            var custom = evt.newValue == labels[1];
            SetMode(custom ? LipSyncSettings.Kind.Custom : LipSyncSettings.Kind.BuiltIn);
        });
        return field;
    }

    private void SetMode(LipSyncSettings.Kind mode)
    {
        if (_editing.Draft.Mode == mode) return;
        _editing.SetMode(mode);
        Rebuild();
    }

    public void Rebuild()
    {
        RebuildSections();
        RebuildAvailable();
    }

    private void RebuildSections()
    {
        _sections.Clear();
        _visemeButtons.Clear();
        _visemeContents.Clear();
        _cancellerButton = null;
        _cancellerContent = null;
        var values = (_editing.PreviewShapes ?? new VrcVisemeLipSyncShapes())
            .GetOrderedShapes();
        for (var index = 0; index < VrcVisemeLipSyncShapes.Count; index++)
        {
            var visemeIndex = index;
            var section = new HorizontalElement();
            section.style.alignItems = Align.FlexStart;
            section.style.marginBottom = FacialShapeUI.Spacing;
            var button = new SimpleToggle
            {
                text = VrcVisemeLipSyncShapes.Names[index],
                value = !_editing.CancellerSelected && index == _editing.SelectedViseme
            };
            button.style.width = HeaderWidth;
            button.style.minWidth = HeaderWidth;
            button.style.maxWidth = HeaderWidth;
            button.style.height = FacialShapeUI.RowHeight;
            button.style.marginRight = FacialShapeUI.Spacing;
            button.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue)
                {
                    button.SetValueWithoutNotify(true);
                    return;
                }
                _editing.SetSelectedViseme(visemeIndex);
                UpdateSelection();
                RebuildAvailable();
            });
            button.RegisterCallback<MouseEnterEvent>(_ =>
                _editing.SetHoveredViseme(visemeIndex));
            button.RegisterCallback<MouseLeaveEvent>(_ =>
                _editing.SetHoveredViseme(-1));
            section.Add(button);
            _visemeButtons.Add(button);

            var content = new SpacedVerticalElement();
            content.style.flexGrow = 1f;
            content.SetEnabled(_editing.Draft.Mode == LipSyncSettings.Kind.Custom
                               && !_editing.CancellerSelected
                               && index == _editing.SelectedViseme);
            foreach (var shape in values[index])
            {
                var shapeIndex = _context.Catalog.IndexOf(shape.Name);
                if (_context.GroupManager.IsLeftSelected
                    && !_context.GroupManager.IsBlendShapeVisible(shapeIndex))
                    continue;
                content.Add(CreateShapeRow(index, shape));
            }
            if (content.childCount == 0)
            {
                var empty = new VisualElement();
                empty.style.height = FacialShapeUI.RowHeight;
                content.Add(empty);
            }
            section.Add(content);
            _visemeContents.Add(content);
            _sections.Add(section);
        }
        AddCancellerSection();
    }

    private void AddCancellerSection()
    {
        var section = new HorizontalElement();
        section.style.alignItems = Align.FlexStart;
        var button = new SimpleToggle
        {
            text = "lipSync.canceller.shortLabel".LS(),
            tooltip = "lipSync.cancellerBlendShapes.label".LS(),
            value = _editing.CancellerSelected
        };
        button.style.width = HeaderWidth;
        button.style.minWidth = HeaderWidth;
        button.style.maxWidth = HeaderWidth;
        button.style.height = FacialShapeUI.RowHeight;
        button.style.marginRight = FacialShapeUI.Spacing;
        button.RegisterValueChangedCallback(evt =>
        {
            if (!evt.newValue)
            {
                button.SetValueWithoutNotify(true);
                return;
            }
            _editing.SelectCanceller();
            UpdateSelection();
            RebuildAvailable();
        });
        section.Add(button);
        _cancellerButton = button;

        var content = new SpacedVerticalElement();
        content.style.flexGrow = 1f;
        content.SetEnabled(_editing.CancellerSelected);
        foreach (var index in _canceller.GetTargetIndices(shapeIndex =>
                     !_context.GroupManager.IsLeftSelected
                     || _context.GroupManager.IsBlendShapeVisible(shapeIndex)))
        {
            content.Add(CreateCancellerRow(index));
        }
        if (content.childCount == 0)
        {
            var empty = new VisualElement();
            empty.style.height = FacialShapeUI.RowHeight;
            content.Add(empty);
        }
        section.Add(content);
        _cancellerContent = content;
        _sections.Add(section);
    }

    private VisualElement CreateCancellerRow(int index)
    {
        var row = new SelectedShapeRow();
        var name = _canceller.AllKeys[index];
        row.NameLabel.text = name;
        row.FacialRail.style.opacity = 0f;
        row.SetChanged(_canceller.IsShapeChangedFromInitialState(index));
        row.SetWarning(_canceller.IsMissing(index), _canceller.IsExplicitlyExcluded(name));
        row.Curve.SetVisible(false);
        row.CurveToggle.SetVisible(false);
        row.Weight.SetValueWithoutNotify(_canceller.GetEffectiveShapeWeight(index));
        row.Weight.RegisterValueChangedCallback(evt =>
        {
            _canceller.SetShapeWeight(index, evt.newValue);
            row.SetChanged(_canceller.IsShapeChangedFromInitialState(index));
        });
        row.WeightToggle.clicked += () =>
        {
            var weight = Mathf.Approximately(_canceller.GetEffectiveShapeWeight(index), 0f)
                ? 100f
                : 0f;
            _canceller.SetShapeWeight(index, weight);
            row.Weight.SetValueWithoutNotify(weight);
            row.SetChanged(_canceller.IsShapeChangedFromInitialState(index));
        };
        row.RemoveButton.clicked += () => _canceller.RemoveShape(index);
        row.Metadata.RegisterCallback<ClickEvent>(_ =>
        {
            if (!_canceller.TryRestoreShapeToInitialState(index)) return;
            row.Weight.SetValueWithoutNotify(_canceller.GetEffectiveShapeWeight(index));
            row.SetChanged(false);
        });
        return row;
    }

    private void UpdateSelection()
    {
        var editable = _editing.Draft.Mode == LipSyncSettings.Kind.Custom;
        for (var index = 0; index < _visemeButtons.Count; index++)
        {
            _visemeButtons[index].SetValueWithoutNotify(
                !_editing.CancellerSelected && index == _editing.SelectedViseme);
            _visemeContents[index].SetEnabled(
                editable && !_editing.CancellerSelected && index == _editing.SelectedViseme);
        }
        _cancellerButton?.SetValueWithoutNotify(_editing.CancellerSelected);
        _cancellerContent?.SetEnabled(_editing.CancellerSelected);
    }

    private VisualElement CreateShapeRow(int visemeIndex, BlendShapeWeight shape)
    {
        var row = new SelectedShapeRow();
        row.SetChanged(_editing.IsChanged(visemeIndex, shape));
        row.FacialRail.style.opacity = 0f;
        row.Metadata.RegisterCallback<ClickEvent>(_ =>
        {
            _editing.Restore(visemeIndex, shape.Name);
            Rebuild();
        });
        row.NameLabel.text = shape.Name;
        var shapeIndex = _context.Catalog.IndexOf(shape.Name);
        row.SetWarning(shapeIndex < 0, _editing.UnavailableNames.Contains(shape.Name));
        void ApplyWeight(float value)
        {
            _editing.SetWeight(visemeIndex, shape.Name, value);
            row.Weight.SetValueWithoutNotify(value);
            row.SetChanged(
                _editing.IsChanged(visemeIndex, new BlendShapeWeight(shape.Name, value)));
        }

        row.Weight.SetValueWithoutNotify(shape.Weight);
        row.Weight.RegisterValueChangedCallback(evt => ApplyWeight(evt.newValue));
        row.Curve.SetVisible(false);
        row.CurveToggle.SetVisible(false);
        row.WeightToggle.clicked += () => ApplyWeight(
            Mathf.Approximately(_editing.GetWeight(visemeIndex, shape.Name), 0f) ? 100f : 0f);
        row.RemoveButton.clicked += () =>
        {
            _editing.Remove(visemeIndex, shape.Name);
            Rebuild();
        };
        return row;
    }

    private void RebuildAvailable()
    {
        _availableNames.Clear();
        var search = _availableSearch.value ?? string.Empty;
        foreach (var name in _context.Catalog.Names)
        {
            var shapeIndex = _context.Catalog.IndexOf(name);
            var unavailable = _editing.CancellerSelected
                ? _canceller.IsUnavailable(shapeIndex)
                : _editing.UnavailableNames.Contains(name);
            if (unavailable
                || search.Length > 0
                && name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                || _context.GroupManager.IsRightSelected
                && !_context.GroupManager.IsBlendShapeVisible(shapeIndex))
                continue;
            _availableNames.Add(name);
        }
        _available.itemsSource = _availableNames;
        _available.RefreshItems();
    }

    private VisualElement MakeAvailableItem()
    {
        var element = UnselectedShapeRowUI.Create();
        element.RegisterCallback<ClickEvent>(_ =>
        {
            if (element.userData is not string name) return;
            if (_editing.CancellerSelected)
            {
                _canceller.AddShapeWithWeight(_context.Catalog.IndexOf(name), 100f);
            }
            else
            {
                _editing.Add(name);
                RebuildSections();
            }
            _available.RefreshItems();
        });
        element.RegisterCallback<MouseEnterEvent>(_ =>
        {
            if (element.userData is not string name) return;
            _context.PreviewManager.CurrentHoveredIndex =
                _context.Catalog.IndexOf(name);
        });
        return element;
    }

    private void BindAvailableItem(VisualElement element, int index)
    {
        var name = _availableNames[index];
        element.userData = name;
        element.Q<Label>("name").text = name;
        var selected = _editing.CancellerSelected
            ? _canceller.IsInTarget(_context.Catalog.IndexOf(name))
            : _editing.Contains(_editing.SelectedViseme, name);
        element.SetEnabled(
            !selected
            && (_editing.CancellerSelected
                || _editing.Draft.Mode == LipSyncSettings.Kind.Custom));
    }
}
